#!/usr/bin/env python3
"""Mapeia OFFLINE os campos usados pelos encoders PG33 de tamanho variavel.

Este script nao executa o PC12, nao abre COM e nao transmite qualquer byte.
Ele complementa analyze_pc12_write_fields.py concentrando-se nos campos do
objeto EDI que alimentam o registro PG33 e no helper 0x004BCA65 chamado antes
do switch de StepSpan.

Objetivos:
- localizar acessos a +0x172..+0x176 e flags auxiliares +0x14A/+0x14E/+0x14F;
- mostrar as janelas dos encoders que convergem para o caminho multistep;
- mostrar o helper 0x004BCA65 e o switch 1..4 passos em 0x004B7799;
- preservar a distincao entre registro PG33 e passo expandido observado no 34.
"""

import argparse
import pathlib
import struct

from analyze_pc12_writeprog import hx, objdump_window, pe_info, va_to_offset, offset_to_va

WRITE_LO = 0x004B6A00
WRITE_HI = 0x004B7E20
HELPER_LO = 0x004BCA20
HELPER_HI = 0x004BCB80

FIELDS = {
    0x14A: 'flag-14A',
    0x14E: 'flag-14E',
    0x14F: 'flag-14F',
    0x172: 'record-HIGH',
    0x173: 'record-LOW',
    0x174: 'record-EXT',
    0x175: 'aux-byte-175',
    0x176: 'StepSpan',
}


def u32(data, off):
    return struct.unpack_from('<I', data, off)[0]


def scan_field_accesses(data, sections, image_base, lo_va, hi_va):
    lo = va_to_offset(sections, image_base, lo_va)
    hi_last = va_to_offset(sections, image_base, hi_va - 1)
    if lo is None or hi_last is None:
        return []
    hi = hi_last + 1
    out = []
    i = lo
    while i < hi:
        va = offset_to_va(sections, image_base, i)
        if va is None:
            i += 1
            continue

        # ModRM com base EDI e disp32: 8A/8B le, 88/89 escreve.
        if i + 6 <= hi and data[i] in (0x8A, 0x8B, 0x88, 0x89) and (data[i + 1] & 0xC7) == 0x87:
            disp = u32(data, i + 2)
            if disp in FIELDS:
                op = data[i]
                rw = 'READ' if op in (0x8A, 0x8B) else 'WRITE'
                width = 8 if op in (0x8A, 0x88) else 32
                out.append((va, disp, rw, width, None, data[i:i + 6]))
            i += 6
            continue

        # MOV byte ptr [EDI+disp32], imm8
        if i + 7 <= hi and data[i:i + 2] == b'\xC6\x87':
            disp = u32(data, i + 2)
            if disp in FIELDS:
                out.append((va, disp, 'WRITE', 8, data[i + 6], data[i:i + 7]))
            i += 7
            continue

        # MOV dword ptr [EDI+disp32], imm32
        if i + 10 <= hi and data[i:i + 2] == b'\xC7\x87':
            disp = u32(data, i + 2)
            if disp in FIELDS:
                out.append((va, disp, 'WRITE', 32, u32(data, i + 6), data[i:i + 10]))
            i += 10
            continue

        i += 1
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe')
    ap.add_argument('-o', '--output')
    args = ap.parse_args()

    path = pathlib.Path(args.exe)
    data = path.read_bytes()
    image_base, sections = pe_info(data)
    accesses = scan_field_accesses(data, sections, image_base, WRITE_LO, WRITE_HI)

    lines = []
    lines.append('PC12 PG33 MULTISTEP ENCODER FIELD MAP - OFFLINE STATIC ANALYSIS')
    lines.append('=' * 96)
    lines.append('mode=OFFLINE ONLY; no serial, no PLC, no TX')
    lines.append('write-path=0x%08X..0x%08X' % (WRITE_LO, WRITE_HI))
    lines.append('')

    for field in sorted(FIELDS):
        rows = [r for r in accesses if r[1] == field]
        lines.append('+0x%03X %-16s accesses=%d' % (field, FIELDS[field], len(rows)))
        for va, _disp, rw, width, value, raw in rows:
            val = '' if value is None else ' value=0x%X' % value
            lines.append('  0x%08X %-5s %2d-bit%s bytes=[%s]' %
                         (va, rw, width, val, hx(raw)))
        lines.append('')

    lines.append('ENCODER AREA: RECORD EMISSION / MULTISTEP PREPARATION')
    lines.append('-' * 96)
    lines.extend(objdump_window(path, 0x004B73E0, 0x004B7799, max_lines=900))
    lines.append('')

    lines.append('GENERIC STEP-SPAN SWITCH')
    lines.append('-' * 96)
    lines.extend(objdump_window(path, 0x004B7790, 0x004B78A1, max_lines=360))
    lines.append('')

    lines.append('HELPER 0x004BCA65')
    lines.append('-' * 96)
    lines.extend(objdump_window(path, HELPER_LO, HELPER_HI, max_lines=500))
    lines.append('')

    lines.append('STATIC INTERPRETATION GUARDRAIL')
    lines.append('-' * 96)
    lines.append('Acesso a +0x172/+0x173/+0x174 prova apenas a origem do registro enviado.')
    lines.append('StepSpan em +0x176 controla o avanço do cursor do programa e nao deve ser confundido')
    lines.append('com o numero de registros PG33. Qualquer helper que altere TX/+0x5E/+0x62 precisa ser')
    lines.append('contabilizado antes de concluir que uma instrucao multistep cabe em um unico registro.')
    lines.append('Nenhum byte foi transmitido ao PLC.')

    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')


if __name__ == '__main__':
    main()
