import os, struct, sys
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_32
from capstone.x86 import X86_OP_IMM, X86_OP_MEM

EXE = os.path.join('PC12_v2.1_Windows7_v3_portatil', 'pc12.exe')
TARGETS = [b'CON-ICB\r', b'TP02 Link Success', b'Link Protocol fail', b'COM1:Linking']

pe = pefile.PE(EXE, fast_load=False)
image_base = pe.OPTIONAL_HEADER.ImageBase
text = next(s for s in pe.sections if s.Name.rstrip(b'\0') == b'.text')
text_data = text.get_data()
text_va = image_base + text.VirtualAddress

md = Cs(CS_ARCH_X86, CS_MODE_32)
md.detail = True
insns = list(md.disasm(text_data, text_va))
addr_to_idx = {i.address:n for n,i in enumerate(insns)}

with open(EXE,'rb') as f:
    raw = f.read()

def off_to_va(off):
    try:
        return image_base + pe.get_rva_from_offset(off)
    except Exception:
        return None

def find_all(hay, needle):
    p = 0
    out = []
    while True:
        p = hay.find(needle, p)
        if p < 0: return out
        out.append(p)
        p += 1

def xrefs_to(values):
    refs=[]
    values=set(values)
    for idx,ins in enumerate(insns):
        hit=False
        for op in ins.operands:
            if op.type == X86_OP_IMM and op.imm in values:
                hit=True
            elif op.type == X86_OP_MEM and op.mem.disp in values:
                hit=True
        if hit: refs.append((idx,ins))
    return refs

def dump_window(idx, before=35, after=80):
    lo=max(0,idx-before); hi=min(len(insns), idx+after)
    for j in range(lo,hi):
        ins=insns[j]
        mark='>>' if j==idx else '  '
        print(f'{mark} {ins.address:08X}: {ins.mnemonic:<7} {ins.op_str}')

print('=== PC12 PG STATIC ANALYSIS ===')
print(f'ImageBase=0x{image_base:08X} .text=0x{text_va:08X} instructions={len(insns)}')

for target in TARGETS:
    print('\n=== TARGET', repr(target), '===')
    offs=find_all(raw,target)
    if not offs:
        print('not found')
        continue
    for off in offs[:4]:
        va=off_to_va(off)
        print(f'file_off=0x{off:X} va={"0x%08X"%va if va else "?"}')
        candidates=[]
        if va:
            candidates.append(va)
            ptr=struct.pack('<I',va)
            ptr_offs=find_all(raw,ptr)
            for po in ptr_offs[:10]:
                pva=off_to_va(po)
                if pva:
                    print(f' pointer at file_off=0x{po:X} va=0x{pva:08X}')
                    candidates.append(pva)
        refs=xrefs_to(candidates)
        print('xrefs:', len(refs))
        for idx,ins in refs[:8]:
            print(f'-- XREF at 0x{ins.address:08X} --')
            dump_window(idx)

print('\n=== RESPONSE CONSTANT SEARCH ===')
patterns=[bytes([0x4E,0x01,0x09,0x35]), bytes([0xC0,0x01,0x09,0x35]), bytes([0xF0,0x00,0x0F])]
for pat in patterns:
    offs=find_all(raw,pat)
    print(pat.hex(' ').upper(), 'occurrences=', len(offs), [hex(x) for x in offs[:20]])

print('\n=== IMMEDIATES NEAR PG-SIZED CONSTANTS ===')
for imm in [0x3509014E,0x0F00F0,0xF0000F,0x4E,0x01,0x09,0x35,0xF0,0x0F]:
    refs=xrefs_to([imm])
    if refs:
        print(f'IMM 0x{imm:X}: {len(refs)} refs')
        for idx,ins in refs[:6]:
            print(f'  0x{ins.address:08X}: {ins.mnemonic} {ins.op_str}')
