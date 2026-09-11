#!/usr/bin/env python3
"""Executa somente o validador de RX da rotina 0x0046F5E6 no Unicorn.

O ponto de entrada 0x0046F684 fica depois que o helper de recepção retornou.
O script injeta bytes sintéticos em RX_BUF/RX_LEN e para em 0x0046F719,
antes do restante da rotina. Nenhuma API, COM ou rotina TX é executada.

Objetivo: comprovar que a aceitação usada também pelo caminho PG33 é genérica:
checksum global (soma=FF) e bits de status do primeiro byte, sem comparação
específica com o comando transmitido.
"""

import argparse
import pathlib
import struct
import sys

try:
    from unicorn import Uc, UcError, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE
    from unicorn.x86_const import UC_X86_REG_EIP, UC_X86_REG_ESP
except ImportError:
    print('ERRO: unicorn não instalado. Use: pip install unicorn==2.1.4', file=sys.stderr)
    raise

from emulate_pc12_pg33_builder import load_pe, map_image

EXE_DEFAULT = 'src/OpenLadderStudio.Desktop/pc12.exe'
ENTRY = 0x0046F684
STOP = 0x0046F719

RX_BUF = 0x00530230
RX_LEN = 0x004FA8B0
F_TIMEOUT = 0x004FA8B7
F_ERROR = 0x004FA8B8
F_CHECKSUM = 0x004FA8B9
F_BIT20 = 0x004FA8BE
CHECKSUM_BYPASS = 0x005301AE
STACK = 0x26000000


def put32(mu, addr, value):
    mu.mem_write(addr, struct.pack('<I', value & 0xFFFFFFFF))


def get8(mu, addr):
    return bytes(mu.mem_read(addr, 1))[0]


def emulate(exe, frame, bypass=0):
    data, image_base, sections = load_pe(exe)
    mu = Uc(UC_ARCH_X86, UC_MODE_32)
    map_image(mu, data, image_base, sections)
    mu.mem_map(STACK - 0x10000, 0x20000)

    mu.mem_write(RX_BUF, bytes(frame))
    put32(mu, RX_LEN, len(frame))
    mu.mem_write(F_TIMEOUT, b'\x00')
    mu.mem_write(F_ERROR, b'\x55')
    mu.mem_write(F_CHECKSUM, b'\x55')
    mu.mem_write(F_BIT20, b'\x55')
    mu.mem_write(CHECKSUM_BYPASS, bytes([bypass & 0xFF]))
    mu.reg_write(UC_X86_REG_ESP, STACK)

    hit = {'stop': False}

    def hook(uc, address, size, _user):
        if address == STOP:
            hit['stop'] = True
            uc.emu_stop()

    mu.hook_add(UC_HOOK_CODE, hook)
    try:
        mu.emu_start(ENTRY, STOP, count=20000)
    except UcError as exc:
        raise RuntimeError('Unicorn falhou: %s' % exc)
    if not hit['stop']:
        raise RuntimeError('ponto final do validador não foi alcançado')

    return {
        'timeout': get8(mu, F_TIMEOUT),
        'checksum': get8(mu, F_CHECKSUM),
        'error': get8(mu, F_ERROR),
        'bit20': get8(mu, F_BIT20),
    }


def hx(bs):
    return ' '.join('%02X' % b for b in bs)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('exe', nargs='?', default=EXE_DEFAULT)
    ap.add_argument('-o', '--output')
    args = ap.parse_args()

    fixtures = [
        # Resposta F0 observada fisicamente em bancada: checksum válido, status=00.
        ('physical-f0-known', bytes.fromhex('00 02 10 22 CB'), 0,
         {'timeout': 0, 'checksum': 0, 'error': 0, 'bit20': 0}),
        # Candidato mínimo estrutural: demonstra aceitação genérica, NÃO prova ACK do PG33.
        ('generic-00-00-ff', bytes.fromhex('00 00 FF'), 0,
         {'timeout': 0, 'checksum': 0, 'error': 0, 'bit20': 0}),
        # Soma ainda fecha FF, mas bit 7 do primeiro byte marca resposta de erro.
        ('status-bit80', bytes.fromhex('80 00 7F'), 0,
         {'timeout': 0, 'checksum': 0, 'error': 1, 'bit20': 0}),
        # Checksum inválido deve ligar apenas a flag correspondente antes de STOP.
        ('bad-checksum', bytes.fromhex('00 00 FE'), 0,
         {'timeout': 0, 'checksum': 1, 'error': 0x55, 'bit20': 0x55}),
        # Bit 0x20 é armazenado em flag separada; PG33 não a consulta no gate de sucesso.
        ('status-bit20', bytes.fromhex('20 00 DF'), 0,
         {'timeout': 0, 'checksum': 0, 'error': 0, 'bit20': 1}),
    ]

    rows = []
    overall = True
    for name, frame, bypass, expected in fixtures:
        got = emulate(args.exe, frame, bypass)
        ok = got == expected
        overall &= ok
        rows.append((name, frame, got, expected, ok))

    lines = []
    lines.append('PC12 RX VALIDATOR - UNICORN OFFLINE EMULATION')
    lines.append('=' * 92)
    lines.append('entry=0x%08X stop=0x%08X' % (ENTRY, STOP))
    lines.append('mode=OFFLINE; nenhum helper de recepção, COM ou TX é executado')
    lines.append('')
    for name, frame, got, expected, ok in rows:
        lines.append('%s: %s RX=[%s]' % ('OK' if ok else 'FALHA', name, hx(frame)))
        lines.append('  got      timeout=%02X checksum=%02X error=%02X bit20=%02X' %
                     (got['timeout'], got['checksum'], got['error'], got['bit20']))
        lines.append('  expected timeout=%02X checksum=%02X error=%02X bit20=%02X' %
                     (expected['timeout'], expected['checksum'], expected['error'], expected['bit20']))
    lines.append('')
    lines.append('RESULT=' + ('PASS' if overall else 'FAIL'))
    lines.append('Conclusão: o validador é genérico; ele não identifica um ACK PG33 único.')
    lines.append('Um quadro como 00 00 FF ser aceito aqui significa apenas que satisfaz as regras')
    lines.append('genéricas do parser, NÃO que esse seja o payload realmente devolvido pelo TP02 ao 0x33.')
    lines.append('Nenhum byte foi transmitido fora do Unicorn.')

    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')
    return 0 if overall else 1


if __name__ == '__main__':
    raise SystemExit(main())
