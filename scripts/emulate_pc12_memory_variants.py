#!/usr/bin/env python3
"""Emula construtores PG09/0A do PC12: lotes, paginas, monitor, WS e SC.

Executa codigo x86 original em Unicorn, com entradas sinteticas. Intercepta
CRT, relogio e progresso visual; para ANTES de executar TX. Nao abre serial,
nao usa rede e nao inventa ACK. As retomadas indicadas explicitamente saltam
o trecho de comunicacao para testar somente o construtor do proximo quadro.

Requer unicorn==2.1.4 e pefile==2024.8.26. Hash fixo do PC12 v2.1.
"""

import argparse
import pathlib
import re
import struct

from unicorn import Uc, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE
from unicorn.x86_const import (
    UC_X86_REG_EBP, UC_X86_REG_EBX, UC_X86_REG_EDI,
    UC_X86_REG_ESI, UC_X86_REG_ESP,
)

from emulate_pc12_pg33_builder import map_image
from emulate_pc12_monitor_commands import (
    MonitorEmulator, EXE, SHA256, OBJ, STACK, STOP, TX, TX_BUF, TX_LEN,
    NIBBLE_HELPER, IMPORTS, put32, get32, cstring, frame,
)
from emulate_pc12_eeprom_commands import return_call

PROGRESS = 0x4CBD48
READ_ENCODER = 0x4C1BF2
SP = STACK - 0x30000  # abaixo dos locais de 84 KiB do leitor de dados
BANKS = {'V': 0x50, 'D': 0x90, 'WC': 0x70}
LIMITS = {'V': 1024, 'D': 2048, 'WC': 912}
# inicio, ponto onde a pagina e lida, local da pagina, chamada TX, passo, bytes, paginas
PAGES = {
    'V': (0x4B2DD9, 0x4B2DF8, -0x78, 0x4B2F12, 64, 128, 16),
    'D': (0x4B3234, 0x4B3256, -0x90, 0x4B3373, 64, 128, 32),
    'WC': (0x4B36BC, 0x4B36DE, -0xA8, 0x4B37F9, 57, 114, 16),
    'FL': (0x4B3B93, 0x4B3BB9, -0xC0, 0x4B3C42, 10, 200, 13),
}
# inicio, chamada TX, retorno ao laço apos comunicacao, local da janela de progresso
SCATTER = {
    'V': (0x4B801F, 0x4B825C, 0x4B8365, -0x60),
    'D': (0x4B8526, 0x4B8763, 0x4B886C, -0x70),
    'WC': (0x4B8A2F, 0x4B8C6C, 0x4B8D87, -0x80),
}
# Numeros WS selecionados pelo original; o construtor subtrai 1 depois.
WS_NUMBERS = [4, *range(18, 26), *range(41, 48), 49, *range(58, 63),
              12, 63, 64, *range(67, 87)]


class Slice:
    """Maquina isolada, com allowlist por trecho e validacao das importacoes."""

    def __init__(self, emulator, ranges, fixtures=None):
        data, base, sections = emulator.image
        self.mu = Uc(UC_ARCH_X86, UC_MODE_32)
        map_image(self.mu, data, base, sections)
        self.mu.mem_map(OBJ, 0x10000)
        self.mu.mem_map(STACK - 0x40000, 0x50000)
        self.mu.mem_map(STOP, 0x1000)
        self.mu.reg_write(UC_X86_REG_EBP, STACK)
        self.mu.reg_write(UC_X86_REG_ESP, SP)
        put32(self.mu, SP, STOP)
        put32(self.mu, STACK + 8, OBJ)
        for reg in (UC_X86_REG_EBX, UC_X86_REG_EDI, UC_X86_REG_ESI):
            self.mu.reg_write(reg, OBJ)
        put32(self.mu, STACK - 0x60, OBJ + 0x8000)
        self.mu.mem_write(TX_BUF, b'\xcc' * 256)
        self.ranges = ranges + [(NIBBLE_HELPER, 0x4BDFB2), (0x4C3570, 0x4C36AC),
                                (0x4C1753, 0x4C2012)]
        self.fixtures = fixtures or {}
        self.frames = []
        self.nibble_calls = self.encoder_calls = self.progress_calls = 0
        self.finished = False
        self.mu.hook_add(UC_HOOK_CODE, self.hook)

    def hook(self, uc, pc, size, _):
        if pc == TX:
            length = get32(uc, TX_LEN)
            if not 3 <= length <= 255:
                raise ValueError('TX_LEN inesperado: %d' % length)
            raw = bytes(uc.mem_read(TX_BUF, length))
            if length != raw[1] + 3 or sum(raw) & 255 != 255:
                raise ValueError('comprimento/checksum nativo inconsistente')
            self.frames.append(raw)
            self.finished = True
            uc.emu_stop()
            return
        if pc == NIBBLE_HELPER:
            self.nibble_calls += 1
        if pc == READ_ENCODER:
            self.encoder_calls += 1
        if pc in IMPORTS or pc == PROGRESS:
            sp = uc.reg_read(UC_X86_REG_ESP)
            arg = lambda n: get32(uc, sp + 4 + 4 * n)
            if pc == PROGRESS:
                if (arg(1), arg(2), arg(3)) != (0x400, 1, 0x1770):
                    raise ValueError('mensagem visual inesperada')
                self.progress_calls += 1
                return_call(uc)
                return
            name = IMPORTS[pc]
            if name == b'_strcpy':
                uc.mem_write(arg(0), cstring(uc, arg(1)) + b'\0')
                return_call(uc, arg(0))
            elif name == b'_strncat':
                if arg(2) > 32:
                    raise ValueError('strncat inesperado')
                uc.mem_write(arg(0), cstring(uc, arg(0)) +
                             cstring(uc, arg(1))[:arg(2)] + b'\0')
                return_call(uc, arg(0))
            elif name == b'_atol':
                m = re.match(rb'\s*([+-]?\d+)', cstring(uc, arg(0)))
                return_call(uc, int(m.group(1)) if m else 0)
            elif name == b'lstrcmpA':
                a, b = cstring(uc, arg(0)), cstring(uc, arg(1))
                return_call(uc, (a > b) - (a < b), 8)
            elif name == b'wsprintfA':
                if cstring(uc, arg(1)) != b'%04X':
                    raise ValueError('formato nao permitido')
                out = ('%04X' % arg(2)).encode('ascii')
                uc.mem_write(arg(0), out + b'\0')
                return_call(uc, len(out))
            elif name == b'GetTickCount':
                return_call(uc, 1000)
            else:
                raise ValueError('stub nao implementado')
            return
        if not any(a <= pc < b for a, b in self.ranges):
            raise ValueError('instrucao fora da allowlist: %08X' % pc)
        fixture = self.fixtures.pop(pc, None)
        if fixture:
            fixture(uc)

    def run(self, start):
        self.finished = False
        # Retomada explicita do construtor; descarta pilha da chamada TX nao executada.
        self.mu.reg_write(UC_X86_REG_ESP, SP)
        self.mu.emu_start(start, STOP + 1, count=150000)
        if not self.finished:
            raise RuntimeError('nao alcancou TX antes do limite de instrucoes')
        return self.frames[-1]


def verify_imports(emulator):
    p = emulator.pe
    names = {i.address: i.name for d in p.DIRECTORY_ENTRY_IMPORT for i in d.imports}
    code = p.get_data(PROGRESS - p.OPTIONAL_HEADER.ImageBase, 6)
    if code[:2] != b'\xff\x25' or names.get(struct.unpack_from('<I', code, 2)[0]) != \
            b'@TWindow@SendMessageA$quiuil':
        raise ValueError('importacao de progresso nao reconhecida')


def write_model(area, records):
    body = []
    for number, value in records:
        if not 1 <= number <= LIMITS[area] or not 0 <= value <= 65535:
            raise ValueError('fixture fora de faixa')
        offset = number - 1
        body += [BANKS[area] | (offset >> 8), offset & 255, 2,
                 value >> 8, value & 255]
    if not 1 <= len(records) <= 40:
        raise ValueError('lote fora do limite observado')
    return frame([9, len(body)] + body)


def scatter(emulator, area, records):
    start, tx_call, continuation, progress_local = SCATTER[area]
    m = Slice(emulator, [(start, tx_call + 5), (continuation, continuation + 11)])
    put32(m.mu, STACK + progress_local, OBJ + 0x8000)
    put32(m.mu, STACK - 0x3C, len(records))
    m.mu.mem_write(STACK - 0x48, b'\x01\x01')
    for i, (number, value) in enumerate(records, 1):
        put32(m.mu, OBJ + 0x12F4 + 4 * i, number)
        put32(m.mu, STACK - 0x2098 + 4 * i, value)
    start_at = start
    for index in range(0, len(records), 40):
        expected = write_model(area, records[index:index + 40])
        actual = m.run(start_at)
        assert actual == expected, (area, index, actual.hex(), expected.hex())
        assert get32(m.mu, STACK - 0x38) == min(index + 40, len(records)) + 1
        remaining = bytes(m.mu.mem_read(STACK - 0x47, 1))[0]
        assert remaining == int(index + 40 < len(records))
        start_at = continuation
    assert m.nibble_calls == len(records) * 8
    assert m.progress_calls == len(records)
    return m.frames


def page(emulator, area, index):
    start, use, local, tx_call, step, quantity, count = PAGES[area]
    assert 0 <= index < count
    m = Slice(emulator, [(start, tx_call + 5)],
              {use: lambda uc: put32(uc, STACK + local, index)})
    actual = m.run(start)
    offset = index * step
    bank = 0x80 if area == 'FL' else BANKS[area]
    assert actual == frame([10, 3, bank | (offset >> 8), offset & 255, quantity])
    assert get32(m.mu, 0x560360) == quantity
    assert m.nibble_calls == (0 if area == 'FL' else 4)
    return actual


def file_write(emulator, count, hex_mode=False, sentinel=0):
    """FL: confronta ASCII simples e expõe a iteracao extra do ramo hexadecimal."""
    m = Slice(emulator, [(0x4B9084, 0x4B9401), (0x4B9509, 0x4B9514)])
    put32(m.mu, STACK - 0x90, OBJ + 0x8000)
    put32(m.mu, STACK - 0x3C, count)
    m.mu.mem_write(STACK - 0x49, bytes([int(hex_mode), 1, 1]))
    # Byte alem do NUL do texto hexadecimal: prova de leitura fora da string.
    m.mu.mem_write(STACK - 0x2424 + 41, bytes([sentinel, 0]))
    rows = []
    for i in range(count):
        number = (i * 17) % 130 + 1
        data = bytes((i * 29 + j * 13) & 255 for j in range(20)) if hex_mode else \
            ('FILE %03d ABCDEFGHIJK' % number).encode('ascii')
        assert len(data) == 20
        text = data.hex().upper().encode('ascii') if hex_mode else data
        put32(m.mu, OBJ + 0x12F4 + 4 * (i + 1), number)
        m.mu.mem_write(STACK - 0x23F8 + 41 * (i + 1), text + b'\0')
        rows.append((number, data))
    for index in range(0, count, 10):
        actual = m.run(0x4B9084 if index == 0 else 0x4B9509)
        body = []
        for number, data in rows[index:index + 10]:
            body += [0x80, number - 1, 20, *data]
            if hex_mode:
                # Em 4B9240 o laço inclui indice 40: ha 21 conversoes, nao 20.
                extra = sentinel - 55 if ord('A') <= sentinel <= ord('F') else sentinel
                body.append(extra)
        assert actual == frame([9, len(body)] + body)
    assert m.nibble_calls == count * 4 and m.progress_calls == count
    return m.frames


def monitor_read(emulator, area, number, mode=0, block=0):
    # Os dois blocos pedem 16 bytes. O item individual pede 2.
    builders = [(0x4BF871, 0x4BF908, 0), (0x4C015F, 0x4C01F5, 1),
                (0x4C0A54, 0x4C0AF5, 2)]
    start, tx_call, slot = builders[block]
    m = Slice(emulator, [(start, tx_call + 5)])
    address = ('%s%0*d' % (area, 3 if area in ('WC', 'SC') else 4, number))
    m.mu.mem_write(0x52CD78 + slot * 6, address.encode('ascii') + b'\0')
    put32(m.mu, STACK - 0x34, slot)
    m.mu.mem_write(OBJ + 0x14D, bytes([mode]))
    actual = m.run(start)
    bank = {**BANKS, 'X': 0x20, 'Y': 0, 'C': 0x10, 'SC': 0xA0}[area]
    offset = (number - 1) // 8 if area in ('X', 'Y', 'C', 'SC') else number - 1
    assert actual == frame([10, 3, bank | (offset >> 8), offset & 255,
                            2 if block == 2 else 16])
    assert m.encoder_calls == 1
    if area in ('X', 'Y', 'C', 'SC') and mode == 0:
        assert get32(m.mu, OBJ + 0x15A) == (number - 1) % 8
    return actual


def ws_write(emulator, index, value):
    m = Slice(emulator, [(0x4B9C9E, 0x4BA007)],
              {0x4B9CBC: lambda uc: uc.reg_write(UC_X86_REG_EBX, index)})
    number = WS_NUMBERS[index]
    put32(m.mu, STACK - 0x268 + 4 * number, value)
    actual = m.run(0x4B9C9E)
    assert actual == frame([9, 5, 0x60, number - 1, 2, value >> 8, value & 255])
    assert m.nibble_calls == 4 and m.progress_calls == 1
    return actual


def ladder_read(emulator, addresses, listing=False):
    """Dois construtores de monitoracao Ladder, usando contatos sinteticos."""
    start = 0x4C40F3 if listing else 0x4C37B3
    m = Slice(emulator, [(start, 0x4C4C20)])
    m.mu.reg_write(UC_X86_REG_ESI, 0)
    m.mu.reg_write(UC_X86_REG_EDI, 2)
    m.mu.mem_write(OBJ + 0xDE, bytes([0 if listing else 1]))
    put32(m.mu, STACK - 0xE0, 0)
    put32(m.mu, 0x560360, 0)
    # Entradas de interface: grade de uma linha, ou listagem de instrucoes STR.
    m.mu.mem_write(0x5703E6, b'\0')
    m.mu.mem_write(0x5703EA, b'\0\0')
    put32(m.mu, 0x5703F8, 1)
    put32(m.mu, 0x4F9D9C, len(addresses))
    put32(m.mu, 0x530334, 0)
    put32(m.mu, 0x530338, len(addresses) - 1)
    put32(m.mu, 0x570404, len(addresses))
    body = []
    for i, address in enumerate(addresses):
        if listing:
            m.mu.mem_write(0x53034B + 48 * i, b'STR\0')
            m.mu.mem_write(0x530357 + 48 * i, address.encode('ascii') + b'\0')
        else:
            m.mu.mem_write(0x52DC1D + 32 * i, b'70000\0')
            m.mu.mem_write(0x52DC25 + 32 * i, address.encode('ascii') + b'\0')
        if address.startswith('SC'):
            bank, offset, quantity = 0xA0, (int(address[2:]) - 1) // 8, 1
        elif address.startswith('S'):
            bank, offset, quantity = 0x50, int(address[1:3]) - 1, 2
        else:
            bank = {'X': 0x20, 'Y': 0, 'C': 0x10}[address[0]]
            offset, quantity = (int(address[1:]) - 1) // 8, 1
        body += [bank | (offset >> 8), offset & 255, quantity]
    actual = m.run(start)
    assert actual == frame([10, len(body)] + body)
    assert get32(m.mu, 0x560360) == sum(body[2::3])
    return actual


def sc_write(emulator, values):
    m = Slice(emulator, [(0x4BA20E, 0x4BA344), (0x4BA4AB, 0x4BA514)])
    for number, value in values.items():
        put32(m.mu, STACK - 0x268 + 4 * number, value)
    first = m.run(0x4BA20E)
    second = m.run(0x4BA4AB)
    low = sum(1 << bit for bit in range(8) if values.get(bit + 1, 0) == 1)
    high = sum(1 << bit for bit in range(7) if values.get(bit + 17, 0) == 1)
    assert first == frame([9, 4, 0xA0, 0, 1, low])
    assert second == frame([9, 4, 0xA0, 2, 1, high])
    return [first, second]


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--exe', default=EXE)
    ap.add_argument('-o', '--output')
    args = ap.parse_args()
    emulator = MonitorEmulator(args.exe)
    verify_imports(emulator)
    lines = ['PC12 PG09/0A — variantes de memoria', 'SHA256 ' + SHA256,
             'TX interceptado antes de executar; entradas sinteticas; sem PLC/ACK.', '']
    counts = {}

    counts['lotes V/D/WC'] = 0
    for area in BANKS:
        for count in (1, 2, 39, 40, 41, 80, 81):
            records = [((i * 257) % LIMITS[area] + 1, (i * 32767 + 0x1234) & 65535)
                       for i in range(count)]
            packets = scatter(emulator, area, records)
            counts['lotes V/D/WC'] += len(packets)
            lines.append('%s %d registros: pacotes=%s bytes=%s PASS' %
                         (area, count, [p[1] // 5 for p in packets], list(map(len, packets))))
    examples = scatter(emulator, 'D', [(1, 0x1234), (257, 0x8000), (2048, 0xFFFF)])
    counts['lotes V/D/WC'] += len(examples)
    lines += ['D nao contiguo: ' + examples[0].hex(' ').upper(), '']

    counts['paginas V/D/WC/FL'] = 0
    for area, spec in PAGES.items():
        packets = [page(emulator, area, i) for i in range(spec[-1])]
        counts['paginas V/D/WC/FL'] += len(packets)
        lines.append('%s %d paginas: primeira=%s ultima=%s PASS' %
                     (area, len(packets), packets[0].hex(' ').upper(),
                      packets[-1].hex(' ').upper()))

    counts['FL escrita'] = 0
    for hex_mode in (False, True):
        for count in (1, 9, 10, 11):
            packets = file_write(emulator, count, hex_mode)
            counts['FL escrita'] += len(packets)
            lines.append('FL %s %d registros: bytes=%s PASS' %
                         ('HEX' if hex_mode else 'ASCII', count, list(map(len, packets))))
    p = file_write(emulator, 1, True, sentinel=ord('A'))[0]
    counts['FL escrita'] += 1
    lines += ['FL HEX sentinela A alem do NUL: ' + p.hex(' ').upper(),
              'ANOMALIA NATIVA: HEX declara 20 bytes, mas monta 21; ultimo le alem do NUL.',
              'ASCII simples monta 20 bytes por FL. Nao copiar a anomalia como especificacao.']

    counts['monitor 0A'] = 0
    limits = {**LIMITS, 'X': 384, 'Y': 384, 'C': 2048, 'SC': 128}
    for area, limit in limits.items():
        numbers = sorted({1, 8, 9, 16, 17, min(256, limit), min(257, limit), limit})
        for number in numbers:
            for mode in (0, 1):
                for block in (0, 1, 2):
                    monitor_read(emulator, area, number, mode, block)
                    counts['monitor 0A'] += 1
    lines += ['', 'Monitor: bancos X=20 Y=00 C=10 SC=A0 V=50 D=90 WC=70 PASS',
              'X/Y/C/SC: offset=(n-1)//8; V/D/WC: offset=n-1.',
              '2 blocos de 16 bytes e item individual de 2 bytes; flag 14D=0/1.', '']
    counts['RTC enderecos'] = 0
    for number in range(1018, 1025):
        p = monitor_read(emulator, 'V', number, block=2)
        counts['RTC enderecos'] += 1
        lines.append('V%d: %s PASS' % (number, p.hex(' ').upper()))

    counts['WS escrita'] = 0
    assert len(WS_NUMBERS) == 45 and len(set(WS_NUMBERS)) == 45
    for i in range(len(WS_NUMBERS)):
        for value in (0, 0x1234, 0x8000, 0xFFFF):
            ws_write(emulator, i, value)
            counts['WS escrita'] += 1
    lines += ['', 'WS selecionados: ' + ','.join(map(str, WS_NUMBERS)),
              'Cada escrita: 09 05 60 (numero-1) 02 valorH valorL CHK PASS']

    counts['SC escrita'] = 0
    cases = [{}, {n: 1 for n in (*range(1, 9), *range(17, 24))},
             {n: 2 for n in range(1, 25)}, {9: 1, 16: 1, 24: 1}]
    cases += [{n: 1} for n in (*range(1, 9), *range(17, 24))]
    for values in cases:
        packets = sc_write(emulator, values)
        counts['SC escrita'] += len(packets)
    lines += ['SC001-008 -> A000 bits 0-7; SC017-023 -> A002 bits 0-6 PASS',
              'Somente inteiro 1 liga o bit; demais valores testados nao ligam.', '']

    counts['Ladder leitura multipla'] = 0
    for listing in (False, True):
        for addresses in [['X0001'], ['X0008', 'X0009', 'Y0384'],
                          ['C0001', 'C2048', 'SC001', 'SC128'],
                          ['S0101', 'S0816', 'X0384']]:
            p = ladder_read(emulator, addresses, listing)
            counts['Ladder leitura multipla'] += 1
            lines.append('Ladder %s %s: %s PASS' %
                         ('lista' if listing else 'grade', ','.join(addresses),
                          p.hex(' ').upper()))

    counts['WS leitura'] = 0
    for page_number in (1, 2):
        m = Slice(emulator, [(0x4B4459, 0x4B44F2)])
        put32(m.mu, STACK - 0x40, page_number)
        p = m.run(0x4B4459)
        assert p == frame([10, 3, 0x60, 0 if page_number == 1 else 0xAC, 0xAC])
        counts['WS leitura'] += 1
        lines.append('WS pagina %d: %s PASS' % (page_number, p.hex(' ').upper()))
    lines += ['Layout WS original preservado; nao generalizar o passo V/D/WC para WS.', '']

    counts['WS039/040 e pedidos fixos'] = 0
    for index in (1, 2):
        m = Slice(emulator, [(0x4B413F, 0x4B41CB)],
                  {0x4B4160: lambda uc: uc.reg_write(UC_X86_REG_EBX, index)})
        p = m.run(0x4B413F)
        assert p == frame([10, 3, 0x60, 0x25 + index, 2])
        counts['WS039/040 e pedidos fixos'] += 1
        lines.append('Leitura WS%03d: %s PASS' % (38 + index, p.hex(' ').upper()))
        for values in ((0x12, 0x34, 0xAB, 0xCD), (0x80, 0, 0xFF, 0xFF)):
            m = Slice(emulator, [(0x4B98DA, 0x4B9999)],
                      {0x4B98FB: lambda uc: uc.reg_write(UC_X86_REG_ESI, index)})
            for offset, value in zip((-0x29, -0x2A, -0x2B, -0x2C), values):
                m.mu.mem_write(STACK + offset, bytes([value]))
            p = m.run(0x4B98DA)
            pair = list(values[:2] if index == 1 else values[2:])
            assert p == frame([9, 5, 0x60, 0x25 + index, 2] + pair)
            counts['WS039/040 e pedidos fixos'] += 1
            lines.append('Escrita sintetica WS%03d: %s PASS' %
                         (38 + index, p.hex(' ').upper()))
    for start, tx_call, prefix, label in (
        (0x4BD312, 0x4BD385, [10, 3, 0x60, 5, 2], 'Leitura no handler de erro A'),
        (0x4BD45E, 0x4BD4D0, [10, 3, 0x60, 4, 2], 'Leitura no handler de erro B'),
        (0x4BD5FE, 0x4BD68D, [9, 5, 0x60, 0x2A, 2, 0, 0], 'Sair de Remote I/O'),
        (0x4C153C, 0x4C15A6, [10, 3, 0x53, 0xF9, 6], 'RTC hora/minuto/segundo'),
    ):
        m = Slice(emulator, [(start, tx_call + 5)])
        p = m.run(start)
        assert p == frame(prefix)
        counts['WS039/040 e pedidos fixos'] += 1
        lines.append('%s: %s PASS' % (label, p.hex(' ').upper()))
    lines.append('')
    for group, count in counts.items():
        lines.append('%s: %d quadros PASS' % (group, count))
    lines += ['TOTAL: %d quadros PASS' % sum(counts.values()),
              'Aprovacao significa igualdade de bytes nativos com o modelo offline.',
              'Nao comprova aceitação, persistencia ou efeitos no hardware.']
    output = '\n'.join(lines) + '\n'
    print(output, end='')
    if args.output:
        pathlib.Path(args.output).write_text(output, encoding='utf-8')


if __name__ == '__main__':
    main()
