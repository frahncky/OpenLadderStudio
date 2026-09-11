#!/usr/bin/env python3
"""Emula OFFLINE a coleta completa de F-23 SET no caminho PG33 do PC12.

Programa sintético:
    F-23 SET Y0001

A emulação usa o parser e o helper originais do PC12 e para antes da montagem/
envio do quadro. Wrappers genéricos de CRT/UI são interceptados localmente.
Nenhuma COM, rotina TX, API serial ou PLC é acessada.
"""

import argparse
import struct
import sys

try:
    from unicorn import Uc, UcError, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE, UC_HOOK_MEM_INVALID
    from unicorn.x86_const import UC_X86_REG_EBX, UC_X86_REG_EDI, UC_X86_REG_EIP, UC_X86_REG_ESI, UC_X86_REG_ESP
except ImportError:
    print('ERRO: unicorn não instalado. Use: pip install unicorn==2.1.4', file=sys.stderr)
    raise

from emulate_pc12_pg33_builder import load_pe, map_image
from emulate_pc12_pg33_operand_helper import get32, read_c_string, return_from_cdecl, emulate_strcpy, emulate_sprintf

EXE_DEFAULT = 'src/OpenLadderStudio.Desktop/pc12.exe'
ENTRY = 0x004B758D
STOP = 0x004B7869
PC12_ATOI = 0x004CC126
PC12_STRCPY = 0x004CC174
PC12_SPRINTF = 0x004CBF90
PROGRESS_HELPER = 0x004CBD48
TX_BUF = 0x004FA7A8
PROGRAM_SIZE = 0x00560368
LAST_PROGRAM_STEP = 0x0052CD3C
PROGRAM_RECORD = 0x0053034B

OBJ = 0x30000000
STACK = 0x31000000
DUMMY_ESI = 0x32000000

EXPECTED_HL = bytes.fromhex('17 71 C8 80')


def put32(mu, addr, value):
    mu.mem_write(addr, struct.pack('<I', value & 0xFFFFFFFF))


def write_slot(mu, addr, text):
    raw = text.encode('ascii') + b'\x00'
    if len(raw) > 12:
        raise ValueError('slot PC12 excede 12 bytes: %r' % text)
    mu.mem_write(addr, raw + b'\x00' * (12 - len(raw)))


def emulate_atoi(mu):
    sp = mu.reg_read(UC_X86_REG_ESP)
    src = get32(mu, sp + 4)
    text = read_c_string(mu, src).decode('ascii', errors='strict').strip()
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

    write_slot(mu, PROGRAM_RECORD + 0, '23')
    write_slot(mu, PROGRAM_RECORD + 12, 'Y0001')
    write_slot(mu, PROGRAM_RECORD + 24, '')
    write_slot(mu, PROGRAM_RECORD + 36, '')

    put32(mu, OBJ + 0x56, 0)
    put32(mu, OBJ + 0x5E, 6)
    put32(mu, OBJ + 0x62, 0)
    put32(mu, OBJ + 0x76, 0)
    put32(mu, OBJ + 0x7A, 0)
    mu.mem_write(OBJ + 0xD7, b'\x01')
    mu.mem_write(OBJ + 0xD8, b'\x01')
    put32(mu, PROGRAM_SIZE, 4000)
    put32(mu, LAST_PROGRAM_STEP, 3999)

    mu.mem_write(DUMMY_ESI, struct.pack('<I', 0))
    mu.reg_write(UC_X86_REG_EDI, OBJ)
    mu.reg_write(UC_X86_REG_ESI, DUMMY_ESI)
    mu.reg_write(UC_X86_REG_EBX, 0)
    mu.reg_write(UC_X86_REG_ESP, STACK)

    state = {'atoi': [], 'strcpy': 0, 'sprintf': [], 'progress': 0, 'invalid': None, 'steps': 0}

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
            raise RuntimeError('Unicorn: %s; invalid access=%d addr=0x%08X size=%d value=0x%X EIP=0x%08X' %
                               (exc, access, address, size, value, eip))
        raise

    if mu.reg_read(UC_X86_REG_EIP) != STOP:
        raise RuntimeError('não alcançou STOP')

    hl_bytes = get32(mu, OBJ + 0x56)
    tx_cursor = get32(mu, OBJ + 0x5E)
    ext_cursor = get32(mu, OBJ + 0x62)
    span = get32(mu, OBJ + 0x176)
    cursor = get32(mu, OBJ + 0x76)
    instruction_count = get32(mu, OBJ + 0x7A)
    high_low = bytes(mu.mem_read(TX_BUF + 6, hl_bytes))
    external = bytes(mu.mem_read(OBJ + 0xE0, ext_cursor))

    ok = (high_low == EXPECTED_HL and hl_bytes == 4 and tx_cursor == 10 and ext_cursor == 2
          and span == 2 and cursor == 2 and instruction_count == 1 and ('23', 23) in state['atoi'])

    return dict(ok=ok, high_low=high_low, external=external, hl_bytes=hl_bytes,
                tx_cursor=tx_cursor, ext_cursor=ext_cursor, span=span, cursor=cursor,
                instruction_count=instruction_count, state=state)


def hx(data):
    return ' '.join('%02X' % b for b in data)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe', nargs='?', default=EXE_DEFAULT)
    ap.add_argument('-o', '--output')
    args = ap.parse_args()
    try:
        row = emulate(args.exe)
        lines = [
            'PC12 PG33 F-23 SET FULL COLLECTOR - UNICORN OFFLINE EMULATION',
            '=' * 100,
            'program=F-23 SET Y0001',
            'mode=OFFLINE; no COM, no TX routine, no PLC',
            '',
            ('OK' if row['ok'] else 'DIVERGE') + ' HIGH/LOW=[' + hx(row['high_low']) + ']',
            'expected       =[' + hx(EXPECTED_HL) + ']',
            'EXTERNAL=[' + hx(row['external']) + ']',
            'counters HL=%d TX=%d EXT=%d StepSpan=%d cursor=%d instructions=%d' %
            (row['hl_bytes'], row['tx_cursor'], row['ext_cursor'], row['span'], row['cursor'], row['instruction_count']),
            'atoi=' + repr(row['state']['atoi']),
            '',
            'RESULT=' + ('PASS' if row['ok'] else 'FAIL'),
        ]
        if row['ok']:
            lines += [
                'O coletor original do PC12 expandiu F-23 SET Y0001 em 17 71 | C8 80.',
                'Os pares HIGH/LOW coincidem byte a byte com a leitura física PG34.',
            ]
        lines.append('Nenhum byte foi transmitido fora do Unicorn.')
        rc = 0 if row['ok'] else 1
    except Exception as exc:
        lines = ['PC12 PG33 F-23 SET FULL COLLECTOR - UNICORN OFFLINE EMULATION', '=' * 100,
                 'RESULT=FAIL', 'error=' + str(exc), 'Nenhum byte foi transmitido fora do Unicorn.']
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
