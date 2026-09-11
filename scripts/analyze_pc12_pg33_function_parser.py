#!/usr/bin/env python3
"""Análise OFFLINE do parser de instruções multistep usado pelo Write PLC Program.

Foco: helper 0x004BA69E, chamado antes da expansão StepSpan no caminho PG33.
Não executa PC12, não abre COM e não transmite qualquer byte.
"""

import argparse
import pathlib

from analyze_pc12_writeprog import (
    objdump_window,
    pe_info,
    scan_rel32_calls,
    callers_of,
    text_va_bounds,
)

ENTRY = 0x004BA69E
WINDOW_LO = 0x004BA5C0
WINDOW_HI = 0x004BAB80
WRITE_LO = 0x004B6A00
WRITE_HI = 0x004B7E20


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe')
    ap.add_argument('-o', '--output')
    args = ap.parse_args()

    path = pathlib.Path(args.exe)
    data = path.read_bytes()
    image_base, sections = pe_info(data)

    lines = []
    lines.append('PC12 PG33 FUNCTION PARSER MAP - STATIC OFFLINE ANALYSIS')
    lines.append('=' * 100)
    lines.append('entry=0x%08X' % ENTRY)
    lines.append('mode=OFFLINE ONLY; no serial, no PLC, no TX')
    lines.append('')

    lines.append('CALLERS OF 0x%08X' % ENTRY)
    lines.append('-' * 100)
    rows = callers_of(data, sections, image_base, ENTRY)
    if not rows:
        lines.append('none')
    for call_va, target, off, raw in rows:
        lines.append('CALLSITE=0x%08X target=0x%08X file+0x%08X bytes=[%s]' %
                     (call_va, target, off, ' '.join('%02X' % b for b in raw)))
    lines.append('')

    lines.append('CALLS FROM PARSER WINDOW')
    lines.append('-' * 100)
    for call_va, target, off, raw in scan_rel32_calls(
            data, sections, image_base, WINDOW_LO, WINDOW_HI):
        lines.append('0x%08X -> 0x%08X file+0x%08X bytes=[%s]' %
                     (call_va, target, off, ' '.join('%02X' % b for b in raw)))
    lines.append('')

    lines.append('DISASSEMBLY 0x%08X..0x%08X' % (WINDOW_LO, WINDOW_HI))
    lines.append('-' * 100)
    lines.extend(objdump_window(path, WINDOW_LO, WINDOW_HI, max_lines=2200))
    lines.append('')

    lines.append('WRITE-PATH CALLS INTO PARSER WINDOW')
    lines.append('-' * 100)
    for call_va, target, off, raw in scan_rel32_calls(
            data, sections, image_base, WRITE_LO, WRITE_HI):
        if WINDOW_LO <= target < WINDOW_HI:
            lines.append('0x%08X -> 0x%08X file+0x%08X bytes=[%s]' %
                         (call_va, target, off, ' '.join('%02X' % b for b in raw)))
    lines.append('')

    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')


if __name__ == '__main__':
    main()
