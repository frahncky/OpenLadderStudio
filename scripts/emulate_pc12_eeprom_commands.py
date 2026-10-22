#!/usr/bin/env python3
"""Cruza EEPROM/PG12/PG13 e o gate PG14 com recursos e instrucoes do PC12.

Somente Unicorn offline. PG12/13: executa o handler nativo de selecao do
radio e, em seguida, o bloco de montagem apos a confirmacao do dialogo.
PG14: exercita apenas a comparacao final com strings sinteticas, depois
do tratamento de senha feito pelo PC12. Nao descobre senhas nem executa
o fluxo de autenticacao completo; nenhuma comunicacao com PLC.
"""

import argparse
import pathlib
import re
import struct

from unicorn import Uc, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE
from unicorn.x86_const import (
    UC_X86_REG_EAX, UC_X86_REG_EBP, UC_X86_REG_EBX,
    UC_X86_REG_EDI, UC_X86_REG_EIP, UC_X86_REG_ESP,
)

from emulate_pc12_pg33_builder import map_image
from emulate_pc12_monitor_commands import (
    MonitorEmulator, EXE, SHA256, OBJ, STACK, STOP, TX, TX_BUF, TX_LEN,
    put32, get32, cstring, selected_dialogs, dialog_string,
)

RADIO = {1: 0x474515, 2: 0x474540}
SELECTION = 0x4BC3C3
PASSWORD_COMPARE = 0x4BC220
PASSWORD_MISMATCH = 0x4BC2FA


def make_machine(image):
    data, base, sections = image
    mu = Uc(UC_ARCH_X86, UC_MODE_32)
    map_image(mu, data, base, sections)
    mu.mem_map(OBJ, 0x10000)
    mu.mem_map(STACK - 0x10000, 0x20000)
    mu.mem_map(STOP, 0x1000)
    mu.reg_write(UC_X86_REG_ESP, STACK)
    put32(mu, STACK, STOP)
    return mu


def return_call(mu, eax=0, pop=0):
    sp = mu.reg_read(UC_X86_REG_ESP)
    pc = get32(mu, sp)
    mu.reg_write(UC_X86_REG_ESP, sp + 4 + pop)
    mu.reg_write(UC_X86_REG_EAX, eax & 0xFFFFFFFF)
    mu.reg_write(UC_X86_REG_EIP, pc)


def emulate_eeprom(emulator, option):
    mu = make_machine(emulator.image)
    put32(mu, STACK + 4, OBJ)
    put32(mu, OBJ + 0x19, 3 - option)  # estado oposto antes do clique
    result = {'frame': None, 'selected': None, 'checkbox_calls': 0}

    def hook(uc, pc, size, _):
        if pc == 0x4CB8E6:  # TCheckBox::SetCheck: somente efeito visual
            result['checkbox_calls'] += 1
            return_call(uc)
            return
        if pc == STOP:
            result['selected'] = get32(uc, OBJ + 0x19)
            uc.emu_stop()
            return
        if pc == TX:
            length = get32(uc, TX_LEN)
            if length != 3:
                raise ValueError('comprimento inesperado')
            result['frame'] = bytes(uc.mem_read(TX_BUF, length))
            uc.emu_stop()
            return
        if not (0x474515 <= pc < 0x47456B or SELECTION <= pc < 0x4BC41C):
            raise ValueError('execucao fora da allowlist: %08X' % pc)

    mu.hook_add(UC_HOOK_CODE, hook)
    mu.emu_start(RADIO[option], STOP + 1, count=2000)
    if result['selected'] != option or result['checkbox_calls'] != 2:
        raise RuntimeError('selecao nativa nao confirmada')
    # Inicio explicito do bloco apos OK do dialogo; nao simula autenticacao/UI inteira.
    mu.reg_write(UC_X86_REG_ESP, STACK)
    mu.reg_write(UC_X86_REG_EDI, OBJ)
    mu.reg_write(UC_X86_REG_EBX, OBJ + 0x4000)
    mu.emu_start(SELECTION, STOP + 1, count=2000)
    if result['frame'] is None:
        raise RuntimeError('nao alcancou TX')
    return result


def emulate_password_gate(emulator, equal):
    mu = make_machine(emulator.image)
    mu.reg_write(UC_X86_REG_EBP, STACK)
    mu.reg_write(UC_X86_REG_ESP, STACK - 0x100)
    mu.reg_write(UC_X86_REG_EBX, OBJ)
    mu.mem_write(STACK - 0x4C, b'FIXTURE_A\0')
    mu.mem_write(OBJ + 0x12CA, b'FIXTURE_A\0' if equal else b'FIXTURE_B\0')
    result = {'frame': None, 'rejected': False, 'clock': 0}

    def hook(uc, pc, size, _):
        if pc == 0x4CBEA8:
            sp = uc.reg_read(UC_X86_REG_ESP)
            a, b = cstring(uc, get32(uc, sp + 4)), cstring(uc, get32(uc, sp + 8))
            return_call(uc, (a > b) - (a < b), 8)
            return
        if pc == 0x4CBE48:
            result['clock'] += 1
            return_call(uc, 1000)
            return
        if pc == PASSWORD_MISMATCH:
            result['rejected'] = True
            uc.emu_stop()
            return
        if pc == TX:
            if get32(uc, TX_LEN) != 3:
                raise ValueError('comprimento PG14 inesperado')
            result['frame'] = bytes(uc.mem_read(TX_BUF, 3))
            uc.emu_stop()
            return
        if not PASSWORD_COMPARE <= pc < 0x4BC283:
            raise ValueError('execucao fora da allowlist: %08X' % pc)

    mu.hook_add(UC_HOOK_CODE, hook)
    mu.emu_start(PASSWORD_COMPARE, STOP + 1, count=2000)
    if not result['rejected'] and result['frame'] is None:
        raise RuntimeError('gate nao terminou')
    return result


def evidence(emulator):
    pe = emulator.pe
    base = pe.OPTIONAL_HEADER.ImageBase
    data = emulator.image[0]
    rows = []
    dialogs = selected_dialogs(pe)
    assert dialogs[30][0] == 'EEPROM Dialog'
    assert dialogs[30][1][102] == 'EEPRON PACK ---> PLC'
    assert dialogs[30][1][103] == 'PLC ---> EEPROM PACK'
    rows.append('DIALOG 30: %r' % (dialogs[30],))
    for ident, handler in [(306, 0x4BBEDB), (102, RADIO[1]), (103, RADIO[2]),
                           (104, 0x473932), (105, 0x473CF6)]:
        # Estrutura de resposta OWL: mensagem=0, ID, dispatch, handler.
        needle = struct.pack('<IIII', 0, ident, 0x4CBDC6, handler)
        offset = data.find(needle)
        assert offset >= 0 and data.find(needle, offset + 1) == -1
        rows.append('OWL ID=%d table=0x%08X handler=0x%08X' %
                    (ident, base + pe.get_rva_from_offset(offset), handler))
    # Confere import do unico stub visual utilizado.
    thunk = pe.get_data(0x4CB8E6 - base, 6)
    assert thunk[:2] == b'\xff\x25'
    iat = struct.unpack_from('<I', thunk, 2)[0]
    names = {i.address: i.name for d in pe.DIRECTORY_ENTRY_IMPORT for i in d.imports}
    assert names[iat] == b'@TCheckBox@SetCheck$qui'
    # Recurso de menu 1 estabelece o nome de alto nivel do ID 306.
    menu_resource = next(t for t in pe.DIRECTORY_ENTRY_RESOURCE.entries if t.id == 4)
    item = next(e for e in menu_resource.directory.entries if e.id == 1)
    r = item.directory.entries[0].data.struct
    raw = pe.get_data(r.OffsetToData, r.Size)
    assert raw[:4] == b'\0\0\0\0'
    menus = {}

    def read_menu(pos):
        while True:
            flags = struct.unpack_from('<H', raw, pos)[0]
            pos += 2
            ident = None
            if not flags & 0x10:
                ident = struct.unpack_from('<H', raw, pos)[0]
                pos += 2
            label, pos = dialog_string(raw, pos)
            if ident:
                menus[ident] = label
            if flags & 0x10:
                pos = read_menu(pos)
            if flags & 0x80:
                return pos

    read_menu(4)
    assert menus[306] == 'EEPROM'
    for ident in (306, 403, 406):
        rows.append('MENU %d: %s' % (ident, menus[ident]))
    # PG14 tem cinco construtores em caminhos protegidos, nao envia a senha no quadro.
    pg14 = []
    inventory = {}
    for sec in pe.sections:
        if sec.Name.rstrip(b'\0') != b'.text':
            continue
        code = sec.get_data()
        for match in re.finditer(b'\xc6\x05' + re.escape(struct.pack('<I', TX_BUF)) + b'(.)', code, re.DOTALL):
            value = match.group(1)[0]
            address = base + sec.VirtualAddress + match.start()
            inventory.setdefault(value, []).append(address)
            if value == 0x14:
                pg14.append(address)
    assert pg14 == [0x4AF763, 0x4B19E8, 0x4B687F, 0x4BBD3B, 0x4BC238]
    assert 0x12 in inventory and 0x13 in inventory
    rows.append('PG14 builder sites: ' + ', '.join('%08X' % x for x in pg14))
    rows.append('INVENTARIO C6 05 TX_BUF imm8 (%d codigos; nao prova exaustividade):' % len(inventory))
    for command, sites in sorted(inventory.items()):
        rows.append('  %02X (%d): %s' % (command, len(sites), ', '.join('%08X' % x for x in sites)))
    return rows


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--exe', default=EXE)
    ap.add_argument('-o', '--output')
    args = ap.parse_args()
    emulator = MonitorEmulator(args.exe)
    rows = ['PC12 EEPROM/PASSWORD GATE - OFFLINE', 'SHA256=' + SHA256] + evidence(emulator)
    for option, expected in [(1, bytes.fromhex('12 00 ED')), (2, bytes.fromhex('13 00 EC'))]:
        result = emulate_eeprom(emulator, option)
        assert result['frame'] == expected and sum(expected) & 255 == 255
        rows.append('OK EEPROM option=%d native_selection=%d frame=%s' %
                    (option, result['selected'], expected.hex(' ').upper()))
    ok = emulate_password_gate(emulator, True)
    assert ok['frame'] == bytes.fromhex('14 00 EB') and not ok['rejected'] and ok['clock'] == 1
    no = emulate_password_gate(emulator, False)
    assert no['frame'] is None and no['rejected'] and no['clock'] == 0
    rows += ['OK PG14 equal synthetic final comparison -> 14 00 EB',
             'OK PG14 unequal synthetic final comparison -> rejection, no TX',
             'RESULT=PASS cases=4',
             'Somente blocos nativos identificados; nao executa senha/UI/transferencia completas.',
             'TX interceptado antes da primeira instrucao; nenhum ACK ou efeito fisico validado.']
    report = '\n'.join(rows) + '\n'
    if args.output:
        pathlib.Path(args.output).write_text(report, encoding='utf-8')
    else:
        print(report, end='')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
