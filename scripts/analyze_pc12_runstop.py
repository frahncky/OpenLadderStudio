#!/usr/bin/env python3
"""Static, read-only helper to map short PG command builders to UI context.

It never executes pc12.exe and never touches a serial port.  The report is used
only to decide which frame must later be captured/validated before live TX.
"""
import pathlib, struct, re

BIN = pathlib.Path('src/OpenLadderStudio.Desktop/pc12.exe')
CANDIDATES = {
    0x0046F07A: '03 00 FC',
    0x0046F166: '11 00 EE',
    0x0046F1DC: '04 00 FB',
    0x0046F4FA: '02 00 FD',
    0x0046F570: '01 00 FE',
}

def pe_sections(data):
    pe = struct.unpack_from('<I', data, 0x3C)[0]
    nsec = struct.unpack_from('<H', data, pe + 6)[0]
    opt = struct.unpack_from('<H', data, pe + 20)[0]
    base = struct.unpack_from('<I', data, pe + 24 + 28)[0]
    secs=[]; off=pe+24+opt
    for _ in range(nsec):
        name=data[off:off+8].rstrip(b'\0').decode('latin-1')
        vs,va,rs,ra=struct.unpack_from('<IIII',data,off+8)
        secs.append((name,va,vs,ra,rs)); off+=40
    return base,secs

def f2va(off, base, secs):
    for _,va,vs,ra,rs in secs:
        if ra <= off < ra+rs:
            return base+va+(off-ra)
    return None

def va2f(va, base, secs):
    rva=va-base
    for _,sva,vs,ra,rs in secs:
        if sva <= rva < sva+rs:
            return ra+(rva-sva)
    return None

def text_range(base,secs):
    for name,va,vs,ra,rs in secs:
        if name=='.text': return ra,ra+rs,base+va-ra
    raise RuntimeError('.text missing')

def find_calls(data,start,end,delta,target):
    hits=[]
    for o in range(start,end-5):
        if data[o] != 0xE8: continue
        rel=struct.unpack_from('<i',data,o+1)[0]
        site=o+delta
        if site+5+rel == target: hits.append((o,site))
    return hits

def function_start(data, off, floor):
    # Old VC/MFC code in this image commonly uses push ebp / mov ebp,esp.
    for back in range(0,6000):
        o=off-back
        if o < floor+3: break
        if data[o:o+3] == b'\x55\x8b\xec': return o
    return None

def strings(data, base, secs):
    out=[]
    for m in re.finditer(rb'[ -~]{4,}\x00', data):
        raw=m.group()[:-1]
        txt=raw.decode('latin-1','replace')
        if any(k in txt.upper() for k in ('RUN','STOP','PLC','REMOTE','START')):
            va=f2va(m.start(),base,secs)
            if va is not None: out.append((m.start(),va,txt))
    return out

def refs_to_va(blob, start, end, va):
    needle=struct.pack('<I',va)
    pos=start; hits=[]
    while True:
        p=blob.find(needle,pos,end)
        if p<0: break
        hits.append(p); pos=p+1
    return hits

def nearby_ascii(data, center, radius=2500):
    lo=max(0,center-radius); hi=min(len(data),center+radius)
    vals=[]
    for m in re.finditer(rb'[ -~]{5,}\x00', data[lo:hi]):
        s=m.group()[:-1].decode('latin-1','replace')
        if any(k in s.upper() for k in ('RUN','STOP','PLC','PASSWORD','ERROR','CONNECT')):
            vals.append(s)
    return vals[:30]

def main():
    data=BIN.read_bytes(); base,secs=pe_sections(data)
    ts,te,delta=text_range(base,secs)
    strs=strings(data,base,secs)
    print('PC12 RUN/STOP static cross-reference report')
    print('binary:', BIN, 'size=',len(data),'imagebase=0x%08X'%base)
    print('\nRelevant strings with mapped VA:')
    for _,va,s in strs[:200]: print('  0x%08X  %s'%(va,s))

    print('\nCandidate short-frame builders and callers:')
    for target,frame in CANDIDATES.items():
        calls=find_calls(data,ts,te,delta,target)
        print('\n[%s] builder=0x%08X callers=%d'%(frame,target,len(calls)))
        for fo,site in calls:
            fs=function_start(data,fo,ts)
            if fs is None:
                print('  call=0x%08X function=?'%site); continue
            fva=fs+delta
            # Limit context to the next 8 KB; string xrefs are exact immediates.
            fend=min(te,fs+8192)
            exact=[]
            for _,sva,s in strs:
                if refs_to_va(data,fs,fend,sva): exact.append(s)
            print('  call=0x%08X function=0x%08X'%(site,fva))
            for s in exact[:20]: print('    xref-string:',s)
            for s in nearby_ascii(data,fo): print('    nearby-string:',s)

if __name__=='__main__': main()
