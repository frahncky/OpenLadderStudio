#!/usr/bin/env python3
"""Identifica e testa PG35 (SET/RESET de bit) e PG09 (escrita de registrador).

Executa as instrucoes originais dos handlers PC12 em Unicorn e intercepta
somente funcoes CRT de strings/conversao, relogio e a entrada de TX. A rotina
TX NUNCA executa. Nao simula respostas de PLC nem abre serial/rede.

Requer: unicorn==2.1.4, pefile==2024.8.26.
Uso: python3 scripts/emulate_pc12_monitor_commands.py [--exhaustive] [-o ARQUIVO]
"""

import argparse
import hashlib
import pathlib
import re
import struct

import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE
from unicorn.x86_const import UC_X86_REG_EAX, UC_X86_REG_EIP, UC_X86_REG_ESP

from emulate_pc12_pg33_builder import load_pe, map_image

SHA256 = '05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0'
EXE = 'src/OpenLadderStudio.Desktop/pc12.exe'
BIT_HANDLER, WORD_HANDLER = 0x4C2141, 0x4C240F
BIT_ENCODER, NIBBLE_HELPER = 0x4C27C7, 0x4BDE04
TX, TX_BUF, TX_LEN = 0x46F5E6, 0x4FA7A8, 0x4FA8AC
ADDRESS_TEXT, VALUE_TEXT = 0x52CDBA, 0x52CDC0
OBJ, STACK, STOP = 0x21000000, 0x22000000, 0x23000000

# Nomes conferidos na IAT do binario, nao inferidos pela assinatura do caller.
IMPORTS = {
    0x4CC174: b'_strcpy',
    0x4CC180: b'_strncat',
    0x4CC126: b'_atol',
    0x4CBEA8: b'lstrcmpA',
    0x4CBF90: b'wsprintfA',
    0x4CBE48: b'GetTickCount',
}


def cstring(mu, address):
    out = bytearray()
    for i in range(256):
        value = bytes(mu.mem_read(address + i, 1))[0]
        if value == 0:
            return bytes(out)
        out.append(value)
    raise ValueError('string sem terminador em 256 bytes')


def put32(mu, address, value):
    mu.mem_write(address, struct.pack('<I', value & 0xFFFFFFFF))


def get32(mu, address):
    return struct.unpack('<I', mu.mem_read(address, 4))[0]


def frame(prefix):
    return bytes(prefix + [(0xFF - sum(prefix)) & 0xFF])


def bit_model(area, number, value):
    if area not in ('X', 'Y', 'C') or not 1 <= number <= (2048 if area == 'C' else 384):
        raise ValueError('endereco fora da faixa exposta pelo dialogo 41')
    if value not in (0, 1):
        raise ValueError('valor do bit deve ser 0 ou 1')
    byte_index, bit_index = divmod(number - 1, 8)
    bank = {'X': 0x50, 'Y': 0x10, 'C': 0x30}[area]
    return frame([0x35, 3, bank | (byte_index >> 8), byte_index & 255,
                  bit_index | (value << 7)])


def word_model(area, number, value):
    limits = {'V': 1024, 'D': 2048, 'WC': 912}
    if area not in limits or not 1 <= number <= limits[area]:
        raise ValueError('endereco fora da faixa checada pelo handler do dialogo 43')
    if not 0 <= value <= 65535:
        raise ValueError('valor deve estar entre 0 e 65535')
    offset = number - 1
    bank = {'V': 0x50, 'D': 0x90, 'WC': 0x70}[area]
    return frame([9, 5, bank | (offset >> 8), offset & 255, 2,
                  value >> 8, value & 255])


class MonitorEmulator:
    def __init__(self, exe):
        self.image = load_pe(exe)
        data = self.image[0]
        if hashlib.sha256(data).hexdigest() != SHA256:
            raise ValueError('SHA256 diferente: enderecos fixos nao sao seguros para este binario')
        self.pe = pefile.PE(data=data)
        names = {i.address: i.name for d in self.pe.DIRECTORY_ENTRY_IMPORT for i in d.imports}
        for thunk, expected_name in IMPORTS.items():
            code = self.pe.get_data(thunk - self.pe.OPTIONAL_HEADER.ImageBase, 6)
            if code[:2] != b'\xff\x25':
                raise ValueError('thunk nao reconhecido: %08X' % thunk)
            iat = struct.unpack_from('<I', code, 2)[0]
            if names.get(iat) != expected_name:
                raise ValueError('import inesperado: %08X' % thunk)

    def run(self, address_text, value, word=False, state=None, message=0x22C):
        data, base, sections = self.image
        mu = Uc(UC_ARCH_X86, UC_MODE_32)
        map_image(mu, data, base, sections)
        mu.mem_map(OBJ, 0x10000)
        mu.mem_map(STACK - 0x10000, 0x20000)
        mu.mem_map(STOP, 0x1000)
        mu.mem_write(ADDRESS_TEXT, address_text.encode('ascii') + b'\0')
        mu.mem_write(VALUE_TEXT, str(value).encode('ascii') + b'\0')
        actual_state = (2 if word else 1) if state is None else state
        mu.mem_write(STACK, struct.pack('<IIII', STOP, OBJ, actual_state, message))
        mu.reg_write(UC_X86_REG_ESP, STACK)
        result = {'frame': None, 'stopped': False, 'native_encoder': False,
                  'native_nibble_calls': 0, 'stub_calls': 0}

        def ret(eax=0, stdcall_bytes=0):
            sp = mu.reg_read(UC_X86_REG_ESP)
            target = get32(mu, sp)
            mu.reg_write(UC_X86_REG_ESP, sp + 4 + stdcall_bytes)
            mu.reg_write(UC_X86_REG_EAX, eax & 0xFFFFFFFF)
            mu.reg_write(UC_X86_REG_EIP, target)

        def on_code(uc, pc, size, _):
            if pc == TX:
                length = get32(uc, TX_LEN)
                if length not in (6, 8):
                    raise ValueError('comprimento TX inesperado: %d' % length)
                result['frame'] = bytes(uc.mem_read(TX_BUF, length))
                result['stopped'] = True
                uc.emu_stop()
                return
            if pc == STOP:
                result['stopped'] = True
                uc.emu_stop()
                return
            if pc == BIT_ENCODER:
                result['native_encoder'] = True
            if pc == NIBBLE_HELPER:
                result['native_nibble_calls'] += 1
            if pc in IMPORTS:
                result['stub_calls'] += 1
                sp = uc.reg_read(UC_X86_REG_ESP)
                arg = lambda n: get32(uc, sp + 4 + n * 4)
                name = IMPORTS[pc]
                if name == b'_strcpy':
                    dst, src = arg(0), arg(1)
                    uc.mem_write(dst, cstring(uc, src) + b'\0')
                    ret(dst)
                elif name == b'_strncat':
                    dst, src, count = arg(0), arg(1), arg(2)
                    if count > 32:
                        raise ValueError('strncat inesperado')
                    uc.mem_write(dst, cstring(uc, dst) + cstring(uc, src)[:count] + b'\0')
                    ret(dst)
                elif name == b'_atol':
                    match = re.match(rb'\s*([+-]?\d+)', cstring(uc, arg(0)))
                    ret(int(match.group(1)) if match else 0)
                elif name == b'lstrcmpA':
                    a, b = cstring(uc, arg(0)), cstring(uc, arg(1))
                    ret((a > b) - (a < b), 8)
                elif name == b'wsprintfA':
                    if cstring(uc, arg(1)) != b'%04X':
                        raise ValueError('formato wsprintfA nao esperado')
                    out = ('%04X' % arg(2)).encode('ascii')
                    uc.mem_write(arg(0), out + b'\0')
                    ret(len(out))
                elif name == b'GetTickCount':
                    ret(1000)
                return
            if not (BIT_HANDLER <= pc < 0x4C2A14 or NIBBLE_HELPER <= pc < 0x4BDFB2):
                raise ValueError('execucao fora da allowlist: %08X' % pc)

        mu.hook_add(UC_HOOK_CODE, on_code)
        mu.emu_start(WORD_HANDLER if word else BIT_HANDLER, STOP + 1, count=20000)
        if not result['stopped']:
            raise RuntimeError('limite de instrucoes atingido antes de TX/retorno')
        return result


def dialog_string(data, pos):
    value = struct.unpack_from('<H', data, pos)[0]
    pos += 2
    if value == 0xFFFF:
        return ('ordinal', struct.unpack_from('<H', data, pos)[0]), pos + 2
    out = []
    while value:
        out.append(chr(value))
        value = struct.unpack_from('<H', data, pos)[0]
        pos += 2
    return ''.join(out), pos


def selected_dialogs(pe, ids=(30, 31, 32, 41, 43)):
    result = {}
    for typ in pe.DIRECTORY_ENTRY_RESOURCE.entries:
        if typ.id != 5:
            continue
        for entry in typ.directory.entries:
            if entry.id not in ids:
                continue
            item = entry.directory.entries[0].data.struct
            data = pe.get_data(item.OffsetToData, item.Size)
            if data[2:4] == b'\xff\xff':
                raise ValueError('DLGTEMPLATEEX nao implementado')
            style, _ex, count, _x, _y, _cx, _cy = struct.unpack_from('<IIHhhhh', data)
            pos = 18
            _menu, pos = dialog_string(data, pos)
            _cls, pos = dialog_string(data, pos)
            title, pos = dialog_string(data, pos)
            if style & 0x40:
                _font, pos = dialog_string(data, pos + 2)
            controls = {}
            for _ in range(count):
                pos = (pos + 3) & ~3
                ident = struct.unpack_from('<H', data, pos + 16)[0]
                pos += 18
                _cls, pos = dialog_string(data, pos)
                label, pos = dialog_string(data, pos)
                extra = struct.unpack_from('<H', data, pos)[0]
                pos += 2 + extra
                controls[ident] = label
            result[entry.id] = (title, controls)
    return result


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--exe', default=EXE)
    ap.add_argument('--exhaustive', action='store_true', help='todos os bits X/Y/C expostos no dialogo 41')
    ap.add_argument('-o', '--output')
    args = ap.parse_args()
    emulator = MonitorEmulator(args.exe)
    lines = ['PC12 MONITOR COMMANDS - OFFLINE', 'SHA256=' + SHA256]
    dialogs = selected_dialogs(emulator.pe)
    assert dialogs[41][1][104] == 'SET(ON)'
    assert dialogs[41][1][105] == 'RESET(OFF)'
    for ident, (title, controls) in sorted(dialogs.items()):
        lines.append('DIALOG %d %r %r' % (ident, title, controls))
    tested = 0
    for area, limit in [('X', 384), ('Y', 384), ('C', 2048)]:
        numbers = range(1, limit + 1) if args.exhaustive else sorted(set(
            [1, 2, 7, 8, 9, 16, 17, 255, 256, 257, 258, 259, limit]))
        for number in numbers:
            for value in (0, 1):
                address = '%s%04d' % (area, number)
                got = emulator.run(address, value)
                expected = bit_model(area, number, value)
                assert got['frame'] == expected, (address, value, got, expected.hex())
                assert got['native_encoder'] and got['native_nibble_calls'] == 4
                assert sum(got['frame']) & 255 == 255
                tested += 1
                if not args.exhaustive or number in (1, 8, 9, limit):
                    lines.append('OK PG35 %s=%d -> %s' % (address, value, expected.hex(' ').upper()))
    for area, limit in [('V', 1024), ('D', 2048), ('WC', 912)]:
        for number in sorted(set(n for n in [1, 2, 119, 120, 255, 256, 257, limit] if n <= limit)):
            for value in (0, 1, 255, 256, 0x1234, 0x8000, 0xFFFF):
                address = ('%s%03d' if area == 'WC' else '%s%04d') % (area, number)
                got = emulator.run(address, value, word=True)
                expected = word_model(area, number, value)
                assert got['frame'] == expected, (address, value, got, expected.hex())
                assert got['native_nibble_calls'] == 8
                assert sum(got['frame']) & 255 == 255
                tested += 1
                lines.append('OK PG09 %s=%d -> %s' % (address, value, expected.hex(' ').upper()))
    for word in (False, True):
        for options in ({'state': 0}, {'message': 0x22D}):
            got = emulator.run('V0001' if word else 'C0001', 1, word=word, **options)
            assert got['frame'] is None
            tested += 1
            lines.append('OK GUARDA word=%s %r: nenhum TX' % (word, options))
    lines += ['RESULT=PASS cases=%d' % tested,
              'RT_MENU/RT_DIALOG e quadros nativos identificam a intencao do PC12.',
              'Sem resposta sintetica: nenhuma afirmacao de efeito/ACK do TP02.',
              'Nenhum byte transmitido, nenhuma alteracao no aplicativo.']
    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
