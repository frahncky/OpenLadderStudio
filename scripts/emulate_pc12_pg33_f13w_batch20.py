#!/usr/bin/env python3
"""Emula OFFLINE um bloco máximo de 20 F-13w no coletor + builder PG33 do PC12.

Cada instrução é:
    F-13w ADD D0002,D0001,00010

O código original do PC12 coleta 20 instruções lógicas de StepSpan=4, acumulando
80 palavras de máquina. Em seguida o construtor original 0x004B7958 é executado
com a rotina TX interceptada. O quadro capturado deve ter W=80, LEN=F4,
HIGH/LOW=A0 e 247 bytes.

Nenhuma COM, API serial ou comunicação com PLC é acessada.
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

from emulate_pc12_pg33_builder import load_pe, map_image, model_frame
from emulate_pc12_pg33_operand_helper import get32, read_c_string, return_from_cdecl, emulate_strcpy, emulate_sprintf

EXE_DEFAULT = 'src/OpenLadderStudio.Desktop/pc12.exe'
COLLECT_ENTRY = 0x004B758D
COLLECT_STOP = 0x004B7869
BUILDER = 0x004B7958
CLOCK_HELPER = 0x004CBE48
TX_ROUTINE = 0x0046F5E6
PC12_ATOI = 0x004CC126
PC12_STRCPY = 0x004CC174
PC12_SPRINTF = 0x004CBF90
PROGRESS_HELPER = 0x004CBD48
TX_BUF = 0x004FA7A8
TX_LEN = 0x004FA8AC
PROGRAM_SIZE = 0x00560368
LAST_PROGRAM_STEP = 0x0052CD3C
PROGRAM_BASE = 0x0053034B

OBJ = 0x33000000
STACK = 0x34000000
DUMMY_ESI = 0x35000000

WORD_HL = bytes.fromhex('0D 77 F0 01 F0 00 80 0A')
WORD_EXT = bytes.fromhex('00 00 00 00')


def put32(mu, addr, value):
    mu.mem_write(addr, struct.pack('<I', value & 0xFFFFFFFF))


def write_slot(mu, addr, text):
    raw = text.encode('ascii') + b'\x00'
    if len(raw) > 12:
        raise ValueError('slot excede 12 bytes')
    mu.mem_write(addr, raw + b'\x00' * (12 - len(raw)))


def seed_f13w_record(mu, step):
    base = PROGRAM_BASE + step * 48
    write_slot(mu, base + 0, '513')
    write_slot(mu, base + 12, 'D0002')
    write_slot(mu, base + 24, 'D0001')
    write_slot(mu, base + 36, '00010')


def emulate_atoi(mu):
    sp = mu.reg_read(UC_X86_REG_ESP)
    src = get32(mu, sp + 4)
    text = read_c_string(mu, src).decode('ascii', errors='strict').strip()
    try:
        value = int(text, 10)
    except ValueError:
        value = 0
    return_from_cdecl(mu, value)


def main_emulate(exe):
    data, image_base, sections = load_pe(exe)
    mu = Uc(UC_ARCH_X86, UC_MODE_32)
    map_image(mu, data, image_base, sections)
    mu.mem_map(OBJ, 0x20000)
    mu.mem_map(STACK - 0x10000, 0x20000)
    mu.mem_map(DUMMY_ESI, 0x1000)

    for i in range(20):
        seed_f13w_record(mu, i * 4)

    put32(mu, OBJ + 0x56, 0)
    put32(mu, OBJ + 0x5E, 6)
    put32(mu, OBJ + 0x62, 0)
    put32(mu, OBJ + 0x6A, 0)
    put32(mu, OBJ + 0x6E, 0)
    put32(mu, OBJ + 0x76, 0)
    put32(mu, OBJ + 0x7A, 0)
    put32(mu, OBJ + 0x7E, 0)
    mu.mem_write(OBJ + 0xD7, b'\x01')
    mu.mem_write(OBJ + 0xD8, b'\x01')
    put32(mu, PROGRAM_SIZE, 4000)
    put32(mu, LAST_PROGRAM_STEP, 3999)
    mu.mem_write(DUMMY_ESI, struct.pack('<I', 0))

    mu.reg_write(UC_X86_REG_EDI, OBJ)
    mu.reg_write(UC_X86_REG_ESI, DUMMY_ESI)
    mu.reg_write(UC_X86_REG_EBX, 0)

    state = {'tx': None, 'invalid': None, 'collect_steps': [], 'clock_calls': 0}

    def hook(uc, address, size, _user):
        if address == PC12_ATOI:
            emulate_atoi(uc)
            return
        if address == PC12_STRCPY:
            emulate_strcpy(uc)
            return
        if address == PC12_SPRINTF:
            emulate_sprintf(uc)
            return
        if address == PROGRESS_HELPER:
            return_from_cdecl(uc, 0)
            return
        if address == CLOCK_HELPER:
            state['clock_calls'] += 1
            return_from_cdecl(uc, 0x00100000)
            return
        if address == TX_ROUTINE:
            length = get32(uc, TX_LEN)
            state['tx'] = bytes(uc.mem_read(TX_BUF, length))
            uc.emu_stop()
            return

    def invalid_hook(uc, access, address, size, value, _user):
        state['invalid'] = (access, address, size, value, uc.reg_read(UC_X86_REG_EIP))
        return False

    mu.hook_add(UC_HOOK_CODE, hook)
    mu.hook_add(UC_HOOK_MEM_INVALID, invalid_hook)

    # Coleta cada instrução usando exatamente parser + helpers originais.
    for i in range(20):
        mu.reg_write(UC_X86_REG_ESP, STACK)
        try:
            mu.emu_start(COLLECT_ENTRY, COLLECT_STOP, count=1000000)
        except UcError as exc:
            raise RuntimeError('coleta %d falhou: %s invalid=%r' % (i, exc, state['invalid']))
        if mu.reg_read(UC_X86_REG_EIP) != COLLECT_STOP:
            raise RuntimeError('coleta %d não alcançou limite' % i)
        state['collect_steps'].append((get32(mu, OBJ + 0x76), get32(mu, OBJ + 0x7A), get32(mu, OBJ + 0x56)))

    hl_bytes = get32(mu, OBJ + 0x56)
    ext_count = get32(mu, OBJ + 0x62)
    cursor = get32(mu, OBJ + 0x76)
    instruction_count = get32(mu, OBJ + 0x7A)
    high_low = bytes(mu.mem_read(TX_BUF + 6, hl_bytes))
    external = bytes(mu.mem_read(OBJ + 0xE0, ext_count))

    expected_hl = WORD_HL * 20
    expected_ext = WORD_EXT * 20
    expected_frame = model_frame(0, expected_hl, expected_ext)

    # Executa o construtor original e intercepta a rotina TX antes de I/O.
    mu.reg_write(UC_X86_REG_ESP, STACK)
    try:
        mu.emu_start(BUILDER, 0, count=500000)
    except UcError as exc:
        if state['tx'] is None:
            raise RuntimeError('builder falhou: %s invalid=%r' % (exc, state['invalid']))

    if state['tx'] is None:
        raise RuntimeError('builder não alcançou TX interceptado')

    ok = (
        instruction_count == 20 and cursor == 80 and hl_bytes == 160 and ext_count == 80
        and high_low == expected_hl and external == expected_ext
        and state['tx'] == expected_frame and len(state['tx']) == 247
        and state['tx'][0] == 0x33 and state['tx'][1] == 0xF4 and state['tx'][5] == 0xA0
    )

    return dict(ok=ok, instruction_count=instruction_count, cursor=cursor, hl_bytes=hl_bytes,
                ext_count=ext_count, frame=state['tx'], expected_frame=expected_frame,
                collect_steps=state['collect_steps'], clock_calls=state['clock_calls'])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe', nargs='?', default=EXE_DEFAULT)
    ap.add_argument('-o', '--output')
    args = ap.parse_args()
    try:
        row = main_emulate(args.exe)
        f = row['frame']
        lines = [
            'PC12 PG33 BATCH20 F-13w - UNICORN OFFLINE EMULATION',
            '=' * 100,
            'program=20 x F-13w ADD D0002,D0001,00010',
            'mode=OFFLINE; TX routine intercepted before I/O; no COM/PLC',
            '',
            'logical_instructions=%d cursor=%d HL_bytes=%d EXT_bytes=%d' %
            (row['instruction_count'], row['cursor'], row['hl_bytes'], row['ext_count']),
            'frame bytes=%d CMD=%02X LEN=%02X start=%02X%02X HL_count=%02X checksum=%02X' %
            (len(f), f[0], f[1], f[3], f[4], f[5], f[-1]),
            'first instruction progress=%r' % (row['collect_steps'][0],),
            'last instruction progress=%r' % (row['collect_steps'][-1],),
            'clock_calls=%d' % row['clock_calls'],
            '',
            'RESULT=' + ('PASS' if row['ok'] else 'FAIL'),
        ]
        if row['ok']:
            lines += [
                'O coletor original acumulou 20 instruções lógicas de 4 passos = 80 palavras.',
                'O builder original produziu 33 F4 00 00 00 A0 ... com 247 bytes e checksum válido.',
            ]
        lines.append('Nenhum byte foi transmitido fora do Unicorn.')
        rc = 0 if row['ok'] else 1
    except Exception as exc:
        lines = ['PC12 PG33 BATCH20 F-13w - UNICORN OFFLINE EMULATION', '=' * 100,
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
