#!/usr/bin/env python3
"""
Emula as rotinas de montagem de quadro do pc12.exe para recuperar o protocolo PG.

A varredura estatica (extract_pc12_frames.py) so encontra quadros escritos byte a
byte como imediatos no codigo. Quadros copiados de uma cadeia ou tabela escapam --
o caso conhecido e o proprio handshake CON-ICB.

Este script carrega as secoes do PE em um emulador x86, substitui cada import por
um stub, executa a funcao indicada e observa as escritas no buffer de transmissao,
recuperando o quadro efetivamente montado independentemente de como foi construido.

Nada roda no sistema hospedeiro: o codigo executa dentro do emulador, sem acesso a
disco, rede ou porta serial. As chamadas de API sao interceptadas e retornam
sucesso sem efeito.

    python3 scripts/emulate_pc12_frames.py 46F01D 46F300
    python3 scripts/emulate_pc12_frames.py --sweep

Requer: pip install unicorn
"""
import struct, sys
from unicorn import *
from unicorn.x86_const import *
from capstone import *

EXE = 'PC12_v2.1_Windows7_v3_portatil/pc12.exe'
TX_BUF, TX_LEN = 0x4FA7A8, 0x4FA8AC
TX_ROUTINE = 0x46F5E6
API_BASE = 0x70000000
STACK = 0x20000000

data = open(EXE, 'rb').read()
pe = struct.unpack_from('<I', data, 0x3C)[0]
nsec = struct.unpack_from('<H', data, pe + 6)[0]
optsz = struct.unpack_from('<H', data, pe + 20)[0]
BASE = struct.unpack_from('<I', data, pe + 24 + 28)[0]
secs = []
off = pe + 24 + optsz
for _ in range(nsec):
    nm = data[off:off+8].rstrip(b'\0').decode('latin-1')
    vs, va, rs, ra = struct.unpack_from('<IIII', data, off+8)
    secs.append((nm, va, vs, ra, rs)); off += 40

def rva2f(r):
    for nm, va, vs, ra, rs in secs:
        if rs and va <= r < va + rs:
            return ra + (r - va)
    for nm, va, vs, ra, rs in secs:
        if va <= r < va + max(vs, rs):
            off = ra + (r - va)
            return off if off < len(data) else None
    return None

def load():
    mu = Uc(UC_ARCH_X86, UC_MODE_32)
    lo, hi = BASE, BASE
    for nm, va, vs, ra, rs in secs:
        hi = max(hi, BASE + va + max(vs, rs))
    size = (hi - lo + 0xFFFFF) & ~0xFFFFF
    mu.mem_map(lo, size)
    for nm, va, vs, ra, rs in secs:
        if rs:
            mu.mem_write(BASE + va, data[ra:ra+rs])
    mu.mem_map(STACK - 0x100000, 0x200000)
    mu.mem_map(API_BASE, 0x100000)
    return mu

# --- IAT: aponta cada import para um stub proprio ------------------------
def patch_iat(mu):
    imp_rva = struct.unpack_from("<I", data, pe + 24 + 104)[0]
    f = rva2f(imp_rva)
    names = {}
    idx = 0
    while True:
        oft, ts, fc, nm, ft = struct.unpack_from('<IIIII', data, f)
        if oft == 0 and ft == 0:
            break
        t = rva2f(oft or ft); iat = ft
        if t is None:
            f += 20
            continue
        while True:
            if t + 4 > len(data):
                break
            e = struct.unpack_from('<I', data, t)[0]
            if e == 0:
                break
            nome = '?'
            if not (e & 0x80000000):
                pf = rva2f(e)
                if pf is not None and pf + 2 < len(data):
                    p = pf + 2
                    nome = data[p:data.find(b'\0', p)].decode('latin-1')
            stub = API_BASE + idx * 16
            mu.mem_write(BASE + iat, struct.pack('<I', stub))
            names[stub] = nome
            idx += 1
            t += 4; iat += 4
        f += 20
    return names

# quantos argumentos stdcall cada API consome
ARGS = {'WriteFile':5,'ReadFile':5,'SetCommState':2,'GetCommState':2,'SetupComm':3,
        'PurgeComm':2,'SetCommMask':2,'WaitCommEvent':3,'ClearCommError':3,
        'CreateFileA':7,'GetCommProperties':2,'CloseHandle':1,'Sleep':1,
        'GetTickCount':0,'SetCommTimeouts':2,'GetCommTimeouts':2,'EscapeCommFunction':2}

def run(start, stop_at=TX_ROUTINE, maxins=200000, verbose=False):
    mu = load()
    names = patch_iat(mu)
    mu.reg_write(UC_X86_REG_ESP, STACK)
    mu.reg_write(UC_X86_REG_EBP, STACK)
    mu.mem_write(STACK, struct.pack('<I', 0xDEADBEEF))  # retorno sentinela

    frame = {}
    length = [None]
    hit = [False]

    def on_write(uc, access, addr, size, value, ud):
        if TX_BUF <= addr < TX_BUF + 64:
            for k in range(size):
                frame[addr - TX_BUF + k] = (value >> (8*k)) & 0xFF
        elif addr == TX_LEN:
            length[0] = value

    def on_code(uc, addr, size, ud):
        if addr == stop_at:
            hit[0] = True
            uc.emu_stop(); return
        if API_BASE <= addr < API_BASE + 0x100000:
            nome = names.get(addr & ~0xF, '?')
            esp = uc.reg_read(UC_X86_REG_ESP)
            ret = struct.unpack('<I', uc.mem_read(esp, 4))[0]
            n = ARGS.get(nome, 0)
            uc.reg_write(UC_X86_REG_ESP, esp + 4 + n*4)
            uc.reg_write(UC_X86_REG_EAX, 1)
            uc.reg_write(UC_X86_REG_EIP, ret)
            if verbose: print("   api:", nome)
            return

    mu.hook_add(UC_HOOK_MEM_WRITE, on_write)
    mu.hook_add(UC_HOOK_CODE, on_code)
    err = None
    try:
        mu.emu_start(start, 0xDEADBEEF, count=maxins)
    except UcError as e:
        err = str(e)
    return frame, length[0], hit[0], err

def show(tag, frame, length, hit, err):
    if frame:
        top = max(frame)
        s = ' '.join('%02X' % frame[i] if i in frame else '..' for i in range(top+1))
        tot = sum(frame.values()) % 256
        print("%-28s %s   len=%s%s%s" % (tag, s, length,
              '  soma%256=FF' if tot == 0xFF else '',
              '  [chegou ao TX]' if hit else ''))
    else:
        print("%-28s (nenhuma escrita no buffer TX)%s" % (tag, '  erro: '+err if err else ''))

def f2va(f): return 0x401000 + (f - 0x600)
def va2f(va): return 0x600 + (va - 0x401000)


def sweep():
    """Emula toda funcao que alcanca a rotina de transmissao."""
    import re
    callers = []
    for m in re.finditer(b'\xe8', data[0x600:0xCB800]):
        o = 0x600 + m.start()
        if o + 5 > 0xCB800:
            continue
        rel = struct.unpack_from('<i', data, o + 1)[0]
        if f2va(o) + 5 + rel == TX_ROUTINE:
            callers.append(f2va(o))

    funcs = []
    for c in callers:
        f = va2f(c)
        for back in range(0, 4000):
            o = f - back
            if data[o:o+3] == b'\x55\x8b\xec':
                va = f2va(o)
                if va not in funcs:
                    funcs.append(va)
                break

    print('chamadas a rotina de TX: %d   funcoes distintas: %d\n' % (len(callers), len(funcs)))
    print('%-12s %-38s %-6s %s' % ('funcao', 'quadro montado', 'len', 'soma'))
    print('-' * 74)
    seen = {}
    for fn in sorted(funcs):
        try:
            frame, length, hit, err = run(fn, maxins=60000)
        except Exception:
            continue
        if not frame:
            continue
        top = max(frame)
        key = tuple(frame.get(i) for i in range(top + 1))
        if key in seen:
            continue
        seen[key] = True
        s = ' '.join('%02X' % frame[i] if i in frame else '..' for i in range(top + 1))
        tot = sum(frame.values()) % 256
        print('0x%08X  %-38s %-6s %s' % (fn, s, length, 'FF' if tot == 0xFF else '%02X' % tot))


if __name__ == '__main__':
    if '--sweep' in sys.argv:
        sweep()
    else:
        for a in sys.argv[1:]:
            show('0x%08X' % int(a, 16), *run(int(a, 16)))
