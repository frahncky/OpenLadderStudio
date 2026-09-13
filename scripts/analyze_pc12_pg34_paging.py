#!/usr/bin/env python3
"""Audita a paginação PG34 do PC12 v2.1 sem executar I/O."""
from __future__ import annotations
import hashlib
import pathlib
import struct
import sys

EXPECTED_SHA256 = "05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0"
INITIAL_BUILDERS = (0x4AFA54, 0x4B1D62)
DYNAMIC_BUILDER = 0x4B0DFC
SECOND_DYNAMIC_BUILDER = 0x4B2BE1
FORMAT_VA = 0x4F4FB2
FORMAT_REF_VA = 0x4B0D4B


def u16(data, offset):
    return struct.unpack_from("<H", data, offset)[0]


def u32(data, offset):
    return struct.unpack_from("<I", data, offset)[0]


class PE:
    def __init__(self, data):
        self.data = data
        if data[:2] != b"MZ":
            raise ValueError("não é MZ")
        pe = u32(data, 0x3C)
        if data[pe:pe + 4] != b"PE\0\0":
            raise ValueError("não é PE")
        coff = pe + 4
        count = u16(data, coff + 2)
        optional_size = u16(data, coff + 16)
        optional = coff + 20
        self.image_base = u32(data, optional + 28)
        table = optional + optional_size
        self.sections = []
        for i in range(count):
            offset = table + i * 40
            virtual_address = u32(data, offset + 12)
            size = max(u32(data, offset + 8), u32(data, offset + 16))
            raw = u32(data, offset + 20)
            self.sections.append((virtual_address, size, raw))

    def offset(self, va):
        rva = va - self.image_base
        for start, size, raw in self.sections:
            if start <= rva < start + size:
                return raw + rva - start
        raise ValueError("VA fora das seções: %08X" % va)

    def read(self, va, count):
        offset = self.offset(va)
        return self.data[offset:offset + count]


def checksum(prefix):
    return (0xFF - (sum(prefix) & 0xFF)) & 0xFF


def frame(start):
    prefix = [0x34, 0x03, (start >> 8) & 0xFF, start & 0xFF, 0xA0]
    return bytes(prefix + [checksum(prefix)])


def main(path):
    target = pathlib.Path(path)
    data = target.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    print("PC12_PG34_PAGING_STATIC")
    print("file=" + target.name)
    print("sha256=" + digest)
    if digest != EXPECTED_SHA256:
        raise SystemExit("FAIL: SHA-256 inesperado")
    pe = PE(data)

    initial = bytes.fromhex(
        "C6 05 A8 A7 4F 00 34 "
        "C6 05 A9 A7 4F 00 03 "
        "C6 05 AA A7 4F 00 00 "
        "C6 05 AB A7 4F 00 00 "
        "C6 05 AC A7 4F 00 A0 "
        "C6 05 AD A7 4F 00 28"
    )
    for va in INITIAL_BUILDERS:
        found = pe.read(va, len(initial))
        print("initial_builder=0x%08X match=%s" % (va, str(found == initial).lower()))
        if found != initial:
            raise SystemExit("FAIL: builder inicial divergente")

    dynamic = pe.read(DYNAMIC_BUILDER, 0x48)
    checks = (
        bytes.fromhex("88 15 AA A7 4F 00"),
        bytes.fromhex("88 0D AB A7 4F 00"),
        bytes.fromhex("C6 05 AC A7 4F 00 A0"),
        bytes.fromhex("88 15 AD A7 4F 00"),
    )
    for pattern in checks:
        if pattern not in dynamic:
            raise SystemExit("FAIL: builder dinâmico principal divergente")
    print("dynamic_builder=0x%08X high_low_q_checksum=true" % DYNAMIC_BUILDER)

    dynamic2 = pe.read(SECOND_DYNAMIC_BUILDER, 0x48)
    checks2 = (
        bytes.fromhex("88 0D AA A7 4F 00"),
        bytes.fromhex("A2 AB A7 4F 00"),
        bytes.fromhex("C6 05 AC A7 4F 00 A0"),
        bytes.fromhex("88 0D AD A7 4F 00"),
    )
    for pattern in checks2:
        if pattern not in dynamic2:
            raise SystemExit("FAIL: segundo builder dinâmico divergente")
    print("dynamic_builder=0x%08X two_start_bytes_q_checksum=true" % SECOND_DYNAMIC_BUILDER)

    if pe.read(FORMAT_VA, 5) != b"%04X\0":
        raise SystemExit("FAIL: formato %04X ausente")
    if pe.read(FORMAT_REF_VA, 4) != struct.pack("<I", FORMAT_VA):
        raise SystemExit("FAIL: referência a %04X ausente")
    print("cursor_format_va=0x%08X value=%%04X" % FORMAT_VA)
    print("cursor_format_ref=0x%08X" % FORMAT_REF_VA)

    for start in (0, 80, 160, 240, 320, 400, 1440, 3920):
        print("start=%d frame=%s" % (start, frame(start).hex(" ").upper()))
    print("boundary_00FF_to_0100=start240_then320")
    print("capacity_1500_pages=19 last_start=1440")
    print("capacity_4000_pages=50 last_start=3920")
    print("checksum_note=swapping_start_bytes_preserves_additive_checksum")
    print("RESULT=PASS")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "pc12.exe")
