#!/usr/bin/env python3
"""Mapeia OFFLINE as saídas dos encoders do Write PLC Program do PC12.

Os pontos onde o objeto EDI recebe `+0x56 += 2` marcam a conclusão de um
registro HIGH/LOW/EXTERNAL no caminho PG33. O relatório amplia a janela DEPOIS
desses pontos para descobrir como cada classe:

- atualiza o cursor real +0x76;
- atualiza o contador de registros +0x7A;
- trata o limite do programa;
- converge para continuar a coleta ou montar o quadro 0x33.

Somente bytes do pc12.exe são lidos. Não há execução, COM, PLC ou TX.
"""

import argparse
import pathlib

from analyze_pc12_writeprog import objdump_window, pe_info, va_to_offset, offset_to_va

WRITE_LO = 0x004B6A00
WRITE_HI = 0x004B7E20
PATTERN = bytes.fromhex('83 47 56 02')  # add dword ptr [edi+56],2


def find_sites(data, sections, image_base):
    lo = va_to_offset(sections, image_base, WRITE_LO)
    hi_last = va_to_offset(sections, image_base, WRITE_HI - 1)
    if lo is None or hi_last is None:
        return []
    hi = hi_last + 1
    out = []
    pos = lo
    while True:
        pos = data.find(PATTERN, pos, hi)
        if pos < 0:
            break
        va = offset_to_va(sections, image_base, pos)
        if va is not None:
            out.append(va)
        pos += 1
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe')
    ap.add_argument('-o', '--output')
    args = ap.parse_args()

    path = pathlib.Path(args.exe)
    data = path.read_bytes()
    image_base, sections = pe_info(data)
    sites = find_sites(data, sections, image_base)

    lines = []
    lines.append('PC12 WRITE PLC PROGRAM - ENCODER EXIT MAP')
    lines.append('=' * 96)
    lines.append('mode=OFFLINE ONLY; no execution, no COM, no PLC, no TX')
    lines.append('marker=83 47 56 02  ; +0x56 += 2')
    lines.append('sites=%d' % len(sites))
    lines.append('')

    for idx, va in enumerate(sites, 1):
        start = max(WRITE_LO, va - 0x18)
        stop = min(WRITE_HI, va + 0xB0)
        lines.append('### ENCODER EXIT #%02d completion=0x%08X window=0x%08X..0x%08X' %
                     (idx, va, start, stop))
        lines.extend(objdump_window(path, start, stop, max_lines=260))
        lines.append('')

    lines.append('REFERENCE CONVERGENCE WINDOWS')
    lines.append('-' * 96)
    lines.append('Generic variable-span switch / 20-record boundary:')
    lines.extend(objdump_window(path, 0x004B7790, 0x004B78B0, max_lines=320))
    lines.append('')
    lines.append('Frame preparation / builder handoff:')
    lines.extend(objdump_window(path, 0x004B78A0, 0x004B79A0, max_lines=320))
    lines.append('')

    lines.append('GUARDRAIL')
    lines.append('-' * 96)
    lines.append('Este relatório apenas desassembla o executável. Nenhum caminho do PC12 é executado.')

    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')


if __name__ == '__main__':
    main()
