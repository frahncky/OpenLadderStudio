#!/usr/bin/env python3
"""
Recupera a sequencia de paginacao do "Read PLC Program" do pc12.exe.

Motivacao
---------
O emulate_pc12_frames.py para no primeiro acesso a rotina de transmissao, entao
so recupera o primeiro quadro de cada fluxo. A leitura de programa do TP02 e um
laco: o PC12 envia 34 03 [end_hi] [end_lo] A0 chk varias vezes, avancando o
endereco a cada resposta. O que interessa e justamente a progressao.

Metodo
------
Em vez de parar na rotina de transmissao (0x46F5E6), este script a intercepta:
captura o quadro montado, sintetiza uma resposta valida no buffer de recepcao e
devolve o controle ao chamador. O laco do PC12 entao prossegue e monta o quadro
seguinte, revelando a paginacao real sem PLC e sem porta serial.

Contrato da rotina de transmissao, recuperado por analise estatica:

    0x4FA7A8  buffer de transmissao
    0x4FA8AC  comprimento a transmitir
    0x530230  buffer de recepcao
    0x4FA8B0  quantidade de bytes recebidos
    0x4FA8B7  falha de comunicacao/timeout
    0x4FA8B8  resposta de erro (bit 0x80 do primeiro byte)
    0x4FA8B9  checksum invalido (soma dos bytes recebidos != 0xFF)

Sucesso para o chamador e 0x4FA8B7 = 0x4FA8B8 = 0x4FA8B9 = 0.

Nada roda no sistema hospedeiro: o codigo do PC12 executa dentro do emulador,
sem acesso a disco, rede ou porta serial. Nenhum byte chega a um PLC real.

    python3 scripts/emulate_pc12_readprog.py                  # os dois fluxos
    python3 scripts/emulate_pc12_readprog.py 4B0F94 --paginas 12

Requer: pip install unicorn
"""
import argparse
import struct
import sys

try:
    from unicorn import *
    from unicorn.x86_const import *
    TEM_UNICORN = True
except ImportError:                     # a conferencia estatica dispensa o emulador
    TEM_UNICORN = False

EXE = 'src/OpenLadderStudio.Desktop/pc12.exe'

TX_BUF, TX_LEN = 0x4FA7A8, 0x4FA8AC
RX_BUF, RX_LEN = 0x530230, 0x4FA8B0
F_TIMEOUT, F_ERRO, F_CHECKSUM = 0x4FA8B7, 0x4FA8B8, 0x4FA8B9
TX_ROUTINE = 0x46F5E6

# Guarda no inicio dos dois fluxos: se este byte for zero o PC12 exibe
# "COM Port Unlink !" e retorna sem transmitir. E o indicador de porta
# enlacada; ligado aqui para que o fluxo de leitura seja percorrido.
F_ENLACE = 0x5703E8

# Segunda guarda: codigo do modelo de PLC selecionado no PC12. O ramo do
# TP02-40/60MR(T) e o codigo 1, que fixa o tamanho de programa em 4000 passos
# no global 0x560368 -- o mesmo global que limita o laco de paginacao.
MODELO = 0x5704A8
MODELO_TP02 = 1
TAM_PROGRAMA = 0x560368

API_BASE = 0x70000000
STACK = 0x20000000

# Fluxos que montam 38 00 C7 seguido de 34 03 00 00 A0 28.
FLUXOS = {0x004AEC35: 'fluxo A', 0x004B0F94: 'fluxo B'}

ARGS = {'WriteFile': 5, 'ReadFile': 5, 'SetCommState': 2, 'GetCommState': 2,
        'SetupComm': 3, 'PurgeComm': 2, 'SetCommMask': 2, 'WaitCommEvent': 3,
        'ClearCommError': 3, 'CreateFileA': 7, 'GetCommProperties': 2,
        'CloseHandle': 1, 'Sleep': 1, 'GetTickCount': 0, 'SetCommTimeouts': 2,
        'GetCommTimeouts': 2, 'EscapeCommFunction': 2}


def carrega_pe(caminho):
    data = open(caminho, 'rb').read()
    pe = struct.unpack_from('<I', data, 0x3C)[0]
    nsec = struct.unpack_from('<H', data, pe + 6)[0]
    optsz = struct.unpack_from('<H', data, pe + 20)[0]
    base = struct.unpack_from('<I', data, pe + 24 + 28)[0]
    secs = []
    off = pe + 24 + optsz
    for _ in range(nsec):
        vs, va, rs, ra = struct.unpack_from('<IIII', data, off + 8)
        secs.append((va, vs, ra, rs))
        off += 40
    return data, pe, base, secs


DATA, PE, BASE, SECS = carrega_pe(EXE)


def rva2f(r):
    for va, vs, ra, rs in SECS:
        if rs and va <= r < va + rs:
            return ra + (r - va)
    for va, vs, ra, rs in SECS:
        if va <= r < va + max(vs, rs):
            o = ra + (r - va)
            return o if o < len(DATA) else None
    return None


def monta():
    mu = Uc(UC_ARCH_X86, UC_MODE_32)
    hi = BASE
    for va, vs, ra, rs in SECS:
        hi = max(hi, BASE + va + max(vs, rs))
    mu.mem_map(BASE, (hi - BASE + 0xFFFFF) & ~0xFFFFF)
    for va, vs, ra, rs in SECS:
        if rs:
            mu.mem_write(BASE + va, DATA[ra:ra + rs])
    mu.mem_map(STACK - 0x100000, 0x200000)
    mu.mem_map(API_BASE, 0x100000)
    return mu


def patch_iat(mu):
    imp = struct.unpack_from('<I', DATA, PE + 24 + 104)[0]
    f = rva2f(imp)
    nomes = {}
    idx = 0
    while True:
        oft, _ts, _fc, _nm, ft = struct.unpack_from('<IIIII', DATA, f)
        if oft == 0 and ft == 0:
            break
        t = rva2f(oft or ft)
        iat = ft
        if t is None:
            f += 20
            continue
        while t + 4 <= len(DATA):
            e = struct.unpack_from('<I', DATA, t)[0]
            if e == 0:
                break
            nome = '?'
            if not (e & 0x80000000):
                pf = rva2f(e)
                if pf is not None and pf + 2 < len(DATA):
                    p = pf + 2
                    nome = DATA[p:DATA.find(b'\0', p)].decode('latin-1')
            mu.mem_write(BASE + iat, struct.pack('<I', API_BASE + idx * 16))
            nomes[API_BASE + idx * 16] = nome
            idx += 1
            t += 4
            iat += 4
        f += 20
    return nomes


def checksum(bs):
    """Ultimo byte que faz a soma do quadro fechar em 0xFF."""
    return (0xFF - (sum(bs) & 0xFF)) & 0xFF


def resposta_para(quadro):
    """Resposta plausivel e estruturalmente valida para o quadro transmitido.

    Os vetores de 38 e F0 sao os observados em bancada. O 34 devolve o bloco de
    240 bytes ja confirmado (00 F0 payload chk). O payload vai zerado: aqui
    interessa a progressao dos enderecos pedidos, nao o conteudo devolvido.
    """
    if not quadro:
        return None
    cmd = quadro[0]
    if cmd == 0x34:
        corpo = [0x00, 0xF0] + [0x00] * 240
        return bytes(corpo + [checksum(corpo)])
    if cmd == 0x38:
        return bytes([0x00, 0x02, 0x00, 0x0A, 0xF3])
    if cmd == 0xF0:
        return bytes([0x00, 0x02, 0x10, 0x22, 0xCB])
    if cmd == 0x14:
        return bytes([0x00, 0x00, 0xFF])
    if cmd == 0x0A:
        n = quadro[4] if len(quadro) > 4 else 2
        corpo = [0x00, n] + [0x00] * n
        return bytes(corpo + [checksum(corpo)])
    corpo = [0x00, 0x00]
    return bytes(corpo + [checksum(corpo)])


def executa(inicio, max_quadros=24, max_ins=40_000_000, verbose=False):
    mu = monta()
    nomes = patch_iat(mu)
    mu.reg_write(UC_X86_REG_ESP, STACK)
    mu.reg_write(UC_X86_REG_EBP, STACK)
    mu.mem_write(STACK, struct.pack('<I', 0xDEADBEEF))
    mu.mem_write(F_ENLACE, b'\x01')
    mu.mem_write(MODELO, struct.pack('<I', MODELO_TP02))

    quadros = []
    estado = {'parar': False}

    def mapeia_sob_demanda(uc, access, addr, size, value, ud):
        pag = addr & ~0xFFF
        try:
            uc.mem_map(pag, 0x2000)
        except UcError:
            try:
                uc.mem_map(pag, 0x1000)
            except UcError:
                return False
        return True

    def no_codigo(uc, addr, size, ud):
        if addr == TX_ROUTINE:
            n = struct.unpack('<I', uc.mem_read(TX_LEN, 4))[0] & 0xFFFF
            n = max(0, min(n, 64))
            quadro = bytes(uc.mem_read(TX_BUF, n)) if n else b''
            quadros.append(quadro)

            resp = resposta_para(quadro)
            if resp:
                uc.mem_write(RX_BUF, resp)
                uc.mem_write(RX_LEN, struct.pack('<I', len(resp)))
            uc.mem_write(F_TIMEOUT, b'\x00')
            uc.mem_write(F_ERRO, b'\x00')
            uc.mem_write(F_CHECKSUM, b'\x00')

            esp = uc.reg_read(UC_X86_REG_ESP)
            ret = struct.unpack('<I', uc.mem_read(esp, 4))[0]
            uc.reg_write(UC_X86_REG_ESP, esp + 4)
            uc.reg_write(UC_X86_REG_EIP, ret)

            if len(quadros) >= max_quadros:
                estado['parar'] = True
                uc.emu_stop()
            return

        if API_BASE <= addr < API_BASE + 0x100000:
            esp = uc.reg_read(UC_X86_REG_ESP)
            ret = struct.unpack('<I', uc.mem_read(esp, 4))[0]
            nome = nomes.get(addr & ~0xF, '?')
            uc.reg_write(UC_X86_REG_ESP, esp + 4 + ARGS.get(nome, 0) * 4)
            uc.reg_write(UC_X86_REG_EAX, 1)
            uc.reg_write(UC_X86_REG_EIP, ret)
            return

    mu.hook_add(UC_HOOK_MEM_UNMAPPED, mapeia_sob_demanda)
    mu.hook_add(UC_HOOK_CODE, no_codigo)

    erro = None
    try:
        mu.emu_start(inicio, 0xDEADBEEF, count=max_ins)
    except UcError as e:
        erro = str(e)
    return quadros, (None if estado['parar'] else erro)


# ---------------------------------------------------------------------------
# Geometria da leitura, recuperada por analise estatica
# ---------------------------------------------------------------------------
# Cada item e conferido contra os bytes do proprio pc12.exe: se o binario mudar,
# a verificacao falha em vez de repetir uma conclusao antiga.

SITIOS = [
    (0x004B0E0E, 'C6 05 AC A7 4F 00 A0',
     'quantidade pedida no quadro 34, fixa em 0xA0 = 160 passos'),
    (0x004B0DFC, '88 15 AA A7 4F 00',
     'byte alto do endereco vai para o indice 2 do quadro'),
    (0x004B0E08, '88 0D AB A7 4F 00',
     'byte baixo do endereco vai para o indice 3 do quadro'),
    (0x004B0E3A, 'B2 FF 2A D0 88 15 AD A7 4F 00',
     'checksum = 0xFF - soma dos cinco primeiros bytes'),
    (0x004AFCAF, '03 C0 B9 03 00 00 00 99 F7 F9 83 C0 02',
     'inicio da regiao B = 2 + (LEN * 2) / 3'),
    (0x004AFCC6, '83 C0 02',
     'fim do bloco = LEN + 2'),
    (0x004AFCCF, 'BF 03 00 00 00',
     'cursor da regiao A comeca no indice 3 do quadro recebido'),
    (0x004B0422, '83 C7 02',
     'regiao A avanca 2 bytes por instrucao decodificada'),
    (0x004B042B, 'FF 43 66',
     'regiao B avanca 1 byte por instrucao decodificada'),
    (0x004B0425, 'FF 85 6C FE FF FF',
     'contador de passos do programa avanca junto'),
    (0x004B0E5C, 'A1 68 03 56 00',
     'laco termina quando o contador alcanca o tamanho do programa'),
]

QTD_PASSOS = 0xA0
LEN_BLOCO = 0xF0


def le(va, n):
    f = rva2f(va - BASE)
    return DATA[f:f + n] if f is not None else b''


def confere_sitios():
    print('=' * 78)
    print('Geometria da leitura conferida contra os bytes do pc12.exe')
    print('=' * 78)
    ok = True
    for va, esperado, descr in SITIOS:
        alvo = bytes.fromhex(esperado.replace(' ', ''))
        real = le(va, len(alvo))
        bate = real == alvo
        ok &= bate
        print('  %s 0x%08X  %s' % ('OK  ' if bate else 'FALHA', va, descr))
        if not bate:
            print('        esperado %s' % esperado)
            print('        no arquivo %s' % ' '.join('%02X' % b for b in real))
    print()
    return ok


def geometria(tam_len=LEN_BLOCO):
    """Deriva o formato do bloco a partir das constantes acima."""
    inicio_b = 2 + (tam_len * 2) // 3
    fim = tam_len + 2
    passos = fim - inicio_b
    print('Formato do bloco devolvido pelo comando 34, para LEN = 0x%02X (%d bytes):'
          % (tam_len, tam_len))
    print()
    print('  quadro recebido: [FLAGS] [LEN] [payload de %d bytes] [CHECKSUM]' % tam_len)
    print()
    print('  regiao A  payload[0x000 .. 0x%03X]  %3d bytes  2 por passo'
          % (inicio_b - 3, inicio_b - 2))
    print('  regiao B  payload[0x%03X .. 0x%03X]  %3d bytes  1 por passo'
          % (inicio_b - 2, fim - 3, passos))
    print('  passos no bloco: %d      bytes por passo: %d'
          % (passos, (inicio_b - 2 + passos) // passos))
    print()
    print('  passo i:  A = payload[2i], payload[2i+1]      B = payload[0x%03X + i]'
          % (inicio_b - 2))
    print()
    print('  O decodificador do PC12 le primeiro payload[2i+1] (indice 3 do quadro),')
    print('  que e onde a bancada observou o opcode do contato.')
    print()


def paginacao():
    print('Paginacao do Read PLC Program:')
    print()
    print('  quadro:  34 03 [passo_hi] [passo_lo] %02X chk' % QTD_PASSOS)
    print()
    print('  O endereco NAO e um deslocamento em bytes nem um passo fixo. E o')
    print('  contador de passos do programa, formatado com "%04X" e reconvertido')
    print('  para dois bytes. O contador avanca de 1 a 4 por instrucao decodificada,')
    print('  conforme o tamanho de cada instrucao, entao a paginacao e determinada')
    print('  pelo conteudo do programa, nao por um passo constante.')
    print()
    print('  O laco termina quando o contador alcanca o tamanho de programa do')
    print('  modelo selecionado (global 0x%08X).' % TAM_PROGRAMA)
    print()


# ---------------------------------------------------------------------------
# Conferencia cruzada com as capturas de bancada
# ---------------------------------------------------------------------------
# As cinco capturas da secao 9.1 de docs/TP02_PG_ESTADO_DA_ARTE.md, previstas
# pela formula do encoder do PC12 ja implementada em
# src/OpenLadderStudio.Core/Tp02TargetCompiler.cs.

BASE_INSTR = {'STR': 0x10, 'STR NOT': 0x18, 'OUT': 0x40}
BASE_DISP = {'X': 0x00, 'Y': 0x20, 'C': 0x40}

# ladder, contato, entrada, bobina, payload[0x001], [0x002], [0x003]
BANCADA = [
    ('A', 'STR NOT', 1, 2, 0x18, 0x20, 0x41),
    ('B', 'STR NOT', 2, 2, 0x19, 0x20, 0x41),
    ('C', 'STR NOT', 2, 3, 0x19, 0x20, 0x42),
    ('D', 'STR',     2, 3, 0x11, 0x20, 0x42),
    ('E', 'STR',     1, 3, 0x10, 0x20, 0x42),
]


def palavra(instr, disp, n):
    k = n - 1
    return (BASE_DISP[disp] | ((k >> 3) & 0x1F),
            BASE_INSTR[instr] | (k & 7),
            ((k >> 3) >> 1) & 0xF0)


def confere_bancada():
    print('=' * 78)
    print('Capturas de bancada sobre a geometria recuperada')
    print('=' * 78)
    print('  passo 0 = contato (payload[0x000] alto, [0x001] baixo)')
    print('  passo 1 = bobina  (payload[0x002] alto, [0x003] baixo)')
    print()
    print('  %-3s %-24s %-14s %-14s %s'
          % ('', 'ladder', '[0x001] baixo', '[0x002] alto', '[0x003] baixo'))
    print('  ' + '-' * 74)
    tudo = True
    for tag, instr, ent, sai, p1, p2, p3 in BANCADA:
        _, baixo_c, _ = palavra(instr, 'X', ent)
        alto_o, baixo_o, _ = palavra('OUT', 'Y', sai)
        m = (baixo_c == p1, alto_o == p2, baixo_o == p3)
        tudo &= all(m)
        lad = 'X%04d %s -> Y%04d' % (ent, 'NF' if 'NOT' in instr else 'NA', sai)
        print('  %-3s %-24s %-14s %-14s %s'
              % (tag, lad,
                 '%02X %s' % (baixo_c, 'ok' if m[0] else 'DIVERGE'),
                 '%02X %s' % (alto_o, 'ok' if m[1] else 'DIVERGE'),
                 '%02X %s' % (baixo_o, 'ok' if m[2] else 'DIVERGE')))
    print()
    if tudo:
        print('  As cinco capturas batem. O byte 0x20 em payload[0x002] e o byte')
        print('  alto do passo da bobina: base do dispositivo Y, grupo 0.')
    print()
    return tudo


def hexs(b):
    return ' '.join('%02X' % x for x in b) if b else '(vazio)'


def relata(inicio, rotulo, max_quadros):
    print('=' * 78)
    print('%s  --  0x%08X' % (rotulo, inicio))
    print('=' * 78)
    quadros, erro = executa(inicio, max_quadros=max_quadros)
    if not quadros:
        print('nenhum quadro montado%s' % ('   erro: ' + erro if erro else ''))
        print()
        return []

    print('%-4s %-26s %-8s %s' % ('#', 'quadro transmitido', 'soma', 'leitura'))
    print('-' * 78)
    enderecos = []
    for i, q in enumerate(quadros, 1):
        soma = 'FF' if q and sum(q) % 256 == 0xFF else (
            '%02X' % (sum(q) % 256) if q else '--')
        nota = ''
        if len(q) == 6 and q[0] in (0x34, 0x0A):
            end = (q[2] << 8) | q[3]
            enderecos.append((q[0], end, q[4]))
            nota = 'end=0x%04X  qtd=0x%02X' % (end, q[4])
        elif q[:8] == b'CON-ICB\r':
            nota = 'HELLO'
        print('%-4d %-26s %-8s %s' % (i, hexs(q), soma, nota))
    if erro:
        print('\ninterrompido: %s' % erro)

    if len(enderecos) >= 2:
        print('\nprogressao dos enderecos de leitura:')
        ant = None
        for cmd, end, qtd in enderecos:
            passo = '' if ant is None else '   passo = 0x%04X (%d)' % (
                (end - ant) & 0xFFFF, (end - ant) & 0xFFFF)
            print('   %02X  0x%04X  qtd=0x%02X%s' % (cmd, end, qtd, passo))
            ant = end
    print()
    return quadros


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('funcao', nargs='*', help='endereco virtual em hexadecimal')
    ap.add_argument('--paginas', type=int, default=24,
                    help='maximo de quadros a capturar por fluxo')
    ap.add_argument('--so-geometria', action='store_true',
                    help='apenas a conferencia estatica, sem emular')
    a = ap.parse_args()

    if not confere_sitios():
        print('A geometria nao confere com este pc12.exe. Nao siga adiante.')
        return 1
    paginacao()
    geometria()
    if not confere_bancada():
        print('As capturas de bancada divergem do modelo. Nao siga adiante.')
        return 1
    if a.so_geometria:
        return 0
    if not TEM_UNICORN:
        print('Emulacao indisponivel: pip install unicorn')
        return 0

    alvos = ([(int(x, 16), 'funcao 0x%s' % x.upper()) for x in a.funcao]
             if a.funcao else sorted(FLUXOS.items()))
    for va, rotulo in alvos:
        relata(va, rotulo, a.paginas)
    return 0


if __name__ == '__main__':
    sys.exit(main())
