#!/usr/bin/env python3
"""Audita o dispatcher do monitor Ladder do PC12 v2.1 sem executar I/O.

Usa apenas a biblioteca padrão. Confere SHA-256, mapeia VA->offset a partir do PE,
valida a tabela 70001..70027 e os sites nativos que emitem quantidade Q=4 em
quadros PG0A. Não transmite nada.
"""
from __future__ import annotations
import hashlib
import pathlib
import struct
import sys

EXPECTED_SHA256 = "05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0"
DISPATCH_VA = 0x4C3959
TARGETS_VA = 0x4C3974
Q4_SITES = (0x4C3AB9, 0x4C3B45, 0x4C3CCA, 0x4C3E21, 0x4C3F4F)
EXPECTED_CLASSES = bytes([4, 4, 0, 0, 4, 1, 1, 3, 2, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1])
EXPECTED_TARGETS = (0x4C3FFD, 0x4C3A89, 0x4C3BA4, 0x4C3988, 0x4C39A9)


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
        section_table = optional + optional_size
        self.sections = []
        for index in range(count):
            offset = section_table + index * 40
            name = data[offset:offset + 8].split(b"\0", 1)[0].decode("ascii", "replace")
            virtual_size = u32(data, offset + 8)
            virtual_address = u32(data, offset + 12)
            raw_size = u32(data, offset + 16)
            raw_offset = u32(data, offset + 20)
            self.sections.append((name, virtual_address, max(virtual_size, raw_size), raw_offset))

    def file_offset(self, va):
        rva = va - self.image_base
        for _name, start, size, raw in self.sections:
            if start <= rva < start + size:
                return raw + rva - start
        raise ValueError("VA fora das seções: %08X" % va)

    def read(self, va, count):
        offset = self.file_offset(va)
        return self.data[offset:offset + count]


def ascii_context(data, needle, radius=96):
    hits = []
    start = 0
    while True:
        index = data.find(needle, start)
        if index < 0:
            break
        lo = max(0, index - radius)
        hi = min(len(data), index + len(needle) + radius)
        raw = data[lo:hi]
        hits.append("".join(chr(value) if 32 <= value < 127 else "." for value in raw))
        start = index + 1
    return hits


def main(path):
    target = pathlib.Path(path)
    data = target.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    print("PC12_MONITOR_DISPATCH_STATIC")
    print("file=" + target.name)
    print("sha256=" + digest)
    if digest != EXPECTED_SHA256:
        raise SystemExit("FAIL: SHA-256 inesperado")

    pe = PE(data)
    classes = pe.read(DISPATCH_VA, 27)
    print("dispatcher_ids=70001..70027")
    print("class_bytes=" + " ".join("%02X" % value for value in classes))
    if classes != EXPECTED_CLASSES:
        raise SystemExit("FAIL: tabela de classes divergente")

    target_raw = pe.read(TARGETS_VA, 20)
    targets = tuple(u32(target_raw, index * 4) for index in range(5))
    print("targets=" + " ".join("class%d=0x%08X" % (index, value) for index, value in enumerate(targets)))
    if targets != EXPECTED_TARGETS:
        raise SystemExit("FAIL: tabela de destinos divergente")

    for identifier, cls in zip(range(70001, 70028), classes):
        print("id=%d class=%d target=0x%08X" % (identifier, cls, targets[cls]))

    q4_pattern = b"\xC6\x87\xA8\xA7\x4F\x00\x04"
    for va in Q4_SITES:
        found = pe.read(va, len(q4_pattern))
        print("q4_site=0x%08X bytes=%s" % (va, found.hex(" ").upper()))
        if found != q4_pattern:
            raise SystemExit("FAIL: site Q=4 divergente em %08X" % va)

    # Somente vínculos que aparecem textualmente junto aos IDs no executável.
    associations = {
        70013: b"TMR",
        70020: b"CNT",
        70026: b"TMR",
        70015: b"OUT",
        70022: b"F-05",
        70023: b"F-06",
        70016: b"F-34",
        70024: b"F-41",
    }
    for identifier, label in associations.items():
        contexts = ascii_context(data, str(identifier).encode("ascii"))
        observed = any(label.decode("ascii") in context for context in contexts)
        print("text_link=%d:%s observed=%s" % (identifier, label.decode("ascii"), str(observed).lower()))
        if not observed:
            raise SystemExit("FAIL: associação textual não encontrada")

    print("q4_reachable_ids=70006,70007,70009,70017,70027")
    print("q4_reachable_classes=1,2")
    print("RESULT=PASS")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "pc12.exe")
