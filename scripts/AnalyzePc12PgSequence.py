import os, struct
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_32
from capstone.x86 import X86_OP_IMM, X86_OP_MEM

EXE = os.path.join('PC12_v2.1_Windows7_v3_portatil', 'pc12.exe')
TARGETS = [
    b'CON-ICB\r', b'CON-ICB', b'CON-', b'ICB\r', b'ICB',
    b'TP02 Link Success', b'Link Protocol fail', b'COM1:Linking'
]

pe = pefile.PE(EXE, fast_load=False)
image_base = pe.OPTIONAL_HEADER.ImageBase
with open(EXE,'rb') as f:
    raw = f.read()

md = Cs(CS_ARCH_X86, CS_MODE_32)
md.detail = True
md.skipdata = True

sections=[]
insns=[]
for s in pe.sections:
    name=s.Name.rstrip(b'\0').decode('latin1','replace')
    va=image_base+s.VirtualAddress
    data=s.get_data()
    executable=bool(s.Characteristics & 0x20000000)
    sections.append((name,s.PointerToRawData,s.SizeOfRawData,va,s.Misc_VirtualSize,executable))
    if executable:
        for ins in md.disasm(data,va):
            insns.append(ins)
insns.sort(key=lambda i:i.address)

print('=== PE SECTIONS ===')
for name,off,rawsz,va,vsz,exe in sections:
    print(f'{name:8} off=0x{off:06X} raw=0x{rawsz:06X} va=0x{va:08X} vsz=0x{vsz:06X} exec={exe}')
print(f'Executable instructions decoded: {len(insns)}')


def find_all(hay,needle):
    p=0; out=[]
    while True:
        p=hay.find(needle,p)
        if p<0:return out
        out.append(p); p+=1

def off_to_va(off):
    try:return image_base+pe.get_rva_from_offset(off)
    except Exception:return None

def va_to_off(va):
    try:return pe.get_offset_from_rva(va-image_base)
    except Exception:return None

def xrefs_to(values):
    vals=set(v for v in values if v is not None)
    refs=[]
    for idx,ins in enumerate(insns):
        for op in ins.operands:
            if op.type==X86_OP_IMM and op.imm in vals:
                refs.append((idx,ins)); break
            if op.type==X86_OP_MEM and op.mem.disp in vals:
                refs.append((idx,ins)); break
    return refs

def dump_window(idx,before=24,after=48):
    lo=max(0,idx-before); hi=min(len(insns),idx+after)
    for j in range(lo,hi):
        ins=insns[j]; mark='>>' if j==idx else '  '
        print(f'{mark} {ins.address:08X}: {ins.mnemonic:<8} {ins.op_str}')

def pointer_chain_for_va(va):
    vals=[va]
    seen={va}
    frontier=[va]
    for depth in range(2):
        nxt=[]
        for value in frontier:
            pat=struct.pack('<I',value & 0xffffffff)
            for off in find_all(raw,pat)[:30]:
                pva=off_to_va(off)
                if pva is not None and pva not in seen:
                    print(f' pointer depth={depth+1} file_off=0x{off:X} va=0x{pva:08X} -> 0x{value:08X}')
                    seen.add(pva); vals.append(pva); nxt.append(pva)
        frontier=nxt
    return vals

print('\n=== STRING/PARTIAL TARGETS ===')
for target in TARGETS:
    offs=find_all(raw,target)
    print(f'\nTARGET {target!r}: {len(offs)} occurrence(s)')
    for off in offs[:8]:
        va=off_to_va(off)
        print(f' file_off=0x{off:X} va={"0x%08X"%va if va else "?"}')
        if va is None: continue
        vals=pointer_chain_for_va(va)
        refs=xrefs_to(vals)
        print(' xrefs=',len(refs))
        for idx,ins in refs[:6]:
            print(f' -- XREF 0x{ins.address:08X} --')
            dump_window(idx)

print('\n=== RAW CONSTANTS ===')
patterns={
    'HELLO full':bytes([0x43,0x4F,0x4E,0x2D,0x49,0x43,0x42,0x0D]),
    'HELLO dword1':bytes([0x43,0x4F,0x4E,0x2D]),
    'HELLO dword2':bytes([0x49,0x43,0x42,0x0D]),
    'RX 8O1':bytes([0x4E,0x01,0x09,0x35]),
    'RX 8N1':bytes([0xC0,0x01,0x09,0x35]),
    'F0 probe':bytes([0xF0,0x00,0x0F]),
}
for label,pat in patterns.items():
    offs=find_all(raw,pat)
    print(f'{label}: {pat.hex(" ").upper()} occurrences={len(offs)} {list(map(hex,offs[:20]))}')
    for off in offs[:5]:
        va=off_to_va(off)
        print(f'  off=0x{off:X} va={"0x%08X"%va if va else "?"}')
        lo=max(0,off-48); hi=min(len(raw),off+len(pat)+96)
        print('  raw-near=',raw[lo:hi].hex(' ').upper())
        if va:
            vals=pointer_chain_for_va(va)
            refs=xrefs_to(vals)
            print('  xrefs=',len(refs))
            for idx,ins in refs[:6]:
                print(f'  -- XREF 0x{ins.address:08X} --')
                dump_window(idx,18,36)

print('\n=== HELLO DWORD IMMEDIATES ===')
for imm in [0x2D4E4F43,0x0D424349,0x3509014E,0x350901C0,0x000F00F0,0x000F00F0 & 0xffffffff]:
    refs=xrefs_to([imm])
    print(f'IMM 0x{imm:08X}: {len(refs)} refs')
    for idx,ins in refs[:12]:
        print(f'  0x{ins.address:08X}: {ins.mnemonic} {ins.op_str}')
        dump_window(idx,12,24)

print('\n=== BYTE-COMPARE CANDIDATES AROUND LINK MESSAGES ===')
# Print compact references to single-byte constants seen in the physical reply.
for imm in [0x4E,0x01,0x09,0x35,0xC0,0xF0,0x0F]:
    refs=xrefs_to([imm])
    useful=[]
    for idx,ins in refs:
        if ins.mnemonic.startswith(('cmp','test','mov','push')):
            useful.append((idx,ins))
    print(f'IMM 0x{imm:02X}: {len(useful)} cmp/test/mov/push refs')
    for idx,ins in useful[:20]:
        print(f'  0x{ins.address:08X}: {ins.mnemonic:<7} {ins.op_str}')
