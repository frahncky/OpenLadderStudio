#!/usr/bin/env python3
"""Emula blocos PC12 de scan time/RTC e cruza os campos com controles de tela.

Tres fases independentes, declaradas: montagem do pedido (para antes de TX),
parser pos-validacao com payload sintetico e distribuicao dos textos na UI.
Nao simula firmware, autenticacao nem aprovacao de uma resposta pelo transporte.
"""

import argparse
import pathlib
import struct

from unicorn import UC_HOOK_CODE
from unicorn.x86_const import UC_X86_REG_EBX, UC_X86_REG_ESP

from emulate_pc12_monitor_commands import (
    MonitorEmulator, EXE, SHA256, OBJ, STACK, STOP, TX, TX_BUF, TX_LEN,
    get32, put32, cstring, frame, selected_dialogs,
)
from emulate_pc12_eeprom_commands import make_machine, return_call

SCAN_TEXT = (0x52CDC6, 0x52CDDA, 0x52CDD0)
RTC_TEXT = (0x52CDF6, 0x52CDF3, 0x52CDF0, 0x52CDED, 0x52CDEA, 0x52CDE7, 0x52CDE4)


def execute_slice(emulator, entry, end, payload=None, texts=None, ui=None):
    mu = make_machine(emulator.image)
    mu.reg_write(UC_X86_REG_EBX, OBJ)
    if payload is not None:
        mu.mem_write(0x530232, payload)  # inicio dos dados, depois de STATUS/LEN
    for address, value in texts or []:
        mu.mem_write(address, str(value).encode('ascii') + b'\0')
    if ui:
        # Objetos sinteticos de controles. HWND representa o ID do RT_DIALOG.
        for i, (field, ident) in enumerate(ui):
            control = OBJ + 0x2000 + i * 0x100
            window = control + 0x40
            put32(mu, OBJ + field, control)
            put32(mu, control, window)
            put32(mu, window + 0xC, ident)
    result = {'frame': None, 'display': {}, 'texts': {}, 'reached': False}

    def hook(uc, pc, size, _):
        if pc == end:
            result['reached'] = True
            if pc == TX:
                length = get32(uc, TX_LEN)
                if length not in (6, 20):
                    raise ValueError('TX_LEN inesperado')
                result['frame'] = bytes(uc.mem_read(TX_BUF, length))
            uc.emu_stop()
            return
        sp = uc.reg_read(UC_X86_REG_ESP)
        arg = lambda n: get32(uc, sp + 4 + 4 * n)
        if pc == 0x4CBF90:
            fmt = cstring(uc, arg(1))
            if fmt not in (b'%02d', b'%03d'):
                raise ValueError('formato nao esperado')
            out = (fmt.decode('ascii') % arg(2)).encode('ascii')
            dst = arg(0)
            uc.mem_write(dst, out + b'\0')
            result['texts'][dst] = out.decode('ascii')
            return_call(uc, len(out))
            return
        if pc == 0x4CC126:
            return_call(uc, int(cstring(uc, arg(0))))
            return
        if pc == 0x4CBE48:
            return_call(uc, 1000)
            return
        if pc == 0x4CBF7E:
            result['display'][arg(0)] = cstring(uc, arg(1)).decode('ascii')
            return_call(uc, 1, 8)  # SetWindowTextA, sem janela real
            return
        # Cada slice contem somente codigo nativo necessario ate seu ponto final.
        if not entry <= pc < end and not (end == TX and (
                entry == 0x4BF443 and entry <= pc < 0x4BF4CA or
                entry == 0x4BC568 and entry <= pc < 0x4BC5DF or
                entry == 0x4BC811 and entry <= pc < 0x4BC99B)):
            raise ValueError('execucao fora do slice: %08X' % pc)

    mu.hook_add(UC_HOOK_CODE, hook)
    mu.emu_start(entry, STOP + 1, count=5000)
    if not result['reached']:
        raise RuntimeError('slice nao terminou')
    return result


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--exe', default=EXE)
    ap.add_argument('-o', '--output')
    args = ap.parse_args()
    emulator = MonitorEmulator(args.exe)
    dialogs = selected_dialogs(emulator.pe, (33, 45))
    pe = emulator.pe
    thunk = pe.get_data(0x4CBF7E - 0x400000, 6)
    assert thunk[:2] == b'\xff\x25'
    target = struct.unpack_from('<I', thunk, 2)[0]
    names = {i.address: i.name for d in pe.DIRECTORY_ENTRY_IMPORT for i in d.imports}
    assert names[target] == b'SetWindowTextA'
    lines = ['PC12 SCAN TIME / RTC - OFFLINE', 'SHA256=' + SHA256,
             'DIALOG 33 %r' % (dialogs[33],), 'DIALOG 45 %r' % (dialogs[45],)]
    count = 0
    for name, entry, expected in [('scan', 0x4BF443, '0A 03 60 00 06 8C'),
                                  ('rtc', 0x4BC568, '0A 03 53 F9 0E 98')]:
        got = execute_slice(emulator, entry, TX)
        assert got['frame'] == bytes.fromhex(expected)
        lines.append('OK READ %s: %s' % (name, expected))
        count += 1
    # Ordem do payload: atual, minimo, maximo. UI reorganiza minimo/maximo.
    for current, minimum, maximum in [(1234, 256, 4095), (10, 2, 30), (65535, 0, 65535)]:
        values = (current, minimum, maximum)
        got = execute_slice(emulator, 0x4BF5C7, 0x4BF63D, struct.pack('>HHH', *values))
        expected = dict(zip(SCAN_TEXT, ['%03d' % v for v in values]))
        assert got['texts'] == expected
        displayed = execute_slice(emulator, 0x472681, 0x4726B7,
                                  texts=expected.items(), ui=[(0x19, 102), (0x1D, 107), (0x21, 108)])
        assert displayed['display'] == {102: '%03d' % current, 107: '%03d' % maximum,
                                        108: '%03d' % minimum}
        lines.append('OK SCAN payload=%s -> atual=%d minimo=%d maximo=%d (ms da tela)' %
                     (struct.pack('>HHH', *values).hex(' ').upper(), *values))
        count += 1
    # V1022 = dia da semana: cruzamento documentado em tp02-pg-memory-variants-2026-09-12.md.
    # O 7 e deliberadamente fora de 0..6; este slice testa preservacao, nao validade da data.
    for values in [(59, 34, 12, 25, 7, 8, 26), (0, 0, 0, 1, 0, 1, 0), (58, 57, 23, 31, 3, 12, 99)]:
        payload = struct.pack('>7H', *values)
        got = execute_slice(emulator, 0x4BC6A7, 0x4BC7BB, payload)
        expected = dict(zip(RTC_TEXT, ['%02d' % v for v in values]))
        assert got['texts'] == expected
        displayed = execute_slice(emulator, 0x47210D, 0x472179, texts=expected.items(),
                                  ui=[(0x19, 107), (0x1D, 108), (0x21, 109),
                                      (0x25, 110), (0x29, 111), (0x2D, 112)])
        assert displayed['display'] == {107: '%02d' % values[6], 108: '%02d' % values[5],
                                        109: '%02d' % values[3], 110: '%02d' % values[2],
                                        111: '%02d' % values[1], 112: '%02d' % values[0]}
        write = execute_slice(emulator, 0x4BC811, TX, texts=expected.items())
        assert write['frame'] == frame([9, 0x11, 0x53, 0xF9, 0x0E] + list(payload))
        lines.append('OK RTC read sec/min/hour/day/weekday/month/year=%r -> UI=%r' %
                     (values, displayed['display']))
        lines.append('OK RTC write: ' + write['frame'].hex(' ').upper())
        count += 2
    lines += ['RESULT=PASS cases=%d' % count,
              'A unidade ms vem do dialogo; nao houve medicao real de temporizacao.',
              'RTC: ano em dois digitos, sem inferir seculo. V1022=dia da semana (manual + enderecos).',
              'Fixture weekday=7 testa preservacao; a faixa documentada e 0..6.',
              'Nenhuma porta serial aberta; nenhum quadro enviado ao PLC.']
    report = '\n'.join(lines) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
