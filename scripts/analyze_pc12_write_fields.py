#!/usr/bin/env python3
"""Rastreia OFFLINE os campos e a convergência dos encoders PG33 do PC12.

Não executa o PC12, não abre COM e não transmite. O objetivo é localizar no
caminho Write PLC Program as escritas x86 simples sobre os offsets do objeto
EDI consumidos por 0x4B7958, mostrar as janelas que constroem HIGH/LOW/EXTERNAL
e mapear saltos relativos que entram nos pontos comuns de coleta, limite de 20
registros e preparação do quadro 0x33.
"""

import argparse
import pathlib
import struct

from analyze_pc12_writeprog import (
    hx,
    objdump_window,
    offset_to_va,
    pe_info,
    va_to_offset,
)

WRITE_LO = 0x004B6A00
WRITE_HI = 0x004B7E20
TARGET = {0x56, 0x5E, 0x62, 0x6A, 0x6E}
CONTROL_POINTS = {
    0x004B6B8F: 'collector-loop',
    0x004B7799: 'generic-span-switch',
    0x004B7869: 'record-count-boundary',
    0x004B7878: 'program-tail-check',
    0x004B7893: 'post-record-decision',
    0x004B78A1: 'frame-preparation',
    0x004B7958: 'pg33-builder',
    0x004B7D22: 'success-next-block-init',
}


def u32(data, off):
    return struct.unpack_from('<I', data, off)[0]


def i8(v):
    return v - 256 if v >= 128 else v


def i32(data, off):
    return struct.unpack_from('<i', data, off)[0]


def target8(v):
    return v in TARGET


def target32(v):
    return v in TARGET


def scan(data, sections, image_base):
    lo = va_to_offset(sections, image_base, WRITE_LO)
    hi_last = va_to_offset(sections, image_base, WRITE_HI - 1)
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

        if i + 7 <= hi and data[i] == 0xC7 and (data[i + 1] & 0xF8) == 0x40:
            if data[i + 1] == 0x47:
                disp = data[i + 2]
                if target8(disp):
                    out.append((va, disp, 'MOV32_IMM', u32(data, i + 3), data[i:i + 7]))
                i += 7
                continue
        if i + 10 <= hi and data[i:i + 2] == b'\xC7\x87':
            disp = u32(data, i + 2)
            if target32(disp):
                out.append((va, disp, 'MOV32_IMM', u32(data, i + 6), data[i:i + 10]))
            i += 10
            continue

        if i + 4 <= hi and data[i:i + 2] == b'\xC6\x47':
            disp = data[i + 2]
            if target8(disp):
                out.append((va, disp, 'MOV8_IMM', data[i + 3], data[i:i + 4]))
            i += 4
            continue
        if i + 7 <= hi and data[i:i + 2] == b'\xC6\x87':
            disp = u32(data, i + 2)
            if target32(disp):
                out.append((va, disp, 'MOV8_IMM', data[i + 6], data[i:i + 7]))
            i += 7
            continue

        if i + 3 <= hi and data[i] in (0x88, 0x89) and (data[i + 1] & 0xC7) == 0x47:
            disp = data[i + 2]
            if target8(disp):
                kind = 'MOV8_REG' if data[i] == 0x88 else 'MOV32_REG'
                out.append((va, disp, kind, None, data[i:i + 3]))
            i += 3
            continue
        if i + 6 <= hi and data[i] in (0x88, 0x89) and (data[i + 1] & 0xC7) == 0x87:
            disp = u32(data, i + 2)
            if target32(disp):
                kind = 'MOV8_REG' if data[i] == 0x88 else 'MOV32_REG'
                out.append((va, disp, kind, None, data[i:i + 6]))
            i += 6
            continue

        if i + 3 <= hi and data[i] == 0x01 and (data[i + 1] & 0xC7) == 0x47:
            disp = data[i + 2]
            if target8(disp):
                out.append((va, disp, 'ADD32_REG', None, data[i:i + 3]))
            i += 3
            continue

        if i + 4 <= hi and data[i] == 0x83 and (data[i + 1] & 0xC7) == 0x47:
            disp = data[i + 2]
            subop = (data[i + 1] >> 3) & 7
            if target8(disp) and subop in (0, 5):
                kind = 'ADD32_IMM8' if subop == 0 else 'SUB32_IMM8'
                out.append((va, disp, kind, data[i + 3], data[i:i + 4]))
            i += 4
            continue

        if i + 3 <= hi and data[i] == 0xFF and (data[i + 1] & 0xC7) == 0x47:
            disp = data[i + 2]
            subop = (data[i + 1] >> 3) & 7
            if target8(disp) and subop in (0, 1):
                kind = 'INC32' if subop == 0 else 'DEC32'
                out.append((va, disp, kind, None, data[i:i + 3]))
            i += 3
            continue

        i += 1
    return out


def scan_control_branches(data, sections, image_base):
    """Lista saltos relativos cujo alvo é um dos pontos de controle conhecidos."""
    lo = va_to_offset(sections, image_base, WRITE_LO)
    hi_last = va_to_offset(sections, image_base, WRITE_HI - 1)
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

        op = data[i]
        target = None
        kind = None
        size = 1

        if op == 0xEB and i + 2 <= hi:  # JMP short
            size = 2
            target = (va + size + i8(data[i + 1])) & 0xFFFFFFFF
            kind = 'JMP8'
        elif 0x70 <= op <= 0x7F and i + 2 <= hi:  # Jcc short
            size = 2
            target = (va + size + i8(data[i + 1])) & 0xFFFFFFFF
            kind = 'JCC8_%02X' % op
        elif op == 0xE9 and i + 5 <= hi:  # JMP near
            size = 5
            target = (va + size + i32(data, i + 1)) & 0xFFFFFFFF
            kind = 'JMP32'
        elif op == 0x0F and i + 6 <= hi and 0x80 <= data[i + 1] <= 0x8F:
            size = 6
            target = (va + size + i32(data, i + 2)) & 0xFFFFFFFF
            kind = 'JCC32_%02X' % data[i + 1]

        if target in CONTROL_POINTS:
            out.append((va, target, kind, data[i:i + size]))
        i += size if target is not None else 1
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe')
    ap.add_argument('-o', '--output')
    args = ap.parse_args()

    path = pathlib.Path(args.exe)
    data = path.read_bytes()
    image_base, sections = pe_info(data)
    hits = scan(data, sections, image_base)
    control_branches = scan_control_branches(data, sections, image_base)

    lines = []
    lines.append('PC12 WRITE PLC PROGRAM - PG33 OBJECT FIELD TRACE')
    lines.append('=' * 92)
    lines.append('mode=OFFLINE ONLY; no serial, no PLC, no TX')
    lines.append('range=0x%08X..0x%08X' % (WRITE_LO, WRITE_HI))
    lines.append('target_offsets=' + ', '.join('+0x%02X' % x for x in sorted(TARGET)))
    lines.append('')

    by_field = {x: [] for x in TARGET}
    for row in hits:
        by_field[row[1]].append(row)

    for field in sorted(TARGET):
        rows = by_field[field]
        lines.append('FIELD +0x%02X : %d write candidate(s)' % (field, len(rows)))
        for va, disp, kind, value, raw in rows:
            value_text = '?' if value is None else '0x%X' % value
            lines.append('  0x%08X  %-12s value=%-10s bytes=[%s]' %
                         (va, kind, value_text, hx(raw)))
        lines.append('')

    lines.append('DIRECT BRANCHES INTO KNOWN CONTROL POINTS')
    lines.append('-' * 92)
    for target in sorted(CONTROL_POINTS):
        refs = [r for r in control_branches if r[1] == target]
        lines.append('0x%08X %-24s incoming=%d' %
                     (target, CONTROL_POINTS[target], len(refs)))
        for src, _target, kind, raw in refs:
            lines.append('  from 0x%08X %-10s bytes=[%s]' % (src, kind, hx(raw)))
    lines.append('')

    lines.append('CHUNK INITIALIZATION')
    lines.append('-' * 92)
    lines.extend(objdump_window(path, 0x004B7D20, 0x004B7D70, max_lines=100))
    lines.append('')

    lines.append('ENCODER WINDOWS THROUGH EXIT / BOUNDARY DECISION')
    lines.append('-' * 92)
    encoder_sites = [row[0] for row in by_field[0x56]
                     if row[2] == 'ADD32_IMM8' and row[3] == 0x2]
    for idx, va in enumerate(encoder_sites, 1):
        a = max(WRITE_LO, va - 0x48)
        b = min(WRITE_HI, va + 0xB0)
        lines.append('### ENCODER#%02d completion=0x%08X window=0x%08X..0x%08X' %
                     (idx, va, a, b))
        lines.extend(objdump_window(path, a, b, max_lines=260))
        lines.append('')

    lines.append('GENERIC VARIABLE-SPAN / 20-RECORD BOUNDARY')
    lines.append('-' * 92)
    lines.extend(objdump_window(path, 0x004B7790, 0x004B78B0, max_lines=320))
    lines.append('')

    lines.append('ADDRESS BYTE CONSTRUCTION')
    lines.append('-' * 92)
    lines.extend(objdump_window(path, 0x004B78E0, 0x004B7958, max_lines=160))
    lines.append('')

    lines.append('KNOWN CONSUMERS AT 0x004B7958')
    lines.append('  +0x56 -> TX[1] formula and TX[5]')
    lines.append('  +0x5E -> destination index while copying body; later checksum index')
    lines.append('  +0x62 -> number of bytes copied from object +0xE0')
    lines.append('  +0x6A -> TX[3]')
    lines.append('  +0x6E -> TX[4]')
    lines.append('')
    lines.append('GUARDRAIL: this trace establishes static data/control flow only; it never executes a write.')

    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')


if __name__ == '__main__':
    main()
