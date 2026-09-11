#!/usr/bin/env python3
"""Análise OFFLINE do parser de instruções multistep usado pelo Write PLC Program.

Foco: helper 0x004BA69E, chamado antes da expansão StepSpan no caminho PG33,
e os cases do F-13/F-13w/F-13d.
Não executa PC12, não abre COM e não transmite qualquer byte.
"""

import argparse
import pathlib

from analyze_pc12_writeprog import (
    objdump_window,
    pe_info,
    scan_rel32_calls,
    callers_of,
)

ENTRY = 0x004BA69E
DISPATCH_LO = 0x004BA69E
DISPATCH_HI = 0x004BA900
F13_LO = 0x004BACC0
F13_HI = 0x004BAD70
WRITE_LO = 0x004B6A00
WRITE_HI = 0x004B7E20


def hexbytes(raw):
    return ' '.join('%02X' % b for b in raw)


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
    lines.append('focus F-13=0x004BACF0 F-13w=0x004BAD14 F-13d=0x004BAD42')
    lines.append('mode=OFFLINE ONLY; no serial, no PLC, no TX')
    lines.append('')

    lines.append('DIRECT CALLERS OF 0x%08X' % ENTRY)
    lines.append('-' * 100)
    rows = callers_of(data, sections, image_base, ENTRY)
    if not rows:
        lines.append('none (entrada também pode ser alcançada por CALL indireto/dispatch)')
    for call_va, target, off, raw in rows:
        lines.append('CALLSITE=0x%08X target=0x%08X file+0x%08X bytes=[%s]' %
                     (call_va, target, off, hexbytes(raw)))
    lines.append('')

    lines.append('DISPATCH 0x%08X..0x%08X' % (DISPATCH_LO, DISPATCH_HI))
    lines.append('-' * 100)
    lines.extend(objdump_window(path, DISPATCH_LO, DISPATCH_HI, max_lines=900))
    lines.append('')

    lines.append('F-13 / F-13w / F-13d ENCODER CASES')
    lines.append('-' * 100)
    lines.extend(objdump_window(path, F13_LO, F13_HI, max_lines=360))
    lines.append('')

    lines.append('CALLS FROM F-13 FOCUS WINDOW')
    lines.append('-' * 100)
    for call_va, target, off, raw in scan_rel32_calls(
            data, sections, image_base, F13_LO, F13_HI):
        lines.append('0x%08X -> 0x%08X file+0x%08X bytes=[%s]' %
                     (call_va, target, off, hexbytes(raw)))
    lines.append('')

    lines.append('WRITE-PATH DIRECT CALLS INTO PARSER/F13 RANGES')
    lines.append('-' * 100)
    for call_va, target, off, raw in scan_rel32_calls(
            data, sections, image_base, WRITE_LO, WRITE_HI):
        if (DISPATCH_LO <= target < DISPATCH_HI) or (F13_LO <= target < F13_HI):
            lines.append('0x%08X -> 0x%08X file+0x%08X bytes=[%s]' %
                         (call_va, target, off, hexbytes(raw)))
    lines.append('')

    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')


if __name__ == '__main__':
    main()
