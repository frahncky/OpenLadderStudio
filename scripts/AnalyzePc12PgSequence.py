import os, struct
import pefile
from capstone import Cs, CsError, CS_ARCH_X86, CS_MODE_32
from capstone.x86 import X86_OP_IMM, X86_OP_MEM

EXE = os.path.join('PC12_v2.1_Windows7_v3_portatil', 'pc12.exe')
pe = pefile.PE(EXE, fast_load=False)
image_base = pe.OPTIONAL_HEADER.ImageBase
with open(EXE,'rb') as f: raw=f.read()
md=Cs(CS_ARCH_X86,CS_MODE_32); md.detail=True; md.skipdata=True
insns=[]
for s in pe.sections:
    if s.Characteristics & 0x20000000:
        insns.extend(md.disasm(s.get_data(), image_base+s.VirtualAddress))
insns.sort(key=lambda i:i.address)

def ops(ins):
    if getattr(ins,'id',0)==0:return []
    try:return ins.operands
    except CsError:return []

def callers(target):
    out=[]
    for idx,ins in enumerate(insns):
        if ins.mnemonic!='call':continue
        for op in ops(ins):
            if op.type==X86_OP_IMM and (op.imm & 0xffffffff)==target:
                out.append((idx,ins))
    return out

def refs(target):
    out=[]
    for idx,ins in enumerate(insns):
        for op in ops(ins):
            if op.type==X86_OP_IMM and (op.imm & 0xffffffff)==target:
                out.append((idx,ins));break
            if op.type==X86_OP_MEM and (op.mem.disp & 0xffffffff)==target:
                out.append((idx,ins));break
    return out

def window(idx,b=18,a=35):
    for j in range(max(0,idx-b),min(len(insns),idx+a)):
        i=insns[j];m='>>' if j==idx else '  '
        print(f'{m} {i.address:08X}: {i.mnemonic:<8} {i.op_str}')

def range_dump(start,end):
    for i in insns:
        if start<=i.address<end:
            print(f'{i.address:08X}: {i.mnemonic:<8} {i.op_str}')

def find_all(h,n):
    p=0;o=[]
    while True:
        p=h.find(n,p)
        if p<0:return o
        o.append(p);p+=1

def off_to_va(off):
    try:return image_base+pe.get_rva_from_offset(off)
    except:return None

print('=== PHYSICAL OBSERVATION ===')
print('Known good serial profile from PLC test: 19200 8O1 DTR/RTS ON')
print('HELLO TX = 43 4F 4E 2D 49 43 42 0D')
print('HELLO RX = 4E 01 09 35')

print('\n=== HELLO BUILDER 0x46F01D ===')
range_dump(0x0046F01D,0x0046F07A)

print('\n=== NEXT ADJACENT PG ROUTINES 0x46F07A..0x46F590 ===')
range_dump(0x0046F07A,0x0046F590)

print('\n=== CALLERS OF PG ROUTINES ===')
for target in [0x0046F01D,0x0046F07A,0x0046F0EF,0x0046F15B,0x0046F1D0,0x0046F250,0x0046F2C0,0x0046F330,0x0046F430,0x0046F5E6,0x0046F7F7,0x0046FA9E]:
    cs=callers(target)
    print(f'\nTARGET 0x{target:08X}: {len(cs)} caller(s)')
    for idx,ins in cs[:20]:
        print(f'-- caller at 0x{ins.address:08X} --')
        window(idx,14,28)

print('\n=== REFERENCES TO TX GLOBALS ===')
for target in [0x004FA7A8,0x004FA7A9,0x004FA7AA,0x004FA7AB,0x004FA8AC,0x004FA8B7,0x004FA8B9,0x00560360,0x00560364]:
    rr=refs(target)
    filt=[(idx,i) for idx,i in rr if 0x0046E000<=i.address<=0x00471000 or 0x004AD000<=i.address<=0x004AE500]
    print(f'GLOBAL 0x{target:08X}: {len(filt)} nearby refs')
    for idx,i in filt[:40]:print(f'  0x{i.address:08X}: {i.mnemonic:<8} {i.op_str}')

print('\n=== RAW STRING AND RESOURCE CHECK ===')
for label,pat in [('CON-ICB',b'CON-ICB'),('wrong F0 resource',bytes([0xF0,0x00,0x0F]))]:
    offs=find_all(raw,pat)
    print(label,[(hex(o),hex(off_to_va(o)) if off_to_va(o) else '?') for o in offs])

print('\n=== LINK UI RANGE 0x4AD680..0x4AE345 ===')
range_dump(0x004AD680,0x004AE345)
