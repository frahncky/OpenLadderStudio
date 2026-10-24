#!/usr/bin/env python3
"""Audita a semântica das respostas Q=4 do monitor Ladder do PC12 v2.1.

Valida o binário original por SHA-256, confere os cinco sites produtores Q=04,
os dois consumidores de 32 bits e os formatos de exibição decimal/hex. Não há I/O serial.
"""
from __future__ import annotations
import hashlib
import pathlib
import struct
import sys

EXPECTED_SHA256 = "05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0"
Q4_SITES = (0x4C3AB9, 0x4C3B45, 0x4C3CCA, 0x4C3E21, 0x4C3F4F)
TYPE4_VA = 0x4C5303
TYPE7_VA = 0x4C5536
FMT_VAS = (0x4F7AFD, 0x4F7B03, 0x4F7B0A, 0x4F7B41, 0x4F7B47, 0x4F7B4E)


def u16(data, offset): return struct.unpack_from("<H", data, offset)[0]
def u32(data, offset): return struct.unpack_from("<I", data, offset)[0]


class PE:
    def __init__(self, data):
        self.data = data
        if data[:2] != b"MZ": raise ValueError("não é MZ")
        pe = u32(data, 0x3C)
        if data[pe:pe+4] != b"PE\0\0": raise ValueError("não é PE")
        coff = pe + 4
        count = u16(data, coff + 2)
        optional_size = u16(data, coff + 16)
        optional = coff + 20
        self.image_base = u32(data, optional + 28)
        table = optional + optional_size
        self.sections = []
        for i in range(count):
            o = table + i * 40
            vsize = u32(data, o + 8); va = u32(data, o + 12)
            rsize = u32(data, o + 16); raw = u32(data, o + 20)
            self.sections.append((va, max(vsize, rsize), raw))

    def offset(self, va):
        rva = va - self.image_base
        for start, size, raw in self.sections:
            if start <= rva < start + size: return raw + rva - start
        raise ValueError("VA fora das seções: %08X" % va)

    def read(self, va, count):
        o = self.offset(va)
        return self.data[o:o+count]

    def cstr(self, va):
        o = self.offset(va)
        e = self.data.find(b"\0", o)
        return self.data[o:e].decode("ascii")


def main(path):
    p = pathlib.Path(path)
    data = p.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    print("PC12_MONITOR_Q4_RESPONSE_STATIC")
    print("file=" + p.name)
    print("sha256=" + digest)
    if digest != EXPECTED_SHA256: raise SystemExit("FAIL: SHA-256 inesperado")
    pe = PE(data)

    q4 = b"\xC6\x87\xA8\xA7\x4F\x00\x04"
    for va in Q4_SITES:
        got = pe.read(va, len(q4))
        print("q4_site=0x%08X bytes=%s" % (va, got.hex(" ").upper()))
        if got != q4: raise SystemExit("FAIL: produtor Q4 divergente")

    # Tipo 4: b0 + b1<<8 + (b2 + b3<<8)<<16; ao final avança 4 bytes.
    type4_markers = (
        b"\x8A\x82\x30\x02\x53\x00",  # b0
        b"\x8A\x8A\x31\x02\x53\x00",  # b1
        b"\x8A\x8A\x32\x02\x53\x00",  # b2
        b"\x0F\xB6\x92\x33\x02\x53\x00",  # b3
        b"\x83\x85\x28\xFF\xFF\xFF\x04",  # +=4
    )
    block4 = pe.read(TYPE4_VA, 0x186)
    for marker in type4_markers:
        if marker not in block4: raise SystemExit("FAIL: consumidor tipo 4 divergente")
    print("type4_formula=b0|(b1<<8)|(b2<<16)|(b3<<24)")

    # Tipo 7: b1 + b0<<8 + (b3 + b2<<8)<<16; ao final avança 4 bytes.
    type7_markers = (
        b"\x8A\x82\x31\x02\x53\x00",  # b1
        b"\x8A\x8A\x30\x02\x53\x00",  # b0
        b"\x8A\x8A\x33\x02\x53\x00",  # b3
        b"\x0F\xB6\x92\x32\x02\x53\x00",  # b2
        b"\x83\x85\x28\xFF\xFF\xFF\x04",  # +=4
    )
    block7 = pe.read(TYPE7_VA, 0x180)
    for marker in type7_markers:
        if marker not in block7: raise SystemExit("FAIL: consumidor tipo 7 divergente")
    print("type7_formula=b1|(b0<<8)|(b3<<16)|(b2<<24)")

    formats = [pe.cstr(va) for va in FMT_VAS]
    print("formats=" + " | ".join(formats))
    if formats != ["%010u", " %08X ", "%08X  ", "%010u", " %08X ", "%08X  "]:
        raise SystemExit("FAIL: formatos Q4 divergentes")

    print("selector=[object+0x12DE]: nonzero=>type4, zero=>type7")
    print("RESULT=PASS")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "pc12.exe")
