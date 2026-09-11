#!/usr/bin/env python3
"""Emula OFFLINE o gate de resposta/retry do PG33 no PC12.

Escopo: 0x004B7A07..0x004B7C50, imediatamente depois de o quadro 0x33 já
estar montado. A rotina real 0x0046F5E6 é interceptada antes de qualquer I/O e
substituída somente pelas três flags que ela entrega ao chamador:

  0x4FA8B7 timeout
  0x4FA8B9 checksum
  0x4FA8B8 error/status bit7

Objetivos:
- contar quantas tentativas o chamador faz;
- demonstrar que uma resposta validada faz as chamadas restantes serem puladas;
- verificar estaticamente que o chamador desta faixa não lê RX_BUF/RX_LEN
  diretamente, isto é, depende das flags genéricas da rotina de comunicação.

Nenhuma COM, API serial, rotina TX real ou PLC é acessada.
"""

import argparse
import pathlib
import struct
import sys

try:
    from unicorn import Uc, UcError, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE
    from unicorn.x86_const import UC_X86_REG_EDI, UC_X86_REG_EIP, UC_X86_REG_ESP
except ImportError:
    print('ERRO: unicorn não instalado. Use: pip install unicorn==2.1.4', file=sys.stderr)
    raise

from emulate_pc12_pg33_builder import load_pe, map_image
from emulate_pc12_pg33_operand_helper import return_from_cdecl
from analyze_pc12_writeprog import pe_info, va_to_offset, offset_to_va

EXE_DEFAULT = 'src/OpenLadderStudio.Desktop/pc12.exe'
ENTRY = 0x004B7A07
STOP = 0x004B7C50
TX_ROUTINE = 0x0046F5E6
CLOCK_HELPER = 0x004CBE48

F_TIMEOUT = 0x004FA8B7
F_ERROR = 0x004FA8B8
F_CHECKSUM = 0x004FA8B9
RX_BUF = 0x00530230
RX_LEN = 0x004FA8B0

OBJ = 0x39000000
STACK = 0x3A000000


def scan_direct_rx_refs(data, sections, image_base):
    lo = va_to_offset(sections, image_base, ENTRY)
    hi_last = va_to_offset(sections, image_base, STOP - 1)
    if lo is None or hi_last is None:
        return []
    hi = hi_last + 1
    out = []
    for target, label in ((RX_BUF, 'RX_BUF'), (RX_LEN, 'RX_LEN')):
        needle = struct.pack('<I', target)
        pos = data.find(needle, lo, hi)
        while pos != -1:
            va = offset_to_va(sections, image_base, pos)
            out.append((va, label))
            pos = data.find(needle, pos + 1, hi)
    return sorted(out)


def emulate(exe, outcomes):
    """outcomes: lista de triplas (timeout, checksum, error).

    Depois do fim da lista, repete a última tripla.
    """
    data, image_base, sections = load_pe(exe)
    direct_rx_refs = scan_direct_rx_refs(data, sections, image_base)

    mu = Uc(UC_ARCH_X86, UC_MODE_32)
    map_image(mu, data, image_base, sections)
    mu.mem_map(OBJ, 0x1000)
    mu.mem_map(STACK - 0x10000, 0x20000)
    mu.reg_write(UC_X86_REG_EDI, OBJ)
    mu.reg_write(UC_X86_REG_ESP, STACK)

    state = {'calls': 0, 'clock': 0}

    def hook(uc, address, size, _user):
        if address == CLOCK_HELPER:
            state['clock'] += 1
            return_from_cdecl(uc, 0x00100000)
            return
        if address == TX_ROUTINE:
            idx = min(state['calls'], len(outcomes) - 1)
            timeout, checksum, error = outcomes[idx]
            uc.mem_write(F_TIMEOUT, bytes([timeout]))
            uc.mem_write(F_CHECKSUM, bytes([checksum]))
            uc.mem_write(F_ERROR, bytes([error]))
            state['calls'] += 1
            return_from_cdecl(uc, 0)
            return

    mu.hook_add(UC_HOOK_CODE, hook)
    try:
        mu.emu_start(ENTRY, STOP, count=100000)
    except UcError as exc:
        raise RuntimeError('Unicorn: %s EIP=0x%08X' % (exc, mu.reg_read(UC_X86_REG_EIP)))

    if mu.reg_read(UC_X86_REG_EIP) != STOP:
        raise RuntimeError('não alcançou 0x%08X' % STOP)

    return {
        'calls': state['calls'],
        'clock': state['clock'],
        'timeout': bytes(mu.mem_read(F_TIMEOUT, 1))[0],
        'checksum': bytes(mu.mem_read(F_CHECKSUM, 1))[0],
        'error': bytes(mu.mem_read(F_ERROR, 1))[0],
        'direct_rx_refs': direct_rx_refs,
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe', nargs='?', default=EXE_DEFAULT)
    ap.add_argument('-o', '--output')
    args = ap.parse_args()

    success = (0, 0, 0)
    timeout = (1, 0, 0)
    badsum = (0, 1, 0)
    status_error = (0, 0, 1)

    fixtures = [
        ('immediate-success', [success], 1, success),
        ('timeout-then-success', [timeout, success], 2, success),
        ('checksum-then-success', [badsum, success], 2, success),
        ('status-error-then-success', [status_error, success], 2, success),
        ('all-timeout', [timeout], 15, timeout),
        ('all-checksum-error', [badsum], 15, badsum),
        ('all-status-error', [status_error], 15, status_error),
    ]

    rows = []
    overall = True
    for name, outcomes, expected_calls, expected_flags in fixtures:
        row = emulate(args.exe, outcomes)
        row['name'] = name
        row['expected_calls'] = expected_calls
        row['expected_flags'] = expected_flags
        got_flags = (row['timeout'], row['checksum'], row['error'])
        row['ok'] = (row['calls'] == expected_calls and got_flags == expected_flags
                     and len(row['direct_rx_refs']) == 0)
        rows.append(row)
        overall &= row['ok']

    lines = []
    lines.append('PC12 PG33 RETRY/ACK GATE - UNICORN OFFLINE EMULATION')
    lines.append('=' * 100)
    lines.append('range=0x%08X..0x%08X tx_hook=0x%08X' % (ENTRY, STOP, TX_ROUTINE))
    lines.append('mode=OFFLINE; TX routine replaced by synthetic generic flags; no COM/PLC')
    lines.append('')
    for row in rows:
        flags = (row['timeout'], row['checksum'], row['error'])
        lines.append('%s: %-28s calls=%d expected=%d final_flags=%r' %
                     ('OK' if row['ok'] else 'FAIL', row['name'], row['calls'],
                      row['expected_calls'], flags))
    lines.append('')
    refs = rows[0]['direct_rx_refs'] if rows else []
    lines.append('direct RX_BUF/RX_LEN references in caller retry range: %d' % len(refs))
    for va, label in refs:
        lines.append('  0x%08X -> %s' % (va, label))
    lines.append('')
    lines.append('RESULT=' + ('PASS' if overall else 'FAIL'))
    if overall:
        lines.append('O chamador PG33 aceita sucesso quando timeout/checksum/error estão todos zerados.')
        lines.append('Falha em qualquer uma das três flags provoca nova tentativa, até o máximo observado de 15 chamadas.')
        lines.append('Nesta faixa o chamador não lê RX_BUF/RX_LEN diretamente; o conteúdo específico do ACK é abstraído pela rotina genérica.')
        lines.append('Assim, o payload físico exato do ACK não pode ser deduzido deste chamador apenas pelas condições de sucesso.')
    lines.append('Nenhum byte foi transmitido fora do Unicorn.')

    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')
    return 0 if overall else 1


if __name__ == '__main__':
    raise SystemExit(main())
