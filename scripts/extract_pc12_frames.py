#!/usr/bin/env python3
"""
Enumera os quadros que o pc12.exe monta para transmitir ao TP02.

Metodo
------
A rotina de transmissao em 0x46F5E6 chama WriteFile com o buffer em 0x4FA7A8 e o
comprimento lido de 0x4FA8AC. Portanto todo quadro transmitido e montado por
escritas imediatas nesses dois enderecos absolutos:

    mov byte  ptr [0x4FA7A8 + n], imm8    ->  C6 05 <addr32> <imm8>
    mov word  ptr [0x4FA7A8 + n], imm16   ->  66 C7 05 <addr32> <imm16>
    mov dword ptr [0x4FA8AC],     imm32   ->  C7 05 <addr32> <imm32>

Varrer a secao .text atras desses padroes recupera os quadros de forma mecanica,
sem depender de adivinhacao. Escritas proximas no codigo pertencem ao mesmo quadro.

Este script apenas le o binario. Nao executa nada e nao o modifica.
"""

import argparse
import pathlib
import struct
from collections import defaultdict

TX_BUFFER = 0x4FA7A8
TX_LENGTH = 0x4FA8AC
BUFFER_SPAN = 64          # quantos bytes apos o inicio ainda contam como quadro
CLUSTER_GAP = 160         # distancia maxima em bytes de codigo dentro de um quadro


def pe_sections(data):
    pe = struct.unpack_from('<I', data, 0x3C)[0]
    nsec = struct.unpack_from('<H', data, pe + 6)[0]
    opt_size = struct.unpack_from('<H', data, pe + 20)[0]
    base = struct.unpack_from('<I', data, pe + 24 + 28)[0]
    secs = []
    off = pe + 24 + opt_size
    for _ in range(nsec):
        name = data[off:off + 8].rstrip(b'\0').decode('latin-1')
        vsize, vaddr, rsize, raddr = struct.unpack_from('<IIII', data, off + 8)
        secs.append((name, vaddr, vsize, raddr, rsize))
        off += 40
    return base, secs


def text_span(base, secs):
    for name, vaddr, vsize, raddr, rsize in secs:
        if name == '.text':
            return raddr, raddr + min(vsize, rsize) if rsize else raddr + vsize, base + vaddr - raddr
    raise SystemExit('.text nao encontrada')


def scan(data, start, end, delta):
    """Devolve (va_da_instrucao, alvo, tamanho_em_bytes, valor)."""
    hits = []
    i = start
    while i < end - 10:
        b = data[i]
        if b == 0xC6 and data[i + 1] == 0x05:
            target = struct.unpack_from('<I', data, i + 2)[0]
            if TX_BUFFER <= target < TX_BUFFER + BUFFER_SPAN or target == TX_LENGTH:
                hits.append((i + delta, target, 1, data[i + 6]))
            i += 7
            continue
        if b == 0xC7 and data[i + 1] == 0x05:
            target = struct.unpack_from('<I', data, i + 2)[0]
            if TX_BUFFER <= target < TX_BUFFER + BUFFER_SPAN or target == TX_LENGTH:
                hits.append((i + delta, target, 4, struct.unpack_from('<I', data, i + 6)[0]))
            i += 10
            continue
        if b == 0x66 and data[i + 1] == 0xC7 and data[i + 2] == 0x05:
            target = struct.unpack_from('<I', data, i + 3)[0]
            if TX_BUFFER <= target < TX_BUFFER + BUFFER_SPAN or target == TX_LENGTH:
                hits.append((i + delta, target, 2, struct.unpack_from('<H', data, i + 7)[0]))
            i += 9
            continue
        i += 1
    return hits


def cluster(hits):
    groups, current = [], []
    for hit in hits:
        if current and hit[0] - current[-1][0] > CLUSTER_GAP:
            groups.append(current)
            current = []
        current.append(hit)
    if current:
        groups.append(current)
    return groups


def render(groups):
    print('%-3s %-12s %-26s %s' % ('#', 'codigo em', 'quadro montado', 'comprimento TX'))
    print('-' * 78)
    for n, g in enumerate(groups, 1):
        frame = {}
        length = None
        for va, target, size, value in g:
            if target == TX_LENGTH:
                length = value
                continue
            off = target - TX_BUFFER
            for k in range(size):
                frame[off + k] = (value >> (8 * k)) & 0xFF
        if not frame:
            continue
        top = max(frame)
        shown = ' '.join('%02X' % frame[i] if i in frame else '..' for i in range(top + 1))
        total = sum(frame.values()) % 256
        mark = '  soma%256=FF' if total == 0xFF else ''
        print('%-3d 0x%08X   %-26s %s%s'
              % (n, g[0][0], shown, length if length is not None else '-', mark))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('binary', nargs='?',
                    default='PC12_v2.1_Windows7_v3_portatil/pc12.exe')
    args = ap.parse_args()
    data = pathlib.Path(args.binary).read_bytes()
    base, secs = pe_sections(data)
    start, end, delta = text_span(base, secs)
    hits = scan(data, start, end, delta)
    print('escritas encontradas no buffer/comprimento TX: %d\n' % len(hits))
    render(cluster(hits))


if __name__ == '__main__':
    main()
