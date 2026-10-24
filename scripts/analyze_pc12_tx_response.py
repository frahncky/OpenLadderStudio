#!/usr/bin/env python3
"""Analisa estaticamente a rotina de comunicação/TX 0x0046F5E6 do PC12.

Objetivo: localizar onde a rotina preenche o buffer RX, o comprimento recebido e
as três flags consumidas pelo caminho PG33 (timeout, erro e checksum). O script
é estritamente OFFLINE: lê pc12.exe como dados e usa objdump; não executa o
programa, não abre COM e não transmite bytes.
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

ROUTINE_LO = 0x0046F5E6
ROUTINE_HI = 0x00470000

GLOBALS = {
    0x004FA7A8: 'TX_BUF',
    0x004FA8AC: 'TX_LEN',
    0x00530230: 'RX_BUF',
    0x004FA8B0: 'RX_LEN',
    0x004FA8B7: 'F_TIMEOUT',
    0x004FA8B8: 'F_ERROR',
    0x004FA8B9: 'F_CHECKSUM',
}


def u32(data, off):
    return struct.unpack_from('<I', data, off)[0]


def scan_global_refs(data, sections, image_base):
    lo = va_to_offset(sections, image_base, ROUTINE_LO)
    hi_last = va_to_offset(sections, image_base, ROUTINE_HI - 1)
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

        # moffs8/moffs32: A0/A1 read, A2/A3 write
        if i + 5 <= hi and data[i] in (0xA0, 0xA1, 0xA2, 0xA3):
            addr = u32(data, i + 1)
            if addr in GLOBALS:
                op = {0xA0: 'READ8', 0xA1: 'READ32', 0xA2: 'WRITE8', 0xA3: 'WRITE32'}[data[i]]
                out.append((va, addr, op, data[i:i + 5]))
            i += 5
            continue

        # MOV r, [abs] / MOV [abs], r using mod=00 r/m=101
        if i + 6 <= hi and data[i] in (0x88, 0x89, 0x8A, 0x8B) and (data[i + 1] & 0xC7) == 0x05:
            addr = u32(data, i + 2)
            if addr in GLOBALS:
                op = {
                    0x88: 'WRITE8_REG', 0x89: 'WRITE32_REG',
                    0x8A: 'READ8_REG', 0x8B: 'READ32_REG',
                }[data[i]]
                out.append((va, addr, op, data[i:i + 6]))
            i += 6
            continue

        # MOV byte/dword [abs], immediate
        if i + 7 <= hi and data[i:i + 2] == b'\xC6\x05':
            addr = u32(data, i + 2)
            if addr in GLOBALS:
                out.append((va, addr, 'WRITE8_IMM_%02X' % data[i + 6], data[i:i + 7]))
            i += 7
            continue
        if i + 10 <= hi and data[i:i + 2] == b'\xC7\x05':
            addr = u32(data, i + 2)
            if addr in GLOBALS:
                val = u32(data, i + 6)
                out.append((va, addr, 'WRITE32_IMM_%08X' % val, data[i:i + 10]))
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
    refs = scan_global_refs(data, sections, image_base)

    lines = []
    lines.append('PC12 TX/RESPONSE ROUTINE - STATIC OFFLINE ANALYSIS')
    lines.append('=' * 96)
    lines.append('range=0x%08X..0x%08X' % (ROUTINE_LO, ROUTINE_HI))
    lines.append('mode=OFFLINE ONLY; no execution, no COM, no PLC, no TX')
    lines.append('')

    lines.append('GLOBAL REFERENCES')
    lines.append('-' * 96)
    if not refs:
        lines.append('Nenhuma referência absoluta simples aos globais conhecidos foi localizada.')
    for va, addr, op, raw in refs:
        lines.append('0x%08X  %-16s %-20s bytes=[%s]' %
                     (va, GLOBALS[addr], op, hx(raw)))
    lines.append('')

    lines.append('FOCUSED DISASSEMBLY AROUND GLOBAL REFERENCES')
    lines.append('-' * 96)
    seen = set()
    for va, addr, op, _raw in refs:
        key = (max(ROUTINE_LO, va - 0x40), min(ROUTINE_HI, va + 0x70))
        if key in seen:
            continue
        seen.add(key)
        lines.append('### around 0x%08X (%s %s)' % (va, GLOBALS[addr], op))
        lines.extend(objdump_window(path, key[0], key[1], max_lines=180))
        lines.append('')

    lines.append('FULL ROUTINE WINDOW')
    lines.append('-' * 96)
    lines.extend(objdump_window(path, ROUTINE_LO, ROUTINE_HI, max_lines=1800))
    lines.append('')

    lines.append('INTERPRETATION GUARDRAILS')
    lines.append('-' * 96)
    lines.append('* Escrita em F_TIMEOUT/F_ERROR/F_CHECKSUM mostra apenas estado interno da rotina.')
    lines.append('* Uma comparação com RX_BUF pode ajudar a reconstruir o ACK, mas não constitui validação física.')
    lines.append('* Nenhum payload de resposta deve ser inventado sem fluxo de dados explícito ou captura.')
    lines.append('* Este script nunca chama a rotina 0x0046F5E6; apenas examina os bytes do executável.')

    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')


if __name__ == '__main__':
    main()
