#!/usr/bin/env python3
"""Emula OFFLINE a divisão 20+1 do Write PLC Program para 21 F-13w.

Fluxo executado no código original do PC12:
1. coleta 20 F-13w (80 passos/palavras) e monta o primeiro 0x33;
2. intercepta TX antes de I/O;
3. executa a cauda original de sucesso em 0x004B7C50, que reinicia o bloco
   preservando cursor=80 e start=80;
4. coleta o 21º F-13w e monta o segundo 0x33 em start=0x0050.

Nenhuma COM, API serial ou comunicação com PLC é executada.
"""

import argparse
import struct
import sys

try:
    from unicorn import Uc, UcError, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE, UC_HOOK_MEM_INVALID
    from unicorn.x86_const import UC_X86_REG_EBP, UC_X86_REG_EBX, UC_X86_REG_EDI, UC_X86_REG_EIP, UC_X86_REG_ESI, UC_X86_REG_ESP
except ImportError:
    print('ERRO: unicorn não instalado. Use: pip install unicorn==2.1.4', file=sys.stderr)
    raise

from emulate_pc12_pg33_builder import load_pe, map_image, model_frame
from emulate_pc12_pg33_operand_helper import get32, read_c_string, return_from_cdecl, emulate_strcpy, emulate_sprintf

EXE_DEFAULT = 'src/OpenLadderStudio.Desktop/pc12.exe'
COLLECT_ENTRY = 0x004B758D
COLLECT_STOP = 0x004B7869
BUILDER = 0x004B7958
SUCCESS_TAIL = 0x004B7C50
NEXT_BLOCK = 0x004B7893
CLOCK_HELPER = 0x004CBE48
TX_ROUTINE = 0x0046F5E6
PC12_ATOI = 0x004CC126
PC12_STRCPY = 0x004CC174
PC12_SPRINTF = 0x004CBF90
PROGRESS_HELPER = 0x004CBD48
TX_BUF = 0x004FA7A8
TX_LEN = 0x004FA8AC
F_TIMEOUT = 0x004FA8B7
F_ERROR = 0x004FA8B8
F_CHECKSUM = 0x004FA8B9
PROGRAM_SIZE = 0x00560368
LAST_PROGRAM_STEP = 0x0052CD3C
OPTIONAL_TRACE_FLAG = 0x005703E6
PROGRAM_BASE = 0x0053034B

OBJ = 0x36000000
STACK = 0x37000000
DUMMY_ESI = 0x38000000

F13_HL = bytes.fromhex('0D 77 F0 01 F0 00 80 0A')
F13_EXT = bytes.fromhex('00 00 00 00')


def put32(mu, addr, value):
    mu.mem_write(addr, struct.pack('<I', value & 0xFFFFFFFF))


def write_slot(mu, addr, text):
    raw = text.encode('ascii') + b'\x00'
    if len(raw) > 12:
        raise ValueError('slot excede 12 bytes')
    mu.mem_write(addr, raw + b'\x00' * (12 - len(raw)))


def seed_record(mu, step):
    base = PROGRAM_BASE + step * 48
    write_slot(mu, base + 0, '513')
    write_slot(mu, base + 12, 'D0002')
    write_slot(mu, base + 24, 'D0001')
    write_slot(mu, base + 36, '00010')


def atoi_hook(mu):
    sp = mu.reg_read(UC_X86_REG_ESP)
    src = get32(mu, sp + 4)
    text = read_c_string(mu, src).decode('ascii', errors='strict').strip()
    try:
        value = int(text, 10)
    except ValueError:
        value = 0
    return_from_cdecl(mu, value)


def emulate(exe):
    data, image_base, sections = load_pe(exe)
    mu = Uc(UC_ARCH_X86, UC_MODE_32)
    map_image(mu, data, image_base, sections)
    mu.mem_map(OBJ, 0x20000)
    mu.mem_map(STACK - 0x10000, 0x20000)
    mu.mem_map(DUMMY_ESI, 0x1000)

    for i in range(21):
        seed_record(mu, i * 4)

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
    mu.mem_write(OPTIONAL_TRACE_FLAG, b'\x00')
    mu.mem_write(DUMMY_ESI, struct.pack('<I', 0))

    mu.reg_write(UC_X86_REG_EDI, OBJ)
    mu.reg_write(UC_X86_REG_ESI, DUMMY_ESI)
    mu.reg_write(UC_X86_REG_EBX, 0)

    state = {'tx_frames': [], 'stop_next': False, 'invalid': None}

    def hook(uc, address, size, _user):
        if address == PC12_ATOI:
            atoi_hook(uc); return
        if address == PC12_STRCPY:
            emulate_strcpy(uc); return
        if address == PC12_SPRINTF:
            emulate_sprintf(uc); return
        if address == PROGRESS_HELPER:
            return_from_cdecl(uc, 0); return
        if address == CLOCK_HELPER:
            return_from_cdecl(uc, 0x00100000); return
        if address == TX_ROUTINE:
            length = get32(uc, TX_LEN)
            state['tx_frames'].append(bytes(uc.mem_read(TX_BUF, length)))
            uc.emu_stop(); return
        if state['stop_next'] and address == NEXT_BLOCK:
            uc.emu_stop(); return

    def invalid_hook(uc, access, address, size, value, _user):
        state['invalid'] = (access, address, size, value, uc.reg_read(UC_X86_REG_EIP))
        return False

    mu.hook_add(UC_HOOK_CODE, hook)
    mu.hook_add(UC_HOOK_MEM_INVALID, invalid_hook)

    def collect_one():
        mu.reg_write(UC_X86_REG_ESP, STACK)
        mu.emu_start(COLLECT_ENTRY, COLLECT_STOP, count=1000000)
        if mu.reg_read(UC_X86_REG_EIP) != COLLECT_STOP:
            raise RuntimeError('coletor não alcançou boundary')

    def build_one(start_step):
        put32(mu, OBJ + 0x6A, (start_step >> 8) & 0xFF)
        put32(mu, OBJ + 0x6E, start_step & 0xFF)
        before = len(state['tx_frames'])
        mu.reg_write(UC_X86_REG_ESP, STACK)
        mu.emu_start(BUILDER, 0, count=500000)
        if len(state['tx_frames']) != before + 1:
            raise RuntimeError('builder não produziu frame interceptado')
        return state['tx_frames'][-1]

    # Primeiro bloco: 20 instruções x 4 passos.
    for _ in range(20):
        collect_one()
    if get32(mu, OBJ + 0x76) != 80 or get32(mu, OBJ + 0x7A) != 20:
        raise RuntimeError('estado inesperado após primeiro bloco')
    frame1 = build_one(0)

    # Simula apenas o resultado de comunicação bem-sucedida e executa a cauda
    # original do PC12 que prepara o próximo bloco.
    mu.mem_write(F_TIMEOUT, b'\x00')
    mu.mem_write(F_ERROR, b'\x00')
    mu.mem_write(F_CHECKSUM, b'\x00')
    mu.mem_write(OBJ + 0xD7, b'\x01')
    mu.mem_write(OBJ + 0xD8, b'\x00')
    state['stop_next'] = True
    mu.reg_write(UC_X86_REG_EBP, STACK)
    mu.reg_write(UC_X86_REG_ESP, STACK)
    mu.emu_start(SUCCESS_TAIL, 0, count=20000)
    state['stop_next'] = False

    after_success = (
        get32(mu, OBJ + 0x56), get32(mu, OBJ + 0x5E), get32(mu, OBJ + 0x62),
        get32(mu, OBJ + 0x76), get32(mu, OBJ + 0x7A), get32(mu, OBJ + 0x7E)
    )

    # Segundo bloco: 21ª instrução começa no passo 80 (0x0050).
    collect_one()
    frame2 = build_one(80)

    expected1 = model_frame(0, F13_HL * 20, F13_EXT * 20)
    expected2 = model_frame(80, F13_HL, F13_EXT)

    ok = (
        frame1 == expected1 and frame2 == expected2
        and len(frame1) == 247 and frame1[1] == 0xF4 and frame1[5] == 0xA0
        and after_success == (0, 6, 0, 80, 0, 80)
        and len(frame2) == 19 and frame2[1] == 0x10 and frame2[3] == 0x00 and frame2[4] == 0x50 and frame2[5] == 0x08
        and get32(mu, OBJ + 0x76) == 84 and get32(mu, OBJ + 0x7A) == 1
    )

    return dict(ok=ok, frame1=frame1, frame2=frame2, after_success=after_success,
                final_cursor=get32(mu, OBJ + 0x76), final_count=get32(mu, OBJ + 0x7A))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe', nargs='?', default=EXE_DEFAULT)
    ap.add_argument('-o', '--output')
    args = ap.parse_args()
    try:
        row = emulate(args.exe)
        f1, f2 = row['frame1'], row['frame2']
        lines = [
            'PC12 PG33 21 x F-13w / 20+1 BLOCK SPLIT - UNICORN OFFLINE EMULATION',
            '=' * 100,
            'mode=OFFLINE; TX routine intercepted before I/O; no COM/PLC',
            '',
            'block1: bytes=%d LEN=%02X start=%02X%02X HL=%02X checksum=%02X' %
            (len(f1), f1[1], f1[3], f1[4], f1[5], f1[-1]),
            'success-tail reset (+56,+5E,+62,+76,+7A,+7E)=' + repr(row['after_success']),
            'block2: bytes=%d LEN=%02X start=%02X%02X HL=%02X checksum=%02X' %
            (len(f2), f2[1], f2[3], f2[4], f2[5], f2[-1]),
            'final cursor=%d logical_count_in_block2=%d' % (row['final_cursor'], row['final_count']),
            '',
            'RESULT=' + ('PASS' if row['ok'] else 'FAIL'),
        ]
        if row['ok']:
            lines += [
                'O PC12 foi reproduzido offline como dois blocos: 20 instruções/80 palavras em start 0000,',
                'seguido de 1 instrução/4 palavras em start 0050 após a cauda original de sucesso.',
            ]
        lines.append('Nenhum byte foi transmitido fora do Unicorn.')
        rc = 0 if row['ok'] else 1
    except Exception as exc:
        lines = ['PC12 PG33 21 x F-13w / 20+1 BLOCK SPLIT - UNICORN OFFLINE EMULATION', '=' * 100,
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
