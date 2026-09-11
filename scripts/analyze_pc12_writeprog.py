#!/usr/bin/env python3
"""Análise estática OFFLINE do caminho Write PLC Program do PC12.

Não abre porta serial, não executa o PC12 e não transmite qualquer quadro.
O objetivo é localizar construtores de quadros e acessos ao buffer serial no
executável original, mantendo separados fato estático, hipótese e protocolo
fisicamente confirmado.
"""

import argparse
import pathlib
import re
import shutil
import struct
import subprocess


def u16(data, off):
    return struct.unpack_from('<H', data, off)[0]


def u32(data, off):
    return struct.unpack_from('<I', data, off)[0]


def pe_info(data):
    if data[:2] != b'MZ':
        raise ValueError('arquivo nao e MZ')
    pe = u32(data, 0x3C)
    if data[pe:pe + 4] != b'PE\0\0':
        raise ValueError('cabecalho PE ausente')
    nsec = u16(data, pe + 6)
    opt_size = u16(data, pe + 20)
    opt = pe + 24
    magic = u16(data, opt)
    if magic != 0x10B:
        raise ValueError('este analisador exige PE32 (magic 0x10B)')
    image_base = u32(data, opt + 28)
    sec_off = opt + opt_size
    sections = []
    for i in range(nsec):
        o = sec_off + i * 40
        name = data[o:o + 8].split(b'\0', 1)[0].decode('ascii', 'replace')
        vsize = u32(data, o + 8)
        rva = u32(data, o + 12)
        raw_size = u32(data, o + 16)
        raw_ptr = u32(data, o + 20)
        sections.append((name, rva, vsize, raw_ptr, raw_size))
    return image_base, sections


def offset_to_va(sections, image_base, off):
    for _name, rva, _vsize, raw_ptr, raw_size in sections:
        if raw_ptr <= off < raw_ptr + raw_size:
            return image_base + rva + (off - raw_ptr)
    return None


def va_to_offset(sections, image_base, va):
    rva_target = va - image_base
    for _name, rva, vsize, raw_ptr, raw_size in sections:
        span = max(vsize, raw_size)
        if rva <= rva_target < rva + span:
            rel = rva_target - rva
            if rel < raw_size:
                return raw_ptr + rel
    return None


def text_bounds(sections):
    for name, _rva, _vsize, raw_ptr, raw_size in sections:
        if name == '.text':
            return raw_ptr, raw_ptr + raw_size
    return 0, 0


def find_all(data, needle, start=0, end=None):
    if end is None:
        end = len(data)
    pos = start
    while True:
        pos = data.find(needle, pos, end)
        if pos < 0:
            return
        yield pos
        pos += 1


def hx(blob):
    return ' '.join('%02X' % b for b in blob)


def objdump_window(path, start_va, stop_va, max_lines=180):
    exe = shutil.which('objdump')
    if not exe:
        return ['objdump indisponivel']
    try:
        p = subprocess.run(
            [exe, '-D', '-Mintel', '--start-address=0x%X' % start_va,
             '--stop-address=0x%X' % stop_va, str(path)],
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
            text=True, errors='replace', timeout=20, check=False)
        return p.stdout.splitlines()[:max_lines]
    except Exception as ex:
        return ['objdump falhou: %s' % ex]


def printable_strings(data, min_len=4):
    rx = re.compile(rb'[\x20-\x7E]{%d,}' % min_len)
    for m in rx.finditer(data):
        yield m.start(), m.group().decode('ascii', 'replace')


def scan_direct_global_stores(data, sections, image_base, lo_va, hi_va, mem_lo, mem_hi):
    lo = va_to_offset(sections, image_base, lo_va)
    hi = va_to_offset(sections, image_base, hi_va - 1)
    if lo is None or hi is None:
        return []
    hi += 1
    hits = []
    i = lo
    while i < hi:
        va = offset_to_va(sections, image_base, i)
        if va is None:
            i += 1
            continue

        # mov byte ptr [imm32], imm8
        if i + 7 <= hi and data[i:i + 2] == b'\xC6\x05':
            addr = u32(data, i + 2)
            if mem_lo <= addr <= mem_hi:
                hits.append((va, i, addr, 'MOV8_IMM', data[i + 6], data[i:i + 7]))
            i += 7
            continue

        # mov dword ptr [imm32], imm32
        if i + 10 <= hi and data[i:i + 2] == b'\xC7\x05':
            addr = u32(data, i + 2)
            if mem_lo <= addr <= mem_hi:
                hits.append((va, i, addr, 'MOV32_IMM', u32(data, i + 6), data[i:i + 10]))
            i += 10
            continue

        # mov moffs8, AL
        if i + 5 <= hi and data[i] == 0xA2:
            addr = u32(data, i + 1)
            if mem_lo <= addr <= mem_hi:
                hits.append((va, i, addr, 'MOV8_AL', None, data[i:i + 5]))
            i += 5
            continue

        # mov moffs32, EAX
        if i + 5 <= hi and data[i] == 0xA3:
            addr = u32(data, i + 1)
            if mem_lo <= addr <= mem_hi:
                hits.append((va, i, addr, 'MOV32_EAX', None, data[i:i + 5]))
            i += 5
            continue

        # mov [imm32], r8/r32: mod=00 r/m=101, any source register.
        if i + 6 <= hi and data[i] in (0x88, 0x89) and (data[i + 1] & 0xC7) == 0x05:
            addr = u32(data, i + 2)
            if mem_lo <= addr <= mem_hi:
                kind = 'MOV8_REG' if data[i] == 0x88 else 'MOV32_REG'
                hits.append((va, i, addr, kind, None, data[i:i + 6]))
            i += 6
            continue

        i += 1
    return hits


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe')
    ap.add_argument('-o', '--output')
    args = ap.parse_args()

    path = pathlib.Path(args.exe)
    data = path.read_bytes()
    image_base, sections = pe_info(data)
    text_lo, text_hi = text_bounds(sections)

    ranges = [
        ('WRITE_PLC_PROGRAM', 0x004B6A00, 0x004B7E20),
        ('WRITE_OR_CONFIG_A', 0x004BD5B0, 0x004BD6D0),
        ('WRITE_OR_CONFIG_B', 0x004BC7D0, 0x004BC920),
        ('PROGRAM_BUFFER_HELPERS', 0x004B02B0, 0x004B0470),
    ]
    tx_mem_lo = 0x004FA7A8
    tx_mem_hi = 0x004FA7C0

    sequences = [
        ('CLEAR_PROGRAM_0F_01_EF', bytes.fromhex('0F 01 EF')),
        ('CLEAR_ALL_MEMORY_0F_00_F0_DANGEROUS', bytes.fromhex('0F 00 F0')),
        ('CLEAR_DATA_0F_02_EE', bytes.fromhex('0F 02 EE')),
        ('CLEAR_RTC_0F_04_EC', bytes.fromhex('0F 04 EC')),
        ('WRITE_PRIMITIVE_PREFIX_09_03', bytes.fromhex('09 03')),
        ('READ_PROGRAM_PREFIX_34_03', bytes.fromhex('34 03')),
    ]

    lines = []
    lines.append('PC12 WRITE PLC PROGRAM - STATIC OFFLINE ANALYSIS')
    lines.append('=' * 100)
    lines.append('file=%s' % path.as_posix())
    lines.append('size=%d bytes' % len(data))
    lines.append('image_base=0x%08X' % image_base)
    lines.append('mode=OFFLINE ONLY; no serial port, no PLC, no TX')
    lines.append('important: encontrar 09 03 prova apenas uma primitiva estatica de escrita;')
    lines.append('           NAO prova que o download do programa use 09 sem correlacao direta.')
    lines.append('')

    lines.append('PE SECTIONS')
    lines.append('-' * 100)
    for name, rva, vsize, raw_ptr, raw_size in sections:
        lines.append('%-8s VA=0x%08X RVA=0x%08X VSIZE=0x%X RAW=0x%X RSIZE=0x%X' %
                     (name, image_base + rva, rva, vsize, raw_ptr, raw_size))
    lines.append('')

    lines.append('WRITE PLC PROGRAM STRING XREFS')
    lines.append('-' * 100)
    strings = list(printable_strings(data))
    write_strings = [(off, s) for off, s in strings if 'Write PLC Program' in s]
    if not write_strings:
        lines.append('string Write PLC Program nao encontrada')
    for soff, s in write_strings:
        sva = offset_to_va(sections, image_base, soff)
        lines.append('string file+0x%08X VA=%s: %s' %
                     (soff, '0x%08X' % sva if sva is not None else '?', s))
        if sva is not None and sva <= 0xFFFFFFFF and text_hi:
            ptr = struct.pack('<I', sva)
            xrefs = list(find_all(data, ptr, text_lo, text_hi))
            lines.append('  pointer xrefs=%d' % len(xrefs))
            for xoff in xrefs[:20]:
                xva = offset_to_va(sections, image_base, xoff)
                ctx = data[max(text_lo, xoff - 32):min(text_hi, xoff + 48)]
                lines.append('  XREF file+0x%08X VA=0x%08X context=[%s]' %
                             (xoff, xva or 0, hx(ctx)))
    lines.append('')

    lines.append('DIRECT STORES TO CANDIDATE SERIAL/TX GLOBAL 0x%08X..0x%08X' %
                 (tx_mem_lo, tx_mem_hi))
    lines.append('-' * 100)
    total_stores = 0
    for label, lo_va, hi_va in ranges:
        hits = scan_direct_global_stores(
            data, sections, image_base, lo_va, hi_va, tx_mem_lo, tx_mem_hi)
        lines.append('%s 0x%08X..0x%08X: %d direct store(s)' %
                     (label, lo_va, hi_va, len(hits)))
        total_stores += len(hits)
        for va, off, addr, kind, value, raw in hits:
            value_text = '' if value is None else ' value=0x%X' % value
            lines.append('  VA=0x%08X file+0x%08X -> [0x%08X] %s%s bytes=[%s]' %
                         (va, off, addr, kind, value_text, hx(raw)))
    if total_stores == 0:
        lines.append('Nenhum store absoluto simples ao intervalo candidato foi encontrado;')
        lines.append('isso nao exclui acesso via registrador/base/ponteiro ou outro buffer.')
    lines.append('')

    lines.append('STATIC BYTE-SEQUENCE OCCURRENCES')
    lines.append('-' * 100)
    for label, needle in sequences:
        poss = list(find_all(data, needle))
        lines.append('%s [%s]: %d occurrence(s)' % (label, hx(needle), len(poss)))
        for off in poss[:40]:
            va = offset_to_va(sections, image_base, off)
            ctx = data[max(0, off - 24):min(len(data), off + len(needle) + 32)]
            in_write = any(lo <= (va or -1) < hi for _name, lo, hi in ranges)
            lines.append('  file+0x%08X VA=%s target_range=%s context=[%s]' %
                         (off, '0x%08X' % va if va is not None else '?',
                          'yes' if in_write else 'no', hx(ctx)))
    lines.append('')

    lines.append('TARGET DISASSEMBLY WINDOWS')
    lines.append('-' * 100)
    for label, lo_va, hi_va in ranges:
        lines.append('### %s 0x%08X..0x%08X' % (label, lo_va, hi_va))
        # Para a rotina grande, amostra blocos estrategicos e o XREF conhecido
        # em vez de despejar milhares de linhas no artefato.
        windows = [(lo_va, min(lo_va + 0x280, hi_va))]
        if label == 'WRITE_PLC_PROGRAM':
            windows.extend([
                (0x004B6AA0, 0x004B6C20),
                (0x004B76C0, 0x004B79A0),
                (0x004B7C00, 0x004B7E20),
            ])
        for a, b in windows:
            lines.append('--- 0x%08X..0x%08X ---' % (a, b))
            for row in objdump_window(path, a, b):
                lines.append(row)
        lines.append('')

    lines.append('INTERPRETATION GUARDRAILS')
    lines.append('-' * 100)
    lines.append('* 0F 00 F0 permanece classificado como Clear All Memory perigoso e bloqueado.')
    lines.append('* 0F 01 EF/02 EE/04 EC sao candidatos estaticos de clear/preparacao por alvo.')
    lines.append('* 09 03 so pode ser promovido a download de programa se houver caminho estatico')
    lines.append('  direto entre Write PLC Program, montagem do quadro e envio serial correspondente.')
    lines.append('* Nenhum resultado deste script habilita TX de escrita no OpenLadder/PG Lab.')

    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')


if __name__ == '__main__':
    main()
