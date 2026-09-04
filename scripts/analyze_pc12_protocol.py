#!/usr/bin/env python3
import argparse
import pathlib
import re
import shutil
import struct
import subprocess
from collections import defaultdict


def u16(data, off):
    return struct.unpack_from('<H', data, off)[0]


def u32(data, off):
    return struct.unpack_from('<I', data, off)[0]


def pe_info(data):
    info = {'image_base': 0, 'sections': []}
    if data[:2] != b'MZ':
        return info
    pe = u32(data, 0x3C)
    if data[pe:pe+4] != b'PE\0\0':
        return info
    nsec = u16(data, pe + 6)
    opt_size = u16(data, pe + 20)
    opt = pe + 24
    magic = u16(data, opt)
    if magic == 0x10B:
        info['image_base'] = u32(data, opt + 28)
    elif magic == 0x20B:
        info['image_base'] = struct.unpack_from('<Q', data, opt + 24)[0]
    sec = pe + 24 + opt_size
    for i in range(nsec):
        o = sec + i * 40
        name = data[o:o+8].split(b'\0', 1)[0].decode('ascii', 'replace')
        vsize = u32(data, o + 8)
        vaddr = u32(data, o + 12)
        raw_size = u32(data, o + 16)
        raw_ptr = u32(data, o + 20)
        info['sections'].append((name, vaddr, vsize, raw_ptr, raw_size))
    return info


def section_for_offset(sections, off):
    for name, vaddr, vsize, raw_ptr, raw_size in sections:
        if raw_ptr <= off < raw_ptr + raw_size:
            return name, vaddr + (off - raw_ptr)
    return None, None


def file_offset_for_rva(sections, rva):
    for name, vaddr, vsize, raw_ptr, raw_size in sections:
        span = max(vsize, raw_size)
        if vaddr <= rva < vaddr + span:
            rel = rva - vaddr
            if rel < raw_size:
                return raw_ptr + rel
    return None


def va_for_offset(sections, image_base, off):
    sec, rva = section_for_offset(sections, off)
    if rva is None:
        return None
    return image_base + rva


def hexline(blob):
    return ' '.join(f'{b:02X}' for b in blob)


def printable_strings(data, min_len=4):
    rx = re.compile(rb'[\x20-\x7E]{%d,}' % min_len)
    for m in rx.finditer(data):
        yield m.start(), m.group().decode('ascii', 'replace')


def find_all(data, needle, start=0, end=None):
    if end is None:
        end = len(data)
    pos = start
    while True:
        pos = data.find(needle, pos, end)
        if pos < 0:
            break
        yield pos
        pos += 1


def text_range(sections):
    for name, vaddr, vsize, raw_ptr, raw_size in sections:
        if name == '.text':
            return raw_ptr, raw_ptr + raw_size
    return 0, 0


def objdump_window(path, start_va, stop_va):
    exe = shutil.which('objdump')
    if not exe:
        return ['objdump unavailable']
    try:
        p = subprocess.run(
            [exe, '-D', '-Mintel', '--start-address=0x%X' % start_va,
             '--stop-address=0x%X' % stop_va, str(path)],
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            text=True, errors='replace', timeout=20, check=False)
        rows = p.stdout.splitlines()
        return rows[:220]
    except Exception as ex:
        return ['objdump failed: %s' % ex]


def checksum_candidates(data, center, radius=256, min_len=3, max_len=8):
    lo = max(0, center - radius)
    hi = min(len(data), center + radius)
    found = []
    for length in range(min_len, max_len + 1):
        for i in range(lo, hi - length + 1):
            b = data[i:i+length]
            if sum(b) & 0xFF == 0xFF:
                score = sum(1 for x in b[:-1] if x <= 0x20 or x in (0x34, 0x40, 0x80, 0xC0, 0xF0))
                if score >= 2:
                    found.append((i, b, score))
    found.sort(key=lambda x: (-x[2], abs(x[0]-center), len(x[1]), x[0]))
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
    pe = pe_info(data)
    sections = pe['sections']
    image_base = pe['image_base']
    text_lo, text_hi = text_range(sections)

    known = {
        'HELLO_ASCII_CON-ICB<CR>': bytes.fromhex('43 4F 4E 2D 49 43 42 0D'),
        'UNCLASSIFIED_F0_00_0F': bytes.fromhex('F0 00 0F'),
        'CLEAR_ALL_MEMORY_CANDIDATE_0F_00_F0': bytes.fromhex('0F 00 F0'),
        'READ_PROGRAM_STATIC_CANDIDATE': bytes.fromhex('34 03 00 00 A0 28'),
    }

    xref_targets = [
        'CON-ICB',
        'Read PLC Program...',
        'Read PLC System...',
        'Read PLC Vxxxx Register...',
        'Read PLC Dxxxx Register...',
        'Read PLC WCxxx Register...',
        'Read PLC FILE Register...',
        'Compare PLC Program...',
        'Write PLC Program...',
        'PLC Mode: Running',
        'PLC Mode: Program',
    ]

    keywords = [
        'Read PLC Program', 'PLC Program', 'Clear All Memory', 'Clear Program',
        'Download', 'Upload', 'RUN', 'STOP', 'Read', 'Program', 'CON-ICB'
    ]

    lines = []
    lines.append('PC12 OFFLINE PROTOCOL ANALYSIS')
    lines.append('=' * 96)
    lines.append(f'file={path.as_posix()}')
    lines.append(f'size={len(data)} bytes')
    lines.append(f'image_base=0x{image_base:08X}')
    lines.append('mode=OFFLINE ONLY; no serial port and no PLC command is used')
    lines.append('')

    lines.append('PE SECTIONS')
    lines.append('-' * 96)
    if sections:
        for name, vaddr, vsize, raw_ptr, raw_size in sections:
            lines.append(f'{name:8s} RVA=0x{vaddr:08X} VA=0x{image_base+vaddr:08X} VSIZE=0x{vsize:X} RAW=0x{raw_ptr:08X} RSIZE=0x{raw_size:X}')
    else:
        lines.append('PE sections could not be parsed')
    lines.append('')

    strings = list(printable_strings(data, 4))
    string_exact = defaultdict(list)
    for off, s in strings:
        string_exact[s].append(off)

    lines.append('RELEVANT ASCII STRINGS')
    lines.append('-' * 96)
    for off, s in strings:
        low = s.lower()
        if any(k.lower() in low for k in keywords):
            sec, rva = section_for_offset(sections, off)
            va = image_base + rva if rva is not None else None
            lines.append(f'file+0x{off:08X} rva={"0x%08X" % rva if rva is not None else "?"} va={"0x%08X" % va if va is not None else "?"} section={sec or "?"}: {s[:220]}')
    lines.append('')

    lines.append('CODE XREFS TO READ / STATUS STRINGS')
    lines.append('-' * 96)
    if not text_hi:
        lines.append('.text section not found; xref scan unavailable')
    else:
        for target in xref_targets:
            offs = string_exact.get(target, [])
            if not offs:
                lines.append(f'{target}: string not found exactly')
                continue
            for soff in offs:
                sva = va_for_offset(sections, image_base, soff)
                if sva is None or sva > 0xFFFFFFFF:
                    continue
                ptr = struct.pack('<I', sva)
                xrefs = list(find_all(data, ptr, text_lo, text_hi))
                lines.append(f'{target}: string file+0x{soff:08X} VA=0x{sva:08X}; .text pointer xrefs={len(xrefs)}')
                for xoff in xrefs[:12]:
                    xva = va_for_offset(sections, image_base, xoff)
                    ctx = data[max(text_lo, xoff-24):min(text_hi, xoff+28)]
                    lines.append(f'  XREF file+0x{xoff:08X} VA=0x{xva:08X} bytes=[{hexline(ctx)}]')
                    lines.append('  DISASM:')
                    for row in objdump_window(path, max(image_base, xva-64), xva+160):
                        lines.append('    ' + row)
    lines.append('')

    lines.append('KNOWN / PREVIOUSLY CLAIMED BYTE SEQUENCES')
    lines.append('-' * 96)
    seq_offsets = defaultdict(list)
    for label, needle in known.items():
        positions = list(find_all(data, needle))
        if not positions:
            lines.append(f'{label}: NOT FOUND as contiguous bytes [{hexline(needle)}]')
            continue
        for off in positions:
            seq_offsets[label].append(off)
            sec, rva = section_for_offset(sections, off)
            va = image_base + rva if rva is not None else None
            ctx = data[max(0, off-32):min(len(data), off+len(needle)+32)]
            lines.append(f'{label}: file+0x{off:08X} rva={"0x%08X" % rva if rva is not None else "?"} va={"0x%08X" % va if va is not None else "?"} section={sec or "?"} bytes=[{hexline(needle)}]')
            lines.append(f'  context(-32/+32)=[{hexline(ctx)}]')
    lines.append('')

    lines.append('CHECKSUM-FF STRUCTURES NEAR ANY CONTIGUOUS STATIC READ CANDIDATE')
    lines.append('-' * 96)
    centers = seq_offsets.get('READ_PROGRAM_STATIC_CANDIDATE', [])
    if not centers:
        lines.append('34 03 00 00 A0 28 is NOT present contiguously in pc12.exe; it must not be treated as a verified command.')
    else:
        for center in centers:
            lines.append(f'around file+0x{center:08X}:')
            for off, b, score in checksum_candidates(data, center):
                sec, rva = section_for_offset(sections, off)
                marker = ' <== known read candidate' if off == center and b == known['READ_PROGRAM_STATIC_CANDIDATE'] else ''
                lines.append(f'  file+0x{off:08X} rva={"0x%08X" % rva if rva is not None else "?"} {sec or "?":8s} score={score} [{hexline(b)}]{marker}')
    lines.append('')

    lines.append('INTERPRETATION RULES')
    lines.append('-' * 96)
    lines.append('* A contiguous byte sequence in the executable is evidence of a constant, not proof that it is transmitted.')
    lines.append('* A pointer xref identifies code that uses a UI/status string; nearby disassembly may reveal the relevant handler.')
    lines.append('* RUN/STOP/write/download/erase candidates remain blocked during discovery.')
    lines.append('* A read command is promoted only after code-flow or PC12 trace plus controlled physical validation shows it is read-only.')
    lines.append('* Resource-section matches are not protocol evidence unless executable code references them in the relevant path.')
    lines.append('')

    text = '\n'.join(lines) + '\n'
    pathlib.Path(args.output).write_text(text, encoding='utf-8')
    print(text)


if __name__ == '__main__':
    main()
