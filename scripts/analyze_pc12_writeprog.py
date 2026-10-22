#!/usr/bin/env python3
"""Análise estática OFFLINE do caminho Write PLC Program do PC12.

Não abre porta serial, não executa o PC12 e não transmite qualquer quadro.
O objetivo é localizar construtores de quadros, chamadas de envio e acessos ao
buffer serial no executável original, mantendo separados fato estático,
hipótese e protocolo fisicamente confirmado.
"""

import argparse
import pathlib
import re
import shutil
import struct
import subprocess


TX_ROUTINE = 0x0046F5E6
TX_MEM_LO = 0x004FA7A8
TX_MEM_HI = 0x004FA7C0

TARGET_RANGES = [
    ('WRITE_PLC_PROGRAM', 0x004B6A00, 0x004B7E20),
    ('WRITE_OR_CONFIG_A', 0x004BD5B0, 0x004BD6D0),
    ('WRITE_OR_CONFIG_B', 0x004BC7D0, 0x004BC920),
    ('PROGRAM_BUFFER_HELPERS', 0x004B02B0, 0x004B0470),
]


def u16(data, off):
    return struct.unpack_from('<H', data, off)[0]


def u32(data, off):
    return struct.unpack_from('<I', data, off)[0]


def i32(data, off):
    return struct.unpack_from('<i', data, off)[0]


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


def text_va_bounds(sections, image_base):
    for name, rva, vsize, _raw_ptr, raw_size in sections:
        if name == '.text':
            return image_base + rva, image_base + rva + max(vsize, raw_size)
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


def range_label(va):
    for label, lo, hi in TARGET_RANGES:
        if lo <= va < hi:
            return label
    return '-'


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


def scan_rel32_calls(data, sections, image_base, lo_va, hi_va):
    """Varre CALL rel32 (E8 disp32) numa faixa de VA.

    O executável é x86 PE32. Este scanner é propositalmente simples: ele não
    tenta desassemblar fluxo completo; apenas registra candidatos E8 cujo alvo
    também cai na imagem mapeada. Falsos positivos em bytes de dados embutidos
    continuam possíveis e são tratados como candidatos estáticos.
    """
    lo = va_to_offset(sections, image_base, lo_va)
    hi_last = va_to_offset(sections, image_base, hi_va - 1)
    if lo is None or hi_last is None:
        return []
    hi = hi_last + 1
    out = []
    i = lo
    while i + 5 <= hi:
        if data[i] != 0xE8:
            i += 1
            continue
        call_va = offset_to_va(sections, image_base, i)
        if call_va is None:
            i += 1
            continue
        target = (call_va + 5 + i32(data, i + 1)) & 0xFFFFFFFF
        target_off = va_to_offset(sections, image_base, target)
        if target_off is not None:
            out.append((call_va, target, i, data[i:i + 5]))
        i += 5
    return out


def callers_of(data, sections, image_base, target_va):
    lo, hi = text_va_bounds(sections, image_base)
    if not lo or not hi:
        return []
    return [row for row in scan_rel32_calls(data, sections, image_base, lo, hi)
            if row[1] == target_va]


def calls_from_range(data, sections, image_base, lo_va, hi_va):
    return scan_rel32_calls(data, sections, image_base, lo_va, hi_va)


def callsites_into_range(data, sections, image_base, target_lo, target_hi):
    lo, hi = text_va_bounds(sections, image_base)
    if not lo or not hi:
        return []
    return [row for row in scan_rel32_calls(data, sections, image_base, lo, hi)
            if target_lo <= row[1] < target_hi]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe')
    ap.add_argument('-o', '--output')
    args = ap.parse_args()

    path = pathlib.Path(args.exe)
    data = path.read_bytes()
    image_base, sections = pe_info(data)
    text_lo, text_hi = text_bounds(sections)

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
    lines.append('tx_routine_candidate=0x%08X' % TX_ROUTINE)
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
                 (TX_MEM_LO, TX_MEM_HI))
    lines.append('-' * 100)
    total_stores = 0
    for label, lo_va, hi_va in TARGET_RANGES:
        hits = scan_direct_global_stores(
            data, sections, image_base, lo_va, hi_va, TX_MEM_LO, TX_MEM_HI)
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

    lines.append('REL32 CALL GRAPH AROUND WRITE PLC PROGRAM')
    lines.append('-' * 100)
    tx_callers = callers_of(data, sections, image_base, TX_ROUTINE)
    lines.append('Direct CALL rel32 sites -> TX routine 0x%08X: %d candidate(s)' %
                 (TX_ROUTINE, len(tx_callers)))
    for call_va, target, off, raw in tx_callers[:120]:
        lines.append('  CALLSITE=0x%08X target=0x%08X source_range=%s file+0x%08X bytes=[%s]' %
                     (call_va, target, range_label(call_va), off, hx(raw)))

    write_lo = TARGET_RANGES[0][1]
    write_hi = TARGET_RANGES[0][2]
    write_calls = calls_from_range(data, sections, image_base, write_lo, write_hi)
    lines.append('')
    lines.append('CALL rel32 candidates FROM WRITE_PLC_PROGRAM 0x%08X..0x%08X: %d' %
                 (write_lo, write_hi, len(write_calls)))
    for call_va, target, off, raw in write_calls[:260]:
        marker = ''
        if target == TX_ROUTINE:
            marker = ' *** DIRECT_TX ***'
        elif range_label(target) != '-':
            marker = ' -> %s' % range_label(target)
        lines.append('  0x%08X -> 0x%08X%s file+0x%08X bytes=[%s]' %
                     (call_va, target, marker, off, hx(raw)))

    tx_source_vas = {row[0] for row in tx_callers}
    tx_caller_targets = {row[1] for row in write_calls if row[1] in tx_source_vas}
    # O conjunto acima só detecta caso o alvo da chamada coincida exatamente
    # com um callsite TX. Também registramos a proximidade, útil para funções
    # cujo início antecede o E8 de envio por poucos bytes.
    lines.append('')
    lines.append('TWO-HOP TX CORRELATION CANDIDATES')
    direct = [row for row in write_calls if row[1] == TX_ROUTINE]
    if direct:
        for row in direct:
            lines.append('  direct: WRITE range callsite 0x%08X -> TX 0x%08X' %
                         (row[0], TX_ROUTINE))
    else:
        lines.append('  no direct WRITE-range -> TX rel32 call found')
    if tx_caller_targets:
        for target in sorted(tx_caller_targets):
            lines.append('  exact two-hop candidate: WRITE range -> 0x%08X, and that VA is a TX callsite' % target)
    near_hits = []
    for call_va, target, _off, _raw in write_calls:
        for tx_site, _tx_target, _tx_off, _tx_raw in tx_callers:
            delta = tx_site - target
            if 0 <= delta <= 0x180:
                near_hits.append((call_va, target, tx_site, delta))
    if near_hits:
        lines.append('  bounded proximity candidates (callee target <= 0x180 before TX callsite):')
        for call_va, target, tx_site, delta in near_hits[:120]:
            lines.append('    WRITE callsite 0x%08X -> 0x%08X ; TX callsite 0x%08X (+0x%X)' %
                         (call_va, target, tx_site, delta))
    else:
        lines.append('  no bounded proximity candidate found')

    lines.append('')
    lines.append('CALLS INTO WRITE/CONFIG RANGES FROM ALL .text')
    for label, lo_va, hi_va in TARGET_RANGES:
        inbound = callsites_into_range(data, sections, image_base, lo_va, hi_va)
        lines.append('%s 0x%08X..0x%08X: %d inbound CALL candidate(s)' %
                     (label, lo_va, hi_va, len(inbound)))
        for call_va, target, off, raw in inbound[:120]:
            lines.append('  0x%08X [%s] -> 0x%08X file+0x%08X bytes=[%s]' %
                         (call_va, range_label(call_va), target, off, hx(raw)))
    lines.append('')

    lines.append('STATIC BYTE-SEQUENCE OCCURRENCES')
    lines.append('-' * 100)
    for label, needle in sequences:
        poss = list(find_all(data, needle))
        lines.append('%s [%s]: %d occurrence(s)' % (label, hx(needle), len(poss)))
        for off in poss[:40]:
            va = offset_to_va(sections, image_base, off)
            ctx = data[max(0, off - 24):min(len(data), off + len(needle) + 32)]
            in_write = any(lo <= (va or -1) < hi for _name, lo, hi in TARGET_RANGES)
            lines.append('  file+0x%08X VA=%s target_range=%s context=[%s]' %
                         (off, '0x%08X' % va if va is not None else '?',
                          'yes' if in_write else 'no', hx(ctx)))
    lines.append('')

    lines.append('TARGET DISASSEMBLY WINDOWS')
    lines.append('-' * 100)
    for label, lo_va, hi_va in TARGET_RANGES:
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
    lines.append('* CALL rel32 e proximidade de enderecos sao evidencias estaticas, nao prova dinamica.')
    lines.append('* 0F 00 F0 permanece classificado como Clear All Memory perigoso e bloqueado.')
    lines.append('* 0F 01 EF/02 EE/04 EC sao candidatos estaticos de clear/preparacao por alvo.')
    lines.append('* 09 03 so pode ser promovido a download de programa se houver caminho estatico')
    lines.append('  e depois dinamico/offline entre Write PLC Program, montagem do quadro e envio.')
    lines.append('* Nenhum resultado deste script habilita TX de escrita no OpenLadder/PG Lab.')

    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')


if __name__ == '__main__':
    main()
