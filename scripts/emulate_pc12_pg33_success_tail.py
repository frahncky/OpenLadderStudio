#!/usr/bin/env python3
"""Emula SOMENTE o caminho de sucesso após um quadro PG33 no PC12.

Escopo
------
O código começa em 0x004B7C50, depois da cadeia de tentativas de TX do
"Write PLC Program", com as três flags genéricas de comunicação zeradas.
Ele termina antes de qualquer nova coleta de instrução ou chamada de UI:

- 0x004B7893: volta para coletar o próximo bloco;
- 0x004B7D96: caminho de conclusão do programa.

Objetivo
--------
Confirmar dinamicamente, mas 100% OFFLINE, o que acontece quando o PC12 aceita
um envio PG33: reinicialização do estado do bloco e preservação do cursor real
de passos para o próximo quadro.

Não abre COM, não executa a rotina de TX e não transmite qualquer byte.
"""

import argparse
import pathlib
import struct
import sys

try:
    from unicorn import Uc, UcError, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE
    from unicorn.x86_const import (
        UC_X86_REG_EBP,
        UC_X86_REG_EDI,
        UC_X86_REG_ESP,
    )
except ImportError:
    print('ERRO: unicorn não instalado. Use: pip install unicorn==2.1.4', file=sys.stderr)
    raise

from emulate_pc12_pg33_builder import load_pe, map_image

EXE_DEFAULT = 'src/OpenLadderStudio.Desktop/pc12.exe'

SUCCESS_TAIL = 0x004B7C50
NEXT_COLLECTOR = 0x004B7893
COMPLETE_PATH = 0x004B7D96

F_TIMEOUT = 0x004FA8B7
F_ERROR = 0x004FA8B8
F_CHECKSUM = 0x004FA8B9
PROGRAM_SIZE = 0x00560368
TRACE_STEP = 0x005703F0
OPTIONAL_TRACE_FLAG = 0x005703E6

OBJ = 0x24000000
STACK = 0x25000000


def put32(mu, addr, value):
    mu.mem_write(addr, struct.pack('<I', value & 0xFFFFFFFF))


def get32(mu, addr):
    return struct.unpack('<I', bytes(mu.mem_read(addr, 4)))[0]


def get8(mu, addr):
    return bytes(mu.mem_read(addr, 1))[0]


def emulate(exe, cursor, program_size):
    if cursor < 0:
        raise ValueError('cursor deve ser >= 0')
    if program_size <= 0:
        raise ValueError('program_size deve ser > 0')

    data, image_base, sections = load_pe(exe)
    mu = Uc(UC_ARCH_X86, UC_MODE_32)
    map_image(mu, data, image_base, sections)
    mu.mem_map(OBJ, 0x10000)
    mu.mem_map(STACK - 0x10000, 0x20000)

    # Estado anterior propositalmente "sujo": o caminho de sucesso deve
    # reinicializar estes campos antes do próximo bloco.
    put32(mu, OBJ + 0x56, 0x22)
    put32(mu, OBJ + 0x5E, 0x44)
    put32(mu, OBJ + 0x62, 0x11)
    put32(mu, OBJ + 0x76, cursor)
    put32(mu, OBJ + 0x7A, 0x13)
    put32(mu, OBJ + 0x7E, 0xDEAD)
    mu.mem_write(OBJ + 0xD7, b'\x01')  # continuar enquanto houver programa
    mu.mem_write(OBJ + 0xD8, b'\x00')

    # Sucesso genérico da rotina de comunicação: o chamador PG33 testa
    # somente estas três flags antes de entrar em 0x004B7D22.
    mu.mem_write(F_TIMEOUT, b'\x00')
    mu.mem_write(F_ERROR, b'\x00')
    mu.mem_write(F_CHECKSUM, b'\x00')
    mu.mem_write(OPTIONAL_TRACE_FLAG, b'\x00')
    put32(mu, PROGRAM_SIZE, program_size)
    put32(mu, TRACE_STEP, 0xFFFFFFFF)

    mu.reg_write(UC_X86_REG_EDI, OBJ)
    mu.reg_write(UC_X86_REG_EBP, STACK)
    mu.reg_write(UC_X86_REG_ESP, STACK)

    state = {'path': None}

    def hook(uc, address, size, _user):
        if address == NEXT_COLLECTOR:
            state['path'] = 'next-block'
            uc.emu_stop()
        elif address == COMPLETE_PATH:
            state['path'] = 'complete'
            uc.emu_stop()

    mu.hook_add(UC_HOOK_CODE, hook)

    try:
        mu.emu_start(SUCCESS_TAIL, 0, count=20000)
    except UcError as exc:
        raise RuntimeError('Unicorn falhou: %s' % exc)

    if state['path'] is None:
        raise RuntimeError('nenhum destino esperado foi alcançado')

    result = {
        'path': state['path'],
        'cursor': cursor,
        'program_size': program_size,
        'field56': get32(mu, OBJ + 0x56),
        'field5e': get32(mu, OBJ + 0x5E),
        'field62': get32(mu, OBJ + 0x62),
        'field76': get32(mu, OBJ + 0x76),
        'field7a': get32(mu, OBJ + 0x7A),
        'field7e': get32(mu, OBJ + 0x7E),
        'd7': get8(mu, OBJ + 0xD7),
        'd8': get8(mu, OBJ + 0xD8),
        'trace_step': get32(mu, TRACE_STEP),
    }
    return result


def verify(row, expected_path):
    expected_d7 = 1 if expected_path == 'next-block' else 0
    return (
        row['path'] == expected_path
        and row['field56'] == 0
        and row['field5e'] == 6
        and row['field62'] == 0
        and row['field76'] == row['cursor']
        and row['field7a'] == 0
        and row['field7e'] == row['cursor']
        and row['trace_step'] == row['cursor']
        and row['d7'] == expected_d7
        and row['d8'] == 1
    )


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe', nargs='?', default=EXE_DEFAULT)
    ap.add_argument('-o', '--output')
    args = ap.parse_args()

    fixtures = [
        ('middle-of-program', 0x0123, 4000, 'next-block'),
        ('last-step-reached', 4000, 4000, 'complete'),
        ('cursor-past-limit', 4001, 4000, 'complete'),
    ]

    rows = []
    overall = True
    for name, cursor, size, expected_path in fixtures:
        row = emulate(args.exe, cursor, size)
        ok = verify(row, expected_path)
        row['name'] = name
        row['expected_path'] = expected_path
        row['ok'] = ok
        rows.append(row)
        overall &= ok

    lines = []
    lines.append('PC12 PG33 SUCCESS TAIL - UNICORN OFFLINE EMULATION')
    lines.append('=' * 92)
    lines.append('entry=0x%08X next=0x%08X complete=0x%08X' %
                 (SUCCESS_TAIL, NEXT_COLLECTOR, COMPLETE_PATH))
    lines.append('mode=OFFLINE; nenhuma rotina TX é executada')
    lines.append('')

    for row in rows:
        lines.append('%s: %s cursor=0x%04X size=%d path=%s expected=%s' %
                     ('OK' if row['ok'] else 'FALHA', row['name'], row['cursor'],
                      row['program_size'], row['path'], row['expected_path']))
        lines.append('  reset: +56=%d +5E=%d +62=%d +7A=%d' %
                     (row['field56'], row['field5e'], row['field62'], row['field7a']))
        lines.append('  cursor: +76=0x%04X +7E=0x%04X trace=0x%04X' %
                     (row['field76'], row['field7e'], row['trace_step']))
        lines.append('  flags: d7=%d d8=%d' % (row['d7'], row['d8']))

    lines.append('')
    lines.append('RESULT=' + ('PASS' if overall else 'FAIL'))
    lines.append('Conclusão: após sucesso, o próximo bloco começa no cursor REAL de passos (+0x76),')
    lines.append('não em start+N. Quando cursor >= tamanho do programa, o PC12 entra no caminho de conclusão.')
    lines.append('Nenhum byte foi transmitido fora do Unicorn.')

    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')
    return 0 if overall else 1


if __name__ == '__main__':
    raise SystemExit(main())
