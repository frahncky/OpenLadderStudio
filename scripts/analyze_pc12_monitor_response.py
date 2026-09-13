#!/usr/bin/env python3
"""Audita o parser de respostas do monitor Ladder do PC12 v2.1 sem I/O."""
from __future__ import annotations
import hashlib, pathlib, struct, sys

EXPECTED_SHA256 = '05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0'
JUMP_TABLE = 0x4C4F09
EXPECTED_TARGETS = (0x4C4F29, 0x4C4F47, 0x4C5142, 0x4C56B6, 0x4C5303, 0x4C5488, 0x4C54DF, 0x4C5536)
TYPE4 = 0x4C5303
TYPE7 = 0x4C5536


def u16(data, off): return struct.unpack_from('<H', data, off)[0]
def u32(data, off): return struct.unpack_from('<I', data, off)[0]


class PE:
    def __init__(self, data):
        self.data = data
        if data[:2] != b'MZ': raise ValueError('não é MZ')
        pe = u32(data, 0x3c)
        if data[pe:pe + 4] != b'PE\0\0': raise ValueError('não é PE')
        coff = pe + 4
        count = u16(data, coff + 2)
        opt_size = u16(data, coff + 16)
        opt = coff + 20
        self.base = u32(data, opt + 28)
        table = opt + opt_size
        self.sections = []
        for i in range(count):
            o = table + i * 40
            vs, va, rs, ro = struct.unpack_from('<IIII', data, o + 8)
            self.sections.append((va, max(vs, rs), ro))

    def off(self, va):
        r = va - self.base
        for start, size, raw in self.sections:
            if start <= r < start + size:
                return raw + r - start
        raise ValueError('VA fora das seções: %08X' % va)

    def read(self, va, n):
        o = self.off(va)
        return self.data[o:o + n]

    def cstr(self, va):
        o = self.off(va)
        e = self.data.find(b'\0', o, o + 128)
        if e < 0: raise ValueError('string sem terminador')
        return self.data[o:e].decode('cp1252')


def must_contain(block, pattern, label):
    if pattern not in block:
        raise SystemExit('FAIL: padrão ausente: ' + label)


def main(path):
    p = pathlib.Path(path)
    data = p.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    print('PC12_MONITOR_RESPONSE_STATIC')
    print('file=' + p.name)
    print('sha256=' + digest)
    if digest != EXPECTED_SHA256:
        raise SystemExit('FAIL: SHA-256 inesperado')

    pe = PE(data)
    targets = struct.unpack('<8I', pe.read(JUMP_TABLE, 32))
    print('jump_targets=' + ' '.join('type%d=0x%08X' % (i, x) for i, x in enumerate(targets)))
    if targets != EXPECTED_TARGETS:
        raise SystemExit('FAIL: jump table divergente')

    labels = {0: pe.cstr(0x4F7A70), 1: pe.cstr(0x4F7A79), 2: pe.cstr(0x4F7AAF),
              5: pe.cstr(0x4F7B11), 6: pe.cstr(0x4F7B1D)}
    fmts4 = [pe.cstr(x) for x in (0x4F7AE5, 0x4F7AF2, 0x4F7AFD)]
    fmts7 = [pe.cstr(x) for x in (0x4F7B29, 0x4F7B36, 0x4F7B41)]
    print('labels=' + repr(labels))
    print('numeric_formats_type4=' + repr(fmts4))
    print('numeric_formats_type7=' + repr(fmts7))
    if labels[1] != 'Off  ' or labels[2] != 'Off  ' or labels[5] != 'Off  ' or labels[6] != 'Off  ':
        raise SystemExit('FAIL: rótulos de estado divergentes')
    if fmts4 != ['%03d', '%05d', '%010u'] or fmts7 != ['%03d', '%05d', '%010u']:
        raise SystemExit('FAIL: formatos numéricos divergentes')

    b4 = pe.read(TYPE4, 0x15e)
    b7 = pe.read(TYPE7, 0x15e)
    must_contain(b4, bytes.fromhex('8A 82 30 02 53 00'), 'tipo4 largura1 byte+0')
    must_contain(b4, bytes.fromhex('8A 8A 31 02 53 00 C1 E1 08 03 C1'), 'tipo4 largura2 little-endian')
    must_contain(b4, bytes.fromhex('8A 8A 32 02 53 00'), 'tipo4 largura3 byte+2')
    must_contain(b4, bytes.fromhex('0F B6 92 33 02 53 00 C1 E2 08 03 CA C1 E1 10 03 C1'), 'tipo4 largura3 little-endian 32')
    must_contain(b7, bytes.fromhex('8A 82 31 02 53 00'), 'tipo7 largura1 byte+1')
    must_contain(b7, bytes.fromhex('8A 8A 30 02 53 00 C1 E1 08 03 C1'), 'tipo7 largura2 big-endian')
    must_contain(b7, bytes.fromhex('8A 8A 33 02 53 00'), 'tipo7 largura3 byte+3')
    must_contain(b7, bytes.fromhex('0F B6 92 32 02 53 00 C1 E2 08 03 CA C1 E1 10 03 C1'), 'tipo7 largura3 word-pair')

    consumption = {0: 0, 1: 1, 2: 1, 3: 0, 4: 4, 5: 2, 6: 2, 7: 4}
    print('consumption=' + ' '.join('type%d=%d' % (k, v) for k, v in consumption.items()))
    print('type4_width1=b0')
    print('type4_width2=b0|(b1<<8)')
    print('type4_width3=b0|(b1<<8)|(b2<<16)|(b3<<24)')
    print('type7_width1=b1')
    print('type7_width2=(b0<<8)|b1')
    print('type7_width3=((b0<<8)|b1)|(((b2<<8)|b3)<<16)')
    print('type1=active_high_bit; type2=active_low_bit')
    print('type5=payload[1]==selector; type6=payload[1]!=selector')
    print('RESULT=PASS')


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'pc12.exe')
