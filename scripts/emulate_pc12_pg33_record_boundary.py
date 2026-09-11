#!/usr/bin/env python3
"""Emula OFFLINE a lógica de avanço/limite de uma INSTRUÇÃO PG33 do PC12.

O trecho 0x004B7799..0x004B7893 recebe em +0x176 o StepSpan da instrução
lógica (1..4 passos), avança o cursor real +0x76, incrementa +0x7A UMA vez
e encerra o bloco quando +0x7A chega a 20.

A análise do helper 0x004BCA65 mostrou que uma instrução de StepSpan 2..4
pode emitir 2..4 palavras de máquina no quadro. Portanto +0x7A é contador de
INSTRUÇÕES LÓGICAS, não contador de palavras de máquina.

A emulação para antes de retornar ao coletor ou antes da preparação do quadro.
Nenhuma rotina TX, API, COM ou PLC é acessada.
"""

import argparse
import pathlib
import struct
import sys

try:
    from unicorn import Uc, UcError, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE
    from unicorn.x86_const import UC_X86_REG_EDI, UC_X86_REG_ESP
except ImportError:
    print('ERRO: unicorn não instalado. Use: pip install unicorn==2.1.4', file=sys.stderr)
    raise

from emulate_pc12_pg33_builder import load_pe, map_image

EXE_DEFAULT = 'src/OpenLadderStudio.Desktop/pc12.exe'
ENTRY = 0x004B7799
NEXT_COLLECTOR = 0x004B6B8F
BUILD_PREP = 0x004B78A1

PROGRAM_SIZE = 0x00560368
LAST_PROGRAM_STEP = 0x0052CD3C

OBJ = 0x27000000
STACK = 0x28000000


def put32(mu, addr, value):
    mu.mem_write(addr, struct.pack('<I', value & 0xFFFFFFFF))


def get32(mu, addr):
    return struct.unpack('<I', bytes(mu.mem_read(addr, 4)))[0]


def get8(mu, addr):
    return bytes(mu.mem_read(addr, 1))[0]


def emulate(exe, step_span, start_cursor, start_instructions):
    if step_span not in (1, 2, 3, 4):
        raise ValueError('step_span deve ser 1..4')
    if not (0 <= start_instructions < 20):
        raise ValueError('start_instructions deve ser 0..19')

    data, image_base, sections = load_pe(exe)
    mu = Uc(UC_ARCH_X86, UC_MODE_32)
    map_image(mu, data, image_base, sections)
    mu.mem_map(OBJ, 0x2000)
    mu.mem_map(STACK - 0x10000, 0x20000)

    put32(mu, OBJ + 0x176, step_span)
    put32(mu, OBJ + 0x76, start_cursor)
    put32(mu, OBJ + 0x7A, start_instructions)
    mu.mem_write(OBJ + 0xD7, b'\x01')
    mu.mem_write(OBJ + 0xD8, b'\x01')

    # Folga para que somente o limite de 20 instruções decida o bloco.
    put32(mu, PROGRAM_SIZE, 4000)
    put32(mu, LAST_PROGRAM_STEP, 3999)

    mu.reg_write(UC_X86_REG_EDI, OBJ)
    mu.reg_write(UC_X86_REG_ESP, STACK)

    state = {'path': None}

    def hook(uc, address, size, _user):
        if address == NEXT_COLLECTOR:
            state['path'] = 'next-instruction'
            uc.emu_stop()
        elif address == BUILD_PREP:
            state['path'] = 'build-frame'
            uc.emu_stop()

    mu.hook_add(UC_HOOK_CODE, hook)
    try:
        mu.emu_start(ENTRY, 0, count=20000)
    except UcError as exc:
        raise RuntimeError('Unicorn falhou: %s' % exc)

    if state['path'] is None:
        raise RuntimeError('nenhum destino esperado foi alcançado')

    return {
        'path': state['path'],
        'span': step_span,
        'start_cursor': start_cursor,
        'start_instructions': start_instructions,
        'cursor': get32(mu, OBJ + 0x76),
        'instructions': get32(mu, OBJ + 0x7A),
        'd7': get8(mu, OBJ + 0xD7),
        'd8': get8(mu, OBJ + 0xD8),
    }


def verify(row, expected_path):
    return (
        row['path'] == expected_path
        and row['cursor'] == row['start_cursor'] + row['span']
        and row['instructions'] == row['start_instructions'] + 1
        and row['d7'] == 1
        and row['d8'] == (1 if expected_path == 'next-instruction' else 0)
    )


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe', nargs='?', default=EXE_DEFAULT)
    ap.add_argument('-o', '--output')
    args = ap.parse_args()

    fixtures = [
        ('span-1', 1, 0x0100, 0, 'next-instruction'),
        ('span-2', 2, 0x0100, 3, 'next-instruction'),
        ('span-3', 3, 0x0100, 7, 'next-instruction'),
        ('span-4', 4, 0x0100, 12, 'next-instruction'),
        ('instruction-20-span-1', 1, 0x0200, 19, 'build-frame'),
        ('instruction-20-span-4', 4, 0x0300, 19, 'build-frame'),
    ]

    rows = []
    overall = True
    for name, span, cursor, count, expected_path in fixtures:
        row = emulate(args.exe, span, cursor, count)
        row['name'] = name
        row['expected_path'] = expected_path
        row['ok'] = verify(row, expected_path)
        rows.append(row)
        overall &= row['ok']

    lines = []
    lines.append('PC12 PG33 INSTRUCTION STEP/BLOCK BOUNDARY - UNICORN OFFLINE EMULATION')
    lines.append('=' * 96)
    lines.append('entry=0x%08X collector=0x%08X build_prep=0x%08X' %
                 (ENTRY, NEXT_COLLECTOR, BUILD_PREP))
    lines.append('mode=OFFLINE; nenhuma rotina TX, COM ou API é executada')
    lines.append('')

    for row in rows:
        lines.append('%s: %s span=%d cursor %04X->%04X instructions %d->%d path=%s' %
                     ('OK' if row['ok'] else 'FALHA', row['name'], row['span'],
                      row['start_cursor'], row['cursor'], row['start_instructions'],
                      row['instructions'], row['path']))
        lines.append('  flags d7=%d d8=%d expected_path=%s' %
                     (row['d7'], row['d8'], row['expected_path']))

    lines.append('')
    lines.append('RESULT=' + ('PASS' if overall else 'FAIL'))
    lines.append('Conclusão: +0x176=1..4 avança +0x76 pelo mesmo número de passos,')
    lines.append('+0x7A conta UMA instrução lógica, e a 20ª instrução encerra o bloco.')
    lines.append('A quantidade de palavras HIGH/LOW/EXTERNAL é contabilizada separadamente.')
    lines.append('Nenhum byte foi transmitido fora do Unicorn.')

    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')
    return 0 if overall else 1


if __name__ == '__main__':
    raise SystemExit(main())
