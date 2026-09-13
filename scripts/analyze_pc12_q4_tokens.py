#!/usr/bin/env python3
"""Audita os cinco tokens Ladder que alcançam ramos Q=4 no monitor do PC12 v2.1."""
from __future__ import annotations
import hashlib, pathlib, struct, sys
EXPECTED_SHA256 = "05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0"
TOKENS = {70006: 0x11176, 70007: 0x11177, 70009: 0x11179, 70017: 0x11181, 70027: 0x1118B}
CLASS = {70006: 1, 70007: 1, 70009: 2, 70017: 2, 70027: 1}
Q4_IDS = (70006, 70007, 70009, 70017, 70027)

class PE:
    def __init__(self, data):
        self.data = data
        if data[:2] != b"MZ": raise ValueError("not MZ")
        pe = struct.unpack_from("<I", data, 0x3C)[0]
        if data[pe:pe + 4] != b"PE\0\0": raise ValueError("not PE")
        coff = pe + 4
        count = struct.unpack_from("<H", data, coff + 2)[0]
        osz = struct.unpack_from("<H", data, coff + 16)[0]
        opt = coff + 20
        self.base = struct.unpack_from("<I", data, opt + 28)[0]
        st = opt + osz
        self.sections = []
        for i in range(count):
            o = st + i * 40
            name = data[o:o + 8].split(b"\0", 1)[0].decode("ascii", "replace")
            vs, va, rs, ro = struct.unpack_from("<IIII", data, o + 8)
            self.sections.append((name, va, max(vs, rs), ro))

    def off(self, va):
        rva = va - self.base
        for _name, start, size, raw in self.sections:
            if start <= rva < start + size: return raw + rva - start
        raise ValueError("VA outside image: %08X" % va)

    def read(self, va, count):
        o = self.off(va)
        return self.data[o:o + count]

    def cstr(self, va):
        o = self.off(va)
        e = self.data.find(b"\0", o)
        return self.data[o:e]

def need(pe, va, expected, label):
    got = pe.read(va, len(expected))
    print("%s=0x%08X bytes=%s" % (label, va, got.hex(" ").upper()))
    if got != expected: raise SystemExit("FAIL: " + label)

def main(path):
    p = pathlib.Path(path)
    data = p.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    print("PC12_Q4_TOKEN_AUDIT")
    print("file=" + p.name)
    print("sha256=" + digest)
    if digest != EXPECTED_SHA256: raise SystemExit("FAIL: SHA-256")
    pe = PE(data)

    # 70006: o validador compara o token e salta diretamente para a mensagem CNT.
    need(pe, 0x454FE1, b"\x3D\x76\x11\x01\x00", "token70006_cmp")
    cnt = pe.cstr(0x4DA12E)
    print("token70006_error=" + cnt.decode("ascii"))
    if b"CNT Circuit is incorrect" not in cnt: raise SystemExit("FAIL: CNT evidence")

    # 70007: token estrutural percorrido para trás, decrementando a posição enquanto se repete.
    need(pe, 0x41AA43, b"\xBF\x77\x11\x01\x00", "token70007_seed")
    need(pe, 0x41AAB8, b"\x81\xFF\x77\x11\x01\x00", "token70007_backscan_cmp1")
    need(pe, 0x41AACF, b"\x81\xFF\x77\x11\x01\x00", "token70007_backscan_cmp2")

    # 70009: membro da família de circuito de saída; a mesma trilha emite OUT inválido.
    need(pe, 0x455273, b"\x3D\x79\x11\x01\x00", "token70009_cmp")
    out = pe.cstr(0x4DA23B)
    print("token70009_error_family=" + out.decode("ascii"))
    if b"OUT Circuit is incorrect" not in out: raise SystemExit("FAIL: OUT evidence")

    # 70017: agrupado com 70008/70016/70020 no validador de topologia especial.
    need(pe, 0x44808A, b"\x81\xFA\x81\x11\x01\x00", "token70017_cmp")
    companions = (b"\x78\x11\x01\x00", b"\x80\x11\x01\x00", b"\x84\x11\x01\x00")
    region = pe.read(0x448071, 0x50)
    if not all(x in region for x in companions): raise SystemExit("FAIL: 70017 companion family")
    print("token70017_companions=70008,70016,70020")

    # 70027: Boolean->Ladder emite 70008 e, no mesmo bloco, o literal 70027 de um operando.
    need(pe, 0x420FB7, b"\x68\x78\x11\x01\x00", "token70027_pair_token70008")
    if pe.cstr(0x4D3B18) != b"70027": raise SystemExit("FAIL: 70027 literal")
    print("token70027_literal=70027")
    print("token70027_shape=70027 NNNNN 00000")
    need(pe, 0x44BC65, b"\x3D\x8B\x11\x01\x00", "token70027_structural_cmp")

    print("q4_ids=" + ",".join(map(str, Q4_IDS)))
    for ident in Q4_IDS:
        print("id=%d token=0x%05X class=%d" % (ident, TOKENS[ident], CLASS[ident]))
    print("classification=70006:CNT;70007:structural-continuation;70009:output-circuit-family;70017:special-topology-family;70027:boolean-ladder-structural")
    print("type_selection=per-branch/object-state;not-fixed-per-token")
    print("RESULT=PASS")

if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "pc12.exe")
