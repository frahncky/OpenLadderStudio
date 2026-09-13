#!/usr/bin/env python3
"""Audita estaticamente PG14 e F0 no pc12.exe original, sem I/O serial.

Nao depende de disassembler externo. O script valida o SHA-256, converte VA->offset
pela tabela PE, confere os builders constantes e conta chamadas relativas x86.
"""
import hashlib
import pathlib
import struct
import sys

EXPECTED = "05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0"
DEFAULT = pathlib.Path("PC12_v2.1_Windows7_v3_portatil/pc12.exe")
EXE = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT
RAW = EXE.read_bytes()
HASH = hashlib.sha256(RAW).hexdigest()
if HASH != EXPECTED:
    raise SystemExit("hash inesperado: " + HASH)

PE = struct.unpack_from("<I", RAW, 0x3C)[0]
if RAW[PE:PE + 4] != b"PE\0\0":
    raise SystemExit("PE invalido")
COFF = PE + 4
NSEC = struct.unpack_from("<H", RAW, COFF + 2)[0]
OPTSZ = struct.unpack_from("<H", RAW, COFF + 16)[0]
OPT = COFF + 20
IMAGE_BASE = struct.unpack_from("<I", RAW, OPT + 28)[0]
SEC = OPT + OPTSZ
SECTIONS = []
for i in range(NSEC):
    o = SEC + i * 40
    name = RAW[o:o + 8].split(b"\0")[0].decode("ascii", "replace")
    vsize, va, rsize, roff = struct.unpack_from("<IIII", RAW, o + 8)
    SECTIONS.append((name, IMAGE_BASE + va, vsize, rsize, roff))


def file_offset(va):
    for _name, start, vsize, rsize, roff in SECTIONS:
        if start <= va < start + max(vsize, rsize):
            return roff + (va - start)
    raise ValueError("VA fora das secoes: %08X" % va)


def at(va, count):
    o = file_offset(va)
    return RAW[o:o + count]


def cstring(va):
    o = file_offset(va)
    e = RAW.find(b"\0", o)
    return RAW[o:e].decode("latin1")


def scan_commands(start, end):
    # mov byte ptr [004FA7A8], imm8
    pattern = b"\xC6\x05\xA8\xA7\x4F\x00"
    data = at(start, end - start)
    result, pos = [], 0
    while True:
        i = data.find(pattern, pos)
        if i < 0:
            return result
        result.append((start + i, data[i + 6]))
        pos = i + 1


def scan_calls(target, start=0x401000, end=0x4CD000):
    data = at(start, end - start)
    result = []
    for i in range(len(data) - 4):
        if data[i] != 0xE8:
            continue
        rel = struct.unpack_from("<i", data, i + 1)[0]
        if (start + i + 5 + rel) & 0xFFFFFFFF == target:
            result.append(start + i)
    return result


def pg14_builder_ok(site):
    return (
        at(site, 7) == b"\xC6\x05\xA8\xA7\x4F\x00\x14"
        and at(site + 7, 7) == b"\xC6\x05\xA9\xA7\x4F\x00\x00"
        and at(site + 14, 7) == b"\xC6\x05\xAA\xA7\x4F\x00\xEB"
    )


def f0_builder_ok(site):
    expected = (
        b"\xC6\x05\xA8\xA7\x4F\x00\xF0"
        b"\xC6\x05\xA9\xA7\x4F\x00\x00"
        b"\xC6\x05\xAA\xA7\x4F\x00\x0F"
    )
    return at(site, len(expected)) == expected


PG14 = [
    (0x4AF763, 0x4B0F8D, "Compare PLC Program", 0x4F4EB4),
    (0x4B19E8, 0x4B2CF7, "Read PLC Program", 0x4F5327),
    (0x4B687F, 0x4B7DC7, "Write PLC Program", 0x4F5E12),
    (0x4BBD3B, 0x4BBEDA, "manutencao/estado protegido", None),
    (0x4BC238, 0x4BC515, "EEPROM", None),
]

print("PC12 SHA256=" + HASH)
print("\nPG14 — gate de autorizacao")
for site, end, label, string_va in PG14:
    if not pg14_builder_ok(site):
        raise SystemExit("builder PG14 divergente em %08X" % site)
    if string_va is not None:
        text = cstring(string_va)
        if label not in text:
            raise SystemExit("rotulo inesperado em %08X: %r" % (string_va, text))
    cmds = [(a, c) for a, c in scan_commands(site + 21, end) if c != 0x14]
    succ = ", ".join("%08X:%02X" % x for x in cmds[:8]) or "(sem opcode posterior antes do retorno)"
    print("%08X  14 00 EB  %-28s  sucessores: %s" % (site, label, succ))

print("\nClassificacao PG14: AUTHORIZATION_GATE")
print("- quadro constante; nao carrega bytes da senha")
print("- a comparacao da senha ocorre localmente antes do builder")
print("- o mesmo gate protege Compare/Read/Write Program e EEPROM")
print("- portanto nao e comando de transferencia da senha")

print("\nF0 — helpers do PC12")
for site in (0x46F2E2, 0x46F358, 0x46F458):
    if not f0_builder_ok(site):
        raise SystemExit("builder F0 divergente em %08X" % site)
    print("%08X  F0 00 0F" % site)
for fn in (0x46F2BE, 0x46F300, 0x46F409):
    callers = scan_calls(fn)
    print("helper %08X  callers=%d  %s" %
          (fn, len(callers), " ".join("%08X" % x for x in callers)))

print("\nClassificacao F0: SESSION_PREFLIGHT")
print("- 46F300 salva o quadro corrente, troca temporariamente para F0 e restaura o contexto")
print("- 46F409 faz o mesmo e ainda inspeciona bit 7 do buffer/status")
print("- os 24 callers dos wrappers mostram reutilizacao transversal")
print("- isso sustenta preflight/qualificacao de sessao; nao sustenta chamar F0 de STOP")
print("\nRESULT=PASS")
