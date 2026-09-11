#!/usr/bin/env python3
"""Executa OFFLINE o helper 0x004BCA65 do PC12 para operandos isolados.

O helper é chamado pelo coletor Write PLC Program para acrescentar as palavras
de máquina posteriores à primeira em instruções de StepSpan > 1. Este ensaio
injeta somente texto de operando no objeto interno, executa o código original
no Unicorn e captura HIGH/LOW/EXTERNAL antes de qualquer transmissão.

Wrappers de biblioteca do PC12 que saltam para a CRT carregada pelo Windows
(strcpy/sprintf) são interceptados e reproduzidos localmente. A lógica de
codificação TP02 continua sendo executada pelo código original do PC12.

Nenhuma COM, API de serial, rotina TX ou PLC é acessada.
"""

import argparse
import re
import struct
import sys

try:
    from unicorn import (
        Uc, UcError, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE,
        UC_HOOK_MEM_INVALID,
    )
    from unicorn.x86_const import UC_X86_REG_EAX, UC_X86_REG_EIP, UC_X86_REG_ESP
except ImportError:
    print('ERRO: unicorn não instalado. Use: pip install unicorn==2.1.4', file=sys.stderr)
    raise

from emulate_pc12_pg33_builder import load_pe, map_image

EXE_DEFAULT = 'src/OpenLadderStudio.Desktop/pc12.exe'
HELPER = 0x004BCA65
PC12_STRCPY = 0x004CC174
PC12_SPRINTF = 0x004CBF90
TX_BUF = 0x004FA7A8

OBJ = 0x29000000
STACK = 0x2A000000
STOP = 0x2B000000


def put32(mu, addr, value):
    mu.mem_write(addr, struct.pack('<I', value & 0xFFFFFFFF))


def get32(mu, addr):
    return struct.unpack('<I', bytes(mu.mem_read(addr, 4)))[0]


def get8(mu, addr):
    return bytes(mu.mem_read(addr, 1))[0]


def read_c_string(mu, addr, limit=1024):
    out = bytearray()
    for i in range(limit):
        b = bytes(mu.mem_read(addr + i, 1))[0]
        if b == 0:
            return bytes(out)
        out.append(b)
    raise RuntimeError('string C sem terminador em 0x%08X' % addr)


def return_from_cdecl(mu, eax_value=0):
    """Retorna de uma função cdecl interceptada; o chamador limpa os argumentos."""
    sp = mu.reg_read(UC_X86_REG_ESP)
    ret = get32(mu, sp)
    mu.reg_write(UC_X86_REG_ESP, sp + 4)
    mu.reg_write(UC_X86_REG_EAX, eax_value & 0xFFFFFFFF)
    mu.reg_write(UC_X86_REG_EIP, ret)


def emulate_strcpy(mu):
    sp = mu.reg_read(UC_X86_REG_ESP)
    dest = get32(mu, sp + 4)
    src = get32(mu, sp + 8)
    raw = read_c_string(mu, src)
    mu.mem_write(dest, raw + b'\x00')
    return_from_cdecl(mu, dest)


def format_one_integer(fmt_raw, value):
    """Subconjunto de sprintf usado no helper: um inteiro decimal/hexadecimal."""
    fmt = fmt_raw.decode('ascii', errors='strict')
    # Remove modificadores C que o operador % do Python não reconhece.
    normalized = re.sub(r'%(?P<flags>[-+ #0]*)(?P<width>\d*)(?P<prec>\.\d+)?[hlL]+(?P<conv>[diuoxX])',
                        r'%\g<flags>\g<width>\g<prec>\g<conv>', fmt)
    # Python não possui %u distinto; para nossos inteiros positivos, %d é equivalente.
    normalized = re.sub(r'%(?P<flags>[-+ #0]*)(?P<width>\d*)(?P<prec>\.\d+)?u',
                        r'%\g<flags>\g<width>\g<prec>d', normalized)
    try:
        return (normalized % int(value)).encode('ascii')
    except Exception as exc:
        raise RuntimeError('formato sprintf não suportado %r: %s' % (fmt, exc))


def emulate_sprintf(mu):
    sp = mu.reg_read(UC_X86_REG_ESP)
    dest = get32(mu, sp + 4)
    fmt_ptr = get32(mu, sp + 8)
    value = get32(mu, sp + 12)
    fmt_raw = read_c_string(mu, fmt_ptr)
    out = format_one_integer(fmt_raw, value)
    mu.mem_write(dest, out + b'\x00')
    return_from_cdecl(mu, len(out))
    return fmt_raw, out


def emulate(exe, operand, init14a=0, init14e=0, init14f=0):
    data, image_base, sections = load_pe(exe)
    mu = Uc(UC_ARCH_X86, UC_MODE_32)
    map_image(mu, data, image_base, sections)
    mu.mem_map(OBJ, 0x20000)
    mu.mem_map(STACK - 0x10000, 0x20000)
    mu.mem_map(STOP, 0x1000)

    put32(mu, OBJ + 0x5E, 6)
    put32(mu, OBJ + 0x62, 0)
    put32(mu, OBJ + 0x56, 0)
    mu.mem_write(OBJ + 0x14A, bytes([init14a & 0xFF]))
    mu.mem_write(OBJ + 0x14E, bytes([init14e & 0xFF]))
    mu.mem_write(OBJ + 0x14F, bytes([init14f & 0xFF]))

    raw = operand.encode('ascii') + b'\x00'
    if len(raw) > 64:
        raise ValueError('operando longo demais')
    mu.mem_write(OBJ + 0x12A1, raw)

    esp = STACK
    mu.mem_write(esp, struct.pack('<II', STOP, OBJ))
    mu.reg_write(UC_X86_REG_ESP, esp)

    state = {
        'returned': False,
        'steps': 0,
        'last_eip': HELPER,
        'invalid': None,
        'strcpy_calls': 0,
        'sprintf_calls': [],
        'fatal': None,
    }

    def hook(uc, address, size, _user):
        state['steps'] += 1
        state['last_eip'] = address
        if address == STOP:
            state['returned'] = True
            uc.emu_stop()
            return
        if address == PC12_STRCPY:
            try:
                emulate_strcpy(uc)
                state['strcpy_calls'] += 1
            except Exception as exc:
                state['fatal'] = 'strcpy interceptado: ' + str(exc)
                uc.emu_stop()
            return
        if address == PC12_SPRINTF:
            try:
                fmt, out = emulate_sprintf(uc)
                state['sprintf_calls'].append((fmt, out))
            except Exception as exc:
                state['fatal'] = 'sprintf interceptado: ' + str(exc)
                uc.emu_stop()
            return

    def invalid_hook(uc, access, address, size, value, _user):
        state['invalid'] = {
            'access': access,
            'address': address,
            'size': size,
            'value': value,
            'eip': uc.reg_read(UC_X86_REG_EIP),
            'esp': uc.reg_read(UC_X86_REG_ESP),
        }
        return False

    mu.hook_add(UC_HOOK_CODE, hook)
    mu.hook_add(UC_HOOK_MEM_INVALID, invalid_hook)
    try:
        mu.emu_start(HELPER, STOP, count=500000)
    except UcError as exc:
        eip = mu.reg_read(UC_X86_REG_EIP)
        esp_now = mu.reg_read(UC_X86_REG_ESP)
        if state['invalid'] is not None:
            inv = state['invalid']
            raise RuntimeError(
                "Unicorn falhou para %r: %s; EIP=0x%08X last=0x%08X ESP=0x%08X; "
                "mem_invalid access=%d addr=0x%08X size=%d value=0x%X at_eip=0x%08X" %
                (operand, exc, eip, state['last_eip'], esp_now,
                 inv['access'], inv['address'], inv['size'], inv['value'], inv['eip']))
        raise RuntimeError(
            "Unicorn falhou para %r: %s; EIP=0x%08X last=0x%08X ESP=0x%08X" %
            (operand, exc, eip, state['last_eip'], esp_now))

    if state['fatal']:
        raise RuntimeError(state['fatal'])
    if not state['returned']:
        raise RuntimeError('helper não retornou para %r; last=0x%08X' % (operand, state['last_eip']))

    tx_cursor = get32(mu, OBJ + 0x5E)
    ext_cursor = get32(mu, OBJ + 0x62)
    high_low_bytes = get32(mu, OBJ + 0x56)
    emitted = max(0, tx_cursor - 6)
    tx = bytes(mu.mem_read(TX_BUF + 6, emitted)) if emitted else b''
    ext = bytes(mu.mem_read(OBJ + 0xE0, ext_cursor)) if ext_cursor else b''

    return {
        'operand': operand,
        'tx_cursor': tx_cursor,
        'ext_cursor': ext_cursor,
        'high_low_bytes': high_low_bytes,
        'tx': tx,
        'ext': ext,
        'field172': get8(mu, OBJ + 0x172),
        'field173': get8(mu, OBJ + 0x173),
        'field174': get8(mu, OBJ + 0x174),
        'steps': state['steps'],
        'strcpy_calls': state['strcpy_calls'],
        'sprintf_calls': state['sprintf_calls'],
    }


def hx(data):
    return ' '.join('%02X' % b for b in data)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe', nargs='?', default=EXE_DEFAULT)
    ap.add_argument('-o', '--output')
    args = ap.parse_args()

    fixtures = [
        ('D0002', bytes.fromhex('F0 01'), bytes.fromhex('00')),
        ('D0001', bytes.fromhex('F0 00'), bytes.fromhex('00')),
        ('00010', bytes.fromhex('80 0A'), bytes.fromhex('00')),
        ('00000', bytes.fromhex('80 00'), bytes.fromhex('00')),
        ('01000', bytes.fromhex('87 68'), bytes.fromhex('00')),
    ]

    rows = []
    overall = True
    for operand, expected_hl, expected_ext in fixtures:
        try:
            row = emulate(args.exe, operand)
            row['error'] = None
            row['expected_hl'] = expected_hl
            row['expected_ext'] = expected_ext
            row['ok'] = (
                row['tx'] == expected_hl
                and row['ext'] == expected_ext
                and row['high_low_bytes'] == 2
                and row['tx_cursor'] == 8
                and row['ext_cursor'] == 1
            )
        except Exception as exc:
            row = {
                'operand': operand,
                'error': str(exc),
                'expected_hl': expected_hl,
                'expected_ext': expected_ext,
                'ok': False,
            }
        rows.append(row)
        overall &= row['ok']

    lines = []
    lines.append('PC12 PG33 OPERAND HELPER - UNICORN OFFLINE EMULATION')
    lines.append('=' * 96)
    lines.append('entry=0x%08X mode=OFFLINE; CRT string/format wrappers intercepted locally; no COM/TX/PLC' % HELPER)
    lines.append('')
    for row in rows:
        if row.get('error'):
            lines.append('FALHA: %-8s error=%s' % (row['operand'], row['error']))
            continue
        lines.append('%s: %-8s HIGH/LOW=[%s] EXT=[%s] fields=%02X/%02X/%02X steps=%d strcpy=%d sprintf=%d' %
                     ('OK' if row['ok'] else 'DIVERGE', row['operand'],
                      hx(row['tx']), hx(row['ext']), row['field172'], row['field173'],
                      row['field174'], row['steps'], row['strcpy_calls'], len(row['sprintf_calls'])))
        for fmt, out in row['sprintf_calls']:
            lines.append('  CRT sprintf fmt=%r -> %r' % (fmt.decode('ascii', errors='replace'), out.decode('ascii', errors='replace')))
        lines.append('  expected HIGH/LOW=[%s] EXT=[%s] counters HL=%d TX=%d EXT=%d' %
                     (hx(row['expected_hl']), hx(row['expected_ext']),
                      row['high_low_bytes'], row['tx_cursor'], row['ext_cursor']))

    lines.append('')
    lines.append('RESULT=' + ('PASS' if overall else 'FAIL'))
    if overall:
        lines.append('Os operandos do ADD coincidem com as palavras fisicamente observadas no quadro 34.')
    else:
        lines.append('Divergência preservada como dado experimental; wrappers CRT não alteram a lógica TP02 emulada.')
    lines.append('Nenhum byte foi transmitido fora do Unicorn.')

    report = '\n'.join(lines) + '\n'
    if args.output:
        import pathlib
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')
    return 0 if overall else 1


if __name__ == '__main__':
    raise SystemExit(main())
