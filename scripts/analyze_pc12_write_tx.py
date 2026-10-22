#!/usr/bin/env python3
"""Isola, de forma estritamente OFFLINE, os envios do Write PLC Program.

O script NÃO abre COM, NÃO executa pc12.exe e NÃO transmite bytes. Ele apenas
varre o PE32 por CALL rel32 para a rotina de TX já identificada e mostra as
escritas estáticas ao buffer/contador de TX imediatamente anteriores.
"""

import argparse
import pathlib
import struct

from analyze_pc12_writeprog import (
    TX_ROUTINE,
    hx,
    i32,
    objdump_window,
    offset_to_va,
    pe_info,
    va_to_offset,
)

WRITE_LO = 0x004B6A00
WRITE_HI = 0x004B7E20
TX_BUF_LO = 0x004FA7A8
TX_BUF_HI = 0x004FA80F
TX_LEN = 0x004FA8AC
CONTEXT_BEFORE = 0xE0
CONTEXT_AFTER = 0x28
STATE_SETUP_LO = 0x004B76C0
STATE_SETUP_HI = 0x004B7958
FRAME_BUILDER_LO = 0x004B7958
FRAME_BUILDER_HI = 0x004B7A20


def u32(data, off):
    return struct.unpack_from('<I', data, off)[0]


def scan_calls_to(data, sections, image_base, lo_va, hi_va, target_va):
    lo = va_to_offset(sections, image_base, lo_va)
    hi_last = va_to_offset(sections, image_base, hi_va - 1)
    if lo is None or hi_last is None:
        return []
    hi = hi_last + 1
    out = []
    i = lo
    while i + 5 <= hi:
        if data[i] == 0xE8:
            va = offset_to_va(sections, image_base, i)
            if va is not None:
                target = (va + 5 + i32(data, i + 1)) & 0xFFFFFFFF
                if target == target_va:
                    out.append((va, i, data[i:i + 5]))
            i += 5
        else:
            i += 1
    return out


def is_tx_addr(addr):
    return TX_BUF_LO <= addr <= TX_BUF_HI or addr == TX_LEN


def scan_abs_stores(data, sections, image_base, lo_va, hi_va):
    """Reconhece formas simples de MOV absoluto usadas pelo PC12.

    Resultado: (va, address, kind, value-or-None, raw-bytes).
    """
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

        if i + 7 <= hi and data[i:i + 2] == b'\xC6\x05':
            addr = u32(data, i + 2)
            if is_tx_addr(addr):
                out.append((va, addr, 'MOV8_IMM', data[i + 6], data[i:i + 7]))
            i += 7
            continue

        if i + 10 <= hi and data[i:i + 2] == b'\xC7\x05':
            addr = u32(data, i + 2)
            if is_tx_addr(addr):
                out.append((va, addr, 'MOV32_IMM', u32(data, i + 6), data[i:i + 10]))
            i += 10
            continue

        if i + 5 <= hi and data[i] == 0xA2:
            addr = u32(data, i + 1)
            if is_tx_addr(addr):
                out.append((va, addr, 'MOV8_AL', None, data[i:i + 5]))
            i += 5
            continue

        if i + 5 <= hi and data[i] == 0xA3:
            addr = u32(data, i + 1)
            if is_tx_addr(addr):
                out.append((va, addr, 'MOV32_EAX', None, data[i:i + 5]))
            i += 5
            continue

        if i + 6 <= hi and data[i] in (0x88, 0x89) and (data[i + 1] & 0xC7) == 0x05:
            addr = u32(data, i + 2)
            if is_tx_addr(addr):
                kind = 'MOV8_REG' if data[i] == 0x88 else 'MOV32_REG'
                out.append((va, addr, kind, None, data[i:i + 6]))
            i += 6
            continue

        i += 1
    return out


def fmt_addr(addr):
    if addr == TX_LEN:
        return 'TX_LEN[0x%08X]' % addr
    if TX_BUF_LO <= addr <= TX_BUF_HI:
        return 'TX[%02X][0x%08X]' % (addr - TX_BUF_LO, addr)
    return '0x%08X' % addr


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe')
    ap.add_argument('-o', '--output')
    args = ap.parse_args()

    path = pathlib.Path(args.exe)
    data = path.read_bytes()
    image_base, sections = pe_info(data)

    calls = scan_calls_to(data, sections, image_base, WRITE_LO, WRITE_HI, TX_ROUTINE)
    builder_stores = scan_abs_stores(
        data, sections, image_base, FRAME_BUILDER_LO, FRAME_BUILDER_HI)

    lines = []
    lines.append('PC12 WRITE PLC PROGRAM - FOCUSED TX STATIC ANALYSIS')
    lines.append('=' * 100)
    lines.append('mode=OFFLINE ONLY; no serial port, no PLC, no TX')
    lines.append('write_range=0x%08X..0x%08X' % (WRITE_LO, WRITE_HI))
    lines.append('tx_routine=0x%08X' % TX_ROUTINE)
    lines.append('tx_buffer=0x%08X..0x%08X tx_len=0x%08X' %
                 (TX_BUF_LO, TX_BUF_HI, TX_LEN))
    lines.append('direct_tx_calls=%d' % len(calls))
    lines.append('')

    if not calls:
        lines.append('Nenhum CALL rel32 direto para a rotina TX foi encontrado na faixa Write PLC Program.')
    else:
        lines.append('DIRECT TX CALL SITES')
        lines.append('-' * 100)
        for idx, (call_va, off, raw) in enumerate(calls, 1):
            lines.append('%02d. callsite=0x%08X file+0x%08X bytes=[%s]' %
                         (idx, call_va, off, hx(raw)))
        lines.append('')

    lines.append('FRAME BUILDER 0x33 - DIRECT TX STORES')
    lines.append('-' * 100)
    lines.append('window=0x%08X..0x%08X stores=%d' %
                 (FRAME_BUILDER_LO, FRAME_BUILDER_HI, len(builder_stores)))
    for va, addr, kind, value, raw in builder_stores:
        value_text = '?' if value is None else '0x%X' % value
        lines.append('  0x%08X  %-20s %-10s value=%s bytes=[%s]' %
                     (va, fmt_addr(addr), kind, value_text, hx(raw)))
    lines.append('')
    lines.append('STATIC FRAME SKELETON')
    lines.append('  TX[0] = 0x33 is an immediate constant in the Write PLC Program builder.')
    lines.append('  TX[1] is computed from object field +0x56; TX[2] = 0x00.')
    lines.append('  TX[3] comes from object field +0x6A; TX[4] from +0x6E; TX[5] from +0x56.')
    lines.append('  Bytes from object buffer +0xE0 are copied into TX starting at index held in +0x5E.')
    lines.append('  A checksum byte is appended and TX_LEN is set to payload_length + 1.')
    lines.append('  This establishes a 0x33 family frame in the write path, not yet its semantic name.')
    lines.append('')

    lines.append('PRE-TX BUFFER/LENGTH STORES')
    lines.append('-' * 100)
    for idx, (call_va, _off, _raw) in enumerate(calls, 1):
        lo = max(WRITE_LO, call_va - CONTEXT_BEFORE)
        stores = scan_abs_stores(data, sections, image_base, lo, call_va)
        lines.append('TX#%02d callsite=0x%08X window=0x%08X..0x%08X stores=%d' %
                     (idx, call_va, lo, call_va, len(stores)))
        for va, addr, kind, value, raw in stores:
            value_text = '?' if value is None else '0x%X' % value
            lines.append('  0x%08X  %-20s %-10s value=%s bytes=[%s]' %
                         (va, fmt_addr(addr), kind, value_text, hx(raw)))
        if not stores:
            lines.append('  (nenhum MOV absoluto simples ao TX no recorte)')
    lines.append('')

    lines.append('OBJECT/CHUNK STATE SETUP BEFORE 0x33 BUILDER')
    lines.append('-' * 100)
    lines.append('disasm 0x%08X..0x%08X' % (STATE_SETUP_LO, STATE_SETUP_HI))
    lines.extend(objdump_window(path, STATE_SETUP_LO, STATE_SETUP_HI, max_lines=700))
    lines.append('')

    lines.append('FRAME BUILDER OBJDUMP')
    lines.append('-' * 100)
    lines.extend(objdump_window(path, FRAME_BUILDER_LO, FRAME_BUILDER_HI, max_lines=300))
    lines.append('')

    lines.append('FOCUSED OBJDUMP WINDOWS')
    lines.append('-' * 100)
    for idx, (call_va, _off, _raw) in enumerate(calls, 1):
        a = max(WRITE_LO, call_va - 0x70)
        b = min(WRITE_HI, call_va + CONTEXT_AFTER)
        lines.append('### TX#%02d 0x%08X ; disasm 0x%08X..0x%08X' %
                     (idx, call_va, a, b))
        lines.extend(objdump_window(path, a, b, max_lines=120))
        lines.append('')

    lines.append('STATIC INTERPRETATION')
    lines.append('-' * 100)
    lines.append('* CALL direto Write PLC Program -> rotina TX é correlação estática forte do caminho de envio.')
    lines.append('* Os vários CALLs consecutivos são cercados por testes de flags de comunicação; não devem ser contados como 15 quadros distintos sem execução do fluxo.')
    lines.append('* O construtor 0x33 e o cálculo de checksum estão no mesmo caminho que antecede os CALLs de TX.')
    lines.append('* O conteúdo mostrado antes de cada CALL continua sendo estático: branches podem selecionar caminhos diferentes.')
    lines.append('* Nenhuma ocorrência 09/0F deve ser tratada como download completo sem reconstruir a ordem efetiva dos quadros.')
    lines.append('* 0F 00 F0 (Clear All Memory) continua bloqueado e este script nunca executa TX.')

    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')


if __name__ == '__main__':
    main()
