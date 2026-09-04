#!/usr/bin/env python3
import argparse
import pathlib
import re
import struct
from collections import defaultdict


def u16(data, off):
    return struct.unpack_from('<H', data, off)[0]


def u32(data, off):
    return struct.unpack_from('<I', data, off)[0]


def pe_sections(data):
    out = []
    if data[:2] != b'MZ':
        return out
    pe = u32(data, 0x3C)
    if data[pe:pe+4] != b'PE\0\0':
        return out
    nsec = u16(data, pe + 6)
    opt_size = u16(data, pe + 20)
    sec = pe + 24 + opt_size
    for i in range(nsec):
        o = sec + i * 40
        name = data[o:o+8].split(b'\0', 1)[0].decode('ascii', 'replace')
        vsize = u32(data, o + 8)
        vaddr = u32(data, o + 12)
        raw_size = u32(data, o + 16)
        raw_ptr = u32(data, o + 20)
        out.append((name, vaddr, vsize, raw_ptr, raw_size))
    return out


def section_for_offset(sections, off):
    for name, vaddr, vsize, raw_ptr, raw_size in sections:
        if raw_ptr <= off < raw_ptr + raw_size:
            return name, vaddr + (off - raw_ptr)
    return None, None


def hexline(blob):
    return ' '.join(f'{b:02X}' for b in blob)


def printable_strings(data, min_len=4):
    # ASCII strings only; the target binary is a 32-bit legacy Windows program.
    rx = re.compile(rb'[\x20-\x7E]{%d,}' % min_len)
    for m in rx.finditer(data):
        yield m.start(), m.group().decode('ascii', 'replace')


def find_all(data, needle):
    start = 0
    while True:
        pos = data.find(needle, start)
        if pos < 0:
            break
        yield pos
        start = pos + 1


def checksum_candidates(data, center, radius=256, min_len=3, max_len=8):
    lo = max(0, center - radius)
    hi = min(len(data), center + radius)
    found = []
    for length in range(min_len, max_len + 1):
        for i in range(lo, hi - length + 1):
            b = data[i:i+length]
            if sum(b) & 0xFF == 0xFF:
                # Prefer frames with some zero/low-valued fields, typical of command structures.
                score = sum(1 for x in b[:-1] if x <= 0x20 or x in (0x40, 0x80, 0xC0, 0xF0))
                if score >= 2:
                    found.append((i, b, score))
    found.sort(key=lambda x: (-x[2], abs(x[0]-center), len(x[1]), x[0]))
    # de-duplicate exact offset/blob and cap output
    seen = set()
    out = []
    for item in found:
        key = (item[0], item[1])
        if key in seen:
            continue
        seen.add(key)
        out.append(item)
        if len(out) >= 40:
            break
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe')
    ap.add_argument('-o', '--output', required=True)
    args = ap.parse_args()

    path = pathlib.Path(args.exe)
    data = path.read_bytes()
    sections = pe_sections(data)

    known = {
        'HELLO_ASCII_CON-ICB<CR>': bytes.fromhex('43 4F 4E 2D 49 43 42 0D'),
        'UNCLASSIFIED_F0_00_0F': bytes.fromhex('F0 00 0F'),
        'CLEAR_ALL_MEMORY_CANDIDATE_0F_00_F0': bytes.fromhex('0F 00 F0'),
        'READ_PROGRAM_STATIC_CANDIDATE': bytes.fromhex('34 03 00 00 A0 28'),
    }

    keywords = [
        'Read PLC Program', 'PLC Program', 'Clear All Memory', 'Clear Program',
        'Download', 'Upload', 'RUN', 'STOP', 'Read', 'Program', 'CON-ICB'
    ]

    lines = []
    lines.append('PC12 OFFLINE PROTOCOL ANALYSIS')
    lines.append('=' * 80)
    lines.append(f'file={path.as_posix()}')
    lines.append(f'size={len(data)} bytes')
    lines.append('mode=OFFLINE ONLY; no serial port and no PLC command is used')
    lines.append('')

    lines.append('PE SECTIONS')
    lines.append('-' * 80)
    if sections:
        for name, vaddr, vsize, raw_ptr, raw_size in sections:
            lines.append(f'{name:8s} RVA=0x{vaddr:08X} VSIZE=0x{vsize:X} RAW=0x{raw_ptr:08X} RSIZE=0x{raw_size:X}')
    else:
        lines.append('PE sections could not be parsed')
    lines.append('')

    strings = list(printable_strings(data, 4))
    lines.append('RELEVANT ASCII STRINGS')
    lines.append('-' * 80)
    matched_strings = []
    for off, s in strings:
        low = s.lower()
        if any(k.lower() in low for k in keywords):
            sec, rva = section_for_offset(sections, off)
            matched_strings.append((off, s))
            lines.append(f'file+0x{off:08X} rva={"0x%08X" % rva if rva is not None else "?"} section={sec or "?"}: {s[:220]}')
    if not matched_strings:
        lines.append('no keyword strings found')
    lines.append('')

    lines.append('KNOWN / PREVIOUSLY OBSERVED BYTE SEQUENCES')
    lines.append('-' * 80)
    seq_offsets = defaultdict(list)
    for label, needle in known.items():
        positions = list(find_all(data, needle))
        if not positions:
            lines.append(f'{label}: NOT FOUND as contiguous bytes [{hexline(needle)}]')
            continue
        for off in positions:
            seq_offsets[label].append(off)
            sec, rva = section_for_offset(sections, off)
            ctx = data[max(0, off-32):min(len(data), off+len(needle)+32)]
            lines.append(f'{label}: file+0x{off:08X} rva={"0x%08X" % rva if rva is not None else "?"} section={sec or "?"} bytes=[{hexline(needle)}]')
            lines.append(f'  context(-32/+32)=[{hexline(ctx)}]')
    lines.append('')

    lines.append('CHECKSUM-FF STRUCTURES NEAR STATIC READ CANDIDATE')
    lines.append('-' * 80)
    centers = seq_offsets.get('READ_PROGRAM_STATIC_CANDIDATE', [])
    if not centers:
        lines.append('static read candidate not found contiguously; no local scan performed')
    else:
        for center in centers:
            lines.append(f'around file+0x{center:08X}:')
            for off, b, score in checksum_candidates(data, center):
                sec, rva = section_for_offset(sections, off)
                marker = ' <== known read candidate' if off == center and b == known['READ_PROGRAM_STATIC_CANDIDATE'] else ''
                lines.append(f'  file+0x{off:08X} rva={"0x%08X" % rva if rva is not None else "?"} {sec or "?":8s} score={score} [{hexline(b)}]{marker}')
    lines.append('')

    lines.append('INTERPRETATION RULES')
    lines.append('-' * 80)
    lines.append('* A contiguous byte sequence in the executable is evidence of a constant, not proof that it is transmitted.')
    lines.append('* A checksum-FF window near another constant is only a candidate until code-flow or physical capture confirms it.')
    lines.append('* RUN/STOP/write/download/erase candidates must remain blocked during discovery.')
    lines.append('* A read command may be promoted only after static evidence + PC12 trace/controlled physical validation show it is read-only.')
    lines.append('')

    pathlib.Path(args.output).write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print('\n'.join(lines))


if __name__ == '__main__':
    main()
