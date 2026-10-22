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
KEYS = ('RUN','STOP','PLC','REMOTE','START','PROGRAM','MONITOR')

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
    for back in range(0,6000):
        o=off-back
        if o < floor+3: break
        if data[o:o+3] == b'\x55\x8b\xec': return o
    return None

def next_function_start(data, off, ceiling, max_forward=10000):
    top=min(ceiling,off+max_forward)
    p=data.find(b'\x55\x8b\xec',off+3,top)
    return p if p >= 0 else top

def all_strings(data, base, secs):
    out=[]
    for m in re.finditer(rb'[ -~]{4,}\x00', data):
        raw=m.group()[:-1]
        txt=raw.decode('latin-1','replace')
        va=f2va(m.start(),base,secs)
        if va is not None: out.append((m.start(),va,txt))
    return out

def refs_to_va(blob,start,end,va):
    needle=struct.pack('<I',va); pos=start; hits=[]
    while True:
        p=blob.find(needle,pos,end)
        if p<0: break
        hits.append(p); pos=p+1
    return hits

def hex_window(data,off,before=40,after=72):
    lo=max(0,off-before); hi=min(len(data),off+after)
    return lo, ' '.join('%02X'%b for b in data[lo:hi])

def main():
    data=BIN.read_bytes(); base,secs=pe_sections(data)
    ts,te,delta=text_range(base,secs)
    strs=all_strings(data,base,secs)
    relevant=[x for x in strs if any(k in x[2].upper() for k in KEYS)]
    print('PC12 RUN/STOP static cross-reference report v2')
    print('binary:',BIN,'size=',len(data),'imagebase=0x%08X'%base)
    print('\nRelevant strings with mapped VA:')
    for _,va,s in relevant[:240]: print('  0x%08X  %s'%(va,s))

    seen_functions=set()
    print('\nCandidate short-frame builders and callers:')
    for target,frame in CANDIDATES.items():
        calls=find_calls(data,ts,te,delta,target)
        print('\n[%s] builder=0x%08X callers=%d'%(frame,target,len(calls)))
        for fo,site in calls:
            fs=function_start(data,fo,ts)
            if fs is None:
                print('  call=0x%08X function=?'%site); continue
            fe=next_function_start(data,fs,te)
            fva=fs+delta; feva=fe+delta
            print('  call=0x%08X function=0x%08X..0x%08X'%(site,fva,feva))
            # Show exact string-reference sites in execution-address order.
            refs=[]
            for _,sva,s in relevant:
                for p in refs_to_va(data,fs,fe,sva): refs.append((p+delta,sva,s))
            for xva,sva,s in sorted(refs):
                print('    xref@0x%08X -> 0x%08X  %s'%(xva,sva,s))
            lo,h=hex_window(data,fo)
            print('    bytes@0x%08X: %s'%(lo+delta,h))
            seen_functions.add((fs,fe))

    # Full ordered event list for the small adjacent mode-control functions.
    print('\nOrdered events per unique candidate caller function:')
    for fs,fe in sorted(seen_functions):
        print('\n  FUNCTION 0x%08X..0x%08X'%(fs+delta,fe+delta))
        events=[]
        for target,frame in CANDIDATES.items():
            for fo,site in find_calls(data,fs,fe,delta,target): events.append((site,'CALL '+frame))
        for _,sva,s in relevant:
            for p in refs_to_va(data,fs,fe,sva): events.append((p+delta,'STR '+s))
        for va,label in sorted(events): print('    0x%08X  %s'%(va,label))

if __name__=='__main__': main()
