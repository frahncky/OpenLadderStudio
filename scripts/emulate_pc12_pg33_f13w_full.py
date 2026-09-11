#!/usr/bin/env python3
"""Emula OFFLINE a coleta completa de F-13w ADD no caminho PG33 do PC12.

O ensaio começa em 0x004B758D, exatamente antes da chamada ao parser
0x004BA69E. Um registro interno sintético equivalente a:

    F-13w ADD D0002,D0001,00010

é colocado no buffer de programa do PC12. O código original deve:
- identificar o internal=513;
- gerar a primeira palavra 0D 77 / EXT 00;
- usar 0x004BCA65 para D0002, D0001 e 00010;
- terminar com StepSpan=4 e uma instrução lógica coletada.

Somente wrappers genéricos de CRT/UI são interceptados. Nenhuma COM, rotina TX,
API de serial ou PLC é acessada. A emulação para antes da montagem/envio do
quadro e antes da decisão de continuar para a próxima instrução.
"""

import argparse
import re
import struct
import sys

try:
    from unicorn import Uc, UcError, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE, UC_HOOK_MEM_INVALID
    from unicorn.x86_const import (
        UC_X86_REG_EAX, UC_X86_REG_EBX, UC_X86_REG_EDI, UC_X86_REG_EIP,
        UC_X86_REG_ESI, UC_X86_REG_ESP,
    )
except ImportError:
    print('ERRO: unicorn não instalado. Use: pip install unicorn==2.1.4', file=sys.stderr)
    raise

from emulate_pc12_pg33_builder import load_pe, map_image
from emulate_pc12_pg33_operand_helper import (
    get32, read_c_string, return_from_cdecl, emulate_strcpy, emulate_sprintf,
)

EXE_DEFAULT = 'src/OpenLadderStudio.Desktop/pc12.exe'
ENTRY = 0x004B758D
STOP = 0x004B7869
PC12_ATOI = 0x004CC126
PC12_STRCPY = 0x004CC174
PC12_SPRINTF = 0x004CBF90
PROGRESS_HELPER = 0x004CBD48
TX_BUF = 0x004FA7A8

PROGRAM_RECORD = 0x0053034B
OBJ = 0x2C000000
STACK = 0x2D000000
DUMMY_ESI = 0x2E000000

EXPECTED_HL = bytes.fromhex('0D 77 F0 01 F0 00 80 0A')
EXPECTED_EXT = bytes.fromhex('00 00 00 00')


def put32(mu, addr, value):
    mu.mem_write(addr, struct.pack('<I', value & 0xFFFFFFFF))


def write_slot(mu, addr, text):
    raw = text.encode('ascii') + b'\x00'
    if len(raw) > 12:
        raise ValueError('slot PC12 excede 12 bytes: %r' % text)
    mu.mem_write(addr, raw + (b'\x00' * (12 - len(raw))))


def emulate_atoi(mu):
    sp = mu.reg_read(UC_X86_REG_ESP)
    src = get32(mu, sp + 4)
    raw = read_c_string(mu, src)
    text = raw.decode('ascii', errors='strict').strip()
    try:
        value = int(text, 10)
    except ValueError:
        value = 0
    return_from_cdecl(mu, value)
    return text, value


def emulate(exe):
    data, image_base, sections = load_pe(exe)
    mu = Uc(UC_ARCH_X86, UC_MODE_32)
    map_image(mu, data, image_base, sections)
    mu.mem_map(OBJ, 0x20000)
    mu.mem_map(STACK - 0x10000, 0x20000)
    mu.mem_map(DUMMY_ESI, 0x1000)

    # Registro interno de 48 bytes = 4 slots de 12 bytes.
    write_slot(mu, PROGRAM_RECORD + 0, '513')
    write_slot(mu, PROGRAM_RECORD + 12, 'D0002')
    write_slot(mu, PROGRAM_RECORD + 24, 'D0001')
    write_slot(mu, PROGRAM_RECORD + 36, '00010')

    # Estado mínimo do coletor PG33.
    put32(mu, OBJ + 0x56, 0)
    put32(mu, OBJ + 0x5E, 6)
    put32(mu, OBJ + 0x62, 0)
    put32(mu, OBJ + 0x76, 0)   # cursor de passos
    put32(mu, OBJ + 0x7A, 0)   # contador de instruções
    mu.mem_write(OBJ + 0xD7, b'\x01')
    mu.mem_write(OBJ + 0xD8, b'\x01')

    mu.mem_write(DUMMY_ESI, struct.pack('<I', 0))
    mu.reg_write(UC_X86_REG_EDI, OBJ)
    mu.reg_write(UC_X86_REG_ESI, DUMMY_ESI)
    mu.reg_write(UC_X86_REG_EBX, 0)
    mu.reg_write(UC_X86_REG_ESP, STACK)

    state = {
        'atoi': [],
        'strcpy': 0,
        'sprintf': [],
        'progress': 0,
        'invalid': None,
        'steps': 0,
    }

    def hook(uc, address, size, _user):
        state['steps'] += 1
        if address == PC12_ATOI:
            text, value = emulate_atoi(uc)
            state['atoi'].append((text, value))
            return
        if address == PC12_STRCPY:
            emulate_strcpy(uc)
            state['strcpy'] += 1
            return
        if address == PC12_SPRINTF:
            fmt, out = emulate_sprintf(uc)
            state['sprintf'].append((fmt, out))
            return
        if address == PROGRESS_HELPER:
            state['progress'] += 1
            return_from_cdecl(uc, 0)
            return

    def invalid_hook(uc, access, address, size, value, _user):
        state['invalid'] = (access, address, size, value, uc.reg_read(UC_X86_REG_EIP))
        return False

    mu.hook_add(UC_HOOK_CODE, hook)
    mu.hook_add(UC_HOOK_MEM_INVALID, invalid_hook)

    try:
        mu.emu_start(ENTRY, STOP, count=1000000)
    except UcError as exc:
        if state['invalid']:
            access, address, size, value, eip = state['invalid']
            raise RuntimeError(
                'Unicorn: %s; invalid access=%d addr=0x%08X size=%d value=0x%X EIP=0x%08X' %
                (exc, access, address, size, value, eip))
        raise RuntimeError('Unicorn: %s EIP=0x%08X' % (exc, mu.reg_read(UC_X86_REG_EIP)))

    if mu.reg_read(UC_X86_REG_EIP) != STOP:
        raise RuntimeError('não alcançou STOP 0x%08X; EIP=0x%08X' %
                           (STOP, mu.reg_read(UC_X86_REG_EIP)))

    hl_bytes = get32(mu, OBJ + 0x56)
    tx_cursor = get32(mu, OBJ + 0x5E)
    ext_cursor = get32(mu, OBJ + 0x62)
    span = get32(mu, OBJ + 0x176)
    cursor = get32(mu, OBJ + 0x76)
    instruction_count = get32(mu, OBJ + 0x7A)
    mode = get32(mu, OBJ + 0x86)
    high_low = bytes(mu.mem_read(TX_BUF + 6, hl_bytes))
    external = bytes(mu.mem_read(OBJ + 0xE0, ext_cursor))

    ok = (
        high_low == EXPECTED_HL
        and external == EXPECTED_EXT
        and hl_bytes == 8
        and tx_cursor == 14
        and ext_cursor == 4
        and span == 4
        and cursor == 4
        and instruction_count == 1
        and mode == 2
        and ('513', 513) in state['atoi']
    )

    return {
        'ok': ok,
        'high_low': high_low,
        'external': external,
        'hl_bytes': hl_bytes,
        'tx_cursor': tx_cursor,
        'ext_cursor': ext_cursor,
        'span': span,
        'cursor': cursor,
        'instruction_count': instruction_count,
        'mode': mode,
        'state': state,
    }


def hx(data):
    return ' '.join('%02X' % b for b in data)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe', nargs='?', default=EXE_DEFAULT)
    ap.add_argument('-o', '--output')
    args = ap.parse_args()

    try:
        row = emulate(args.exe)
        lines = []
        lines.append('PC12 PG33 F-13w FULL COLLECTOR - UNICORN OFFLINE EMULATION')
        lines.append('=' * 100)
        lines.append('program=F-13w ADD D0002,D0001,00010')
        lines.append('entry=0x%08X stop=0x%08X mode=OFFLINE; no COM, no TX routine, no PLC' %
                     (ENTRY, STOP))
        lines.append('')
        lines.append('%s HIGH/LOW=[%s]' % ('OK' if row['ok'] else 'DIVERGE', hx(row['high_low'])))
        lines.append('expected       =[%s]' % hx(EXPECTED_HL))
        lines.append('EXTERNAL=[%s] expected=[%s]' % (hx(row['external']), hx(EXPECTED_EXT)))
        lines.append('counters HL=%d TX=%d EXT=%d StepSpan=%d cursor=%d instructions=%d mode=%d' %
                     (row['hl_bytes'], row['tx_cursor'], row['ext_cursor'], row['span'],
                      row['cursor'], row['instruction_count'], row['mode']))
        lines.append('atoi=' + repr(row['state']['atoi']))
        lines.append('CRT strcpy=%d sprintf=%d progress_hooks=%d machine_instructions=%d' %
                     (row['state']['strcpy'], len(row['state']['sprintf']),
                      row['state']['progress'], row['state']['steps']))
        lines.append('')
        lines.append('RESULT=' + ('PASS' if row['ok'] else 'FAIL'))
        if row['ok']:
            lines.append('O coletor original do PC12 expandiu F-13w em exatamente 4 palavras:')
            lines.append('0D 77 | F0 01 | F0 00 | 80 0A, todas com EXTERNAL=00.')
            lines.append('Isso coincide byte a byte com a leitura física PG34 do mesmo ADD.')
        lines.append('Nenhum byte foi transmitido fora do Unicorn.')
        rc = 0 if row['ok'] else 1
    except Exception as exc:
        lines = [
            'PC12 PG33 F-13w FULL COLLECTOR - UNICORN OFFLINE EMULATION',
            '=' * 100,
            'RESULT=FAIL',
            'error=' + str(exc),
            'Nenhum byte foi transmitido fora do Unicorn.',
        ]
        rc = 1

    report = '\n'.join(lines) + '\n'
    if args.output:
        import pathlib
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')
    return rc


if __name__ == '__main__':
    raise SystemExit(main())
