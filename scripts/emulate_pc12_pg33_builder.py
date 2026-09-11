#!/usr/bin/env python3
"""Executa SOMENTE o construtor 0x33 do PC12 dentro do Unicorn.

Objetivo
--------
Validar dinamicamente, mas 100% offline, a reconstrução do quadro montado por
0x004B7958 no caminho "Write PLC Program".

A unidade do corpo é PALAVRA DE MÁQUINA (HIGH/LOW/EXTERNAL). A análise do
helper 0x004BCA65 mostrou que até 20 instruções lógicas podem expandir para
até 80 palavras (StepSpan 1..4). Portanto este emulador valida W=1..80.

O script:
- lê pc12.exe como dados;
- mapeia a imagem dentro do Unicorn;
- injeta um objeto sintético EDI e planos HIGH/LOW + EXTERNAL;
- intercepta o relógio interno e a rotina TX;
- captura o buffer que o PC12 tentaria transmitir;
- PARA antes de qualquer I/O real.

Não existe SerialPort, COM, socket, WriteFile do host ou acesso ao PLC.
"""

import argparse
import pathlib
import struct
import sys

try:
    from unicorn import Uc, UcError, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE
    from unicorn.x86_const import (
        UC_X86_REG_EAX,
        UC_X86_REG_EDI,
        UC_X86_REG_EIP,
        UC_X86_REG_ESP,
    )
except ImportError:
    print('ERRO: unicorn não instalado. Use: pip install unicorn==2.1.4', file=sys.stderr)
    raise

EXE_DEFAULT = 'src/OpenLadderStudio.Desktop/pc12.exe'
BUILDER = 0x004B7958
CLOCK_HELPER = 0x004CBE48
TX_ROUTINE = 0x0046F5E6
TX_BUF = 0x004FA7A8
TX_LEN = 0x004FA8AC

OBJ = 0x21000000
STACK = 0x22000000
STOP = 0x23000000

MAX_MACHINE_WORDS = 80


def u16(data, off):
    return struct.unpack_from('<H', data, off)[0]


def u32(data, off):
    return struct.unpack_from('<I', data, off)[0]


def load_pe(path):
    data = pathlib.Path(path).read_bytes()
    pe = u32(data, 0x3C)
    if data[pe:pe + 4] != b'PE\0\0':
        raise ValueError('PE inválido')
    nsec = u16(data, pe + 6)
    opt_size = u16(data, pe + 20)
    opt = pe + 24
    image_base = u32(data, opt + 28)
    sec = opt + opt_size
    sections = []
    for i in range(nsec):
        o = sec + i * 40
        vsize = u32(data, o + 8)
        rva = u32(data, o + 12)
        raw_size = u32(data, o + 16)
        raw_ptr = u32(data, o + 20)
        sections.append((rva, vsize, raw_ptr, raw_size))
    return data, image_base, sections


def map_image(mu, data, image_base, sections):
    high = image_base
    for rva, vsize, _raw_ptr, raw_size in sections:
        high = max(high, image_base + rva + max(vsize, raw_size))
    map_size = ((high - image_base + 0xFFFFF) // 0x100000) * 0x100000
    mu.mem_map(image_base, map_size)
    for rva, _vsize, raw_ptr, raw_size in sections:
        if raw_size:
            mu.mem_write(image_base + rva, data[raw_ptr:raw_ptr + raw_size])


def checksum(prefix):
    return (0xFF - (sum(prefix) & 0xFF)) & 0xFF


def model_frame(start_step, high_low, external):
    w = len(external)
    body = list(high_low) + list(external)
    frame = [
        0x33,
        3 * w + 4,
        0x00,
        (start_step >> 8) & 0xFF,
        start_step & 0xFF,
        2 * w,
    ] + body
    frame.append(checksum(frame))
    return bytes(frame)


def emulate_builder(exe, start_step, high_low, external):
    if len(high_low) % 2:
        raise ValueError('HIGH/LOW deve ter quantidade par de bytes')
    w = len(high_low) // 2
    if w != len(external):
        raise ValueError('EXTERNAL deve ter um byte por palavra de máquina')
    if not (1 <= w <= MAX_MACHINE_WORDS):
        raise ValueError('W deve estar entre 1 e 80 palavras de máquina')
    if not (0 <= start_step < 4000):
        raise ValueError('start_step fora de 0..3999')

    data, image_base, sections = load_pe(exe)
    mu = Uc(UC_ARCH_X86, UC_MODE_32)
    map_image(mu, data, image_base, sections)
    mu.mem_map(OBJ, 0x10000)
    mu.mem_map(STACK - 0x10000, 0x20000)
    mu.mem_map(STOP, 0x1000)

    # Estado como o construtor espera depois da fase de coleta/expansão.
    mu.mem_write(OBJ + 0x56, struct.pack('<I', 2 * w))
    mu.mem_write(OBJ + 0x5E, struct.pack('<I', 6 + 2 * w))
    mu.mem_write(OBJ + 0x62, struct.pack('<I', w))
    mu.mem_write(OBJ + 0x6A, struct.pack('<I', (start_step >> 8) & 0xFF))
    mu.mem_write(OBJ + 0x6E, struct.pack('<I', start_step & 0xFF))
    mu.mem_write(OBJ + 0xE0, bytes(external))

    # A fase de coleta anterior ao builder já deixa HIGH/LOW em TX[6..].
    mu.mem_write(TX_BUF + 6, bytes(high_low))

    esp = STACK
    mu.mem_write(esp, struct.pack('<I', STOP))
    mu.reg_write(UC_X86_REG_ESP, esp)
    mu.reg_write(UC_X86_REG_EDI, OBJ)

    captured = {'frame': None, 'clock_calls': 0}

    def return_from_call(uc, eax=None):
        sp = uc.reg_read(UC_X86_REG_ESP)
        ret = struct.unpack('<I', bytes(uc.mem_read(sp, 4)))[0]
        uc.reg_write(UC_X86_REG_ESP, sp + 4)
        if eax is not None:
            uc.reg_write(UC_X86_REG_EAX, eax)
        uc.reg_write(UC_X86_REG_EIP, ret)

    def code_hook(uc, address, size, _user):
        if address == CLOCK_HELPER:
            captured['clock_calls'] += 1
            return_from_call(uc, 0x00100000)
            return
        if address == TX_ROUTINE:
            length = struct.unpack('<I', bytes(uc.mem_read(TX_LEN, 4)))[0]
            if length > 1024:
                raise RuntimeError('TX_LEN inesperado: %d' % length)
            captured['frame'] = bytes(uc.mem_read(TX_BUF, length))
            uc.emu_stop()
            return
        if address == STOP:
            uc.emu_stop()

    mu.hook_add(UC_HOOK_CODE, code_hook)

    try:
        mu.emu_start(BUILDER, STOP, count=500000)
    except UcError as exc:
        raise RuntimeError('Unicorn falhou: %s' % exc)

    if captured['frame'] is None:
        raise RuntimeError('rotina TX não foi alcançada')
    return captured['frame'], captured['clock_calls']


def hx(data):
    return ' '.join('%02X' % b for b in data)


def run_fixture(exe, name, start_step, high_low, external):
    real, clock_calls = emulate_builder(exe, start_step, high_low, external)
    expected = model_frame(start_step, high_low, external)
    ok = real == expected
    return {
        'name': name,
        'start': start_step,
        'w': len(external),
        'real': real,
        'expected': expected,
        'clock_calls': clock_calls,
        'ok': ok,
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe', nargs='?', default=EXE_DEFAULT)
    ap.add_argument('-o', '--output')
    args = ap.parse_args()

    fixtures = [
        ('one-boolean-word', 0x0000,
         bytes([0x00, 0x10]),
         bytes([0x01])),
        ('two-machine-words', 0x0050,
         bytes([0x00, 0x10, 0x20, 0x41]),
         bytes([0x01, 0x07])),
        ('address-0E34-three-words', 0x0E34,
         bytes([0x02, 0x11, 0x21, 0x40, 0x00, 0x39]),
         bytes([0x04, 0x07, 0x0C])),
        # Pior caso estrutural: 20 instruções lógicas de StepSpan=4 -> W=80.
        ('max-80-machine-words', 0x0F00,
         bytes((i * 7 + 3) & 0xFF for i in range(160)),
         bytes((i * 5 + 1) & 0xFF for i in range(80))),
    ]

    rows = []
    overall = True
    for fx in fixtures:
        row = run_fixture(args.exe, *fx)
        rows.append(row)
        overall &= row['ok']

    lines = []
    lines.append('PC12 PG33 BUILDER - UNICORN OFFLINE EMULATION')
    lines.append('=' * 92)
    lines.append('entry=0x%08X tx_hook=0x%08X' % (BUILDER, TX_ROUTINE))
    lines.append('mode=OFFLINE; rotina TX interceptada antes de qualquer I/O')
    lines.append('')
    for row in rows:
        lines.append('%s: %s  start=0x%04X machine_words=%d clock_calls=%d' %
                     ('OK' if row['ok'] else 'FALHA', row['name'], row['start'],
                      row['w'], row['clock_calls']))
        if row['w'] <= 3:
            lines.append('  PC12 : ' + hx(row['real']))
            lines.append('  model: ' + hx(row['expected']))
        else:
            lines.append('  PC12/model bytes=%d LEN=%02X HL=%02X checksum=%02X' %
                         (len(row['real']), row['real'][1], row['real'][5], row['real'][-1]))
    lines.append('')
    lines.append('RESULT=' + ('PASS' if overall else 'FAIL'))
    lines.append('W=80 valida dinamicamente offline o construtor máximo: LEN=F4, HIGH/LOW=A0, 247 bytes.')
    lines.append('Nenhum byte foi transmitido fora do Unicorn.')

    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')

    return 0 if overall else 1


if __name__ == '__main__':
    raise SystemExit(main())
