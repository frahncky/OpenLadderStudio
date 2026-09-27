#!/usr/bin/env python3
"""Exporta os quadros do PC12 emulado como vetores de referência do Core.

Executa os emuladores offline (scripts/emulate_pc12_*.py) sobre o pc12.exe,
extrai cada quadro que o código x86 original montou e grava um arquivo TSV
versionado em tests/OpenLadderStudio.Core.Tests/Data/pc12-golden-vectors.tsv.

O autoteste Tp02Pc12GoldenVectorsSelfTest lê esse arquivo e confere, byte a
byte, os construtores de OpenLadderStudio.Core. Assim o CI do Windows compara
o OpenLadder com o PC12 sem precisar do Unicorn.

Uso:
    python3 scripts/export_pc12_golden_vectors.py           # regenera o arquivo
    python3 scripts/export_pc12_golden_vectors.py --check   # falha se estiver desatualizado

Requer unicorn==2.1.4, pefile==2024.8.26 e capstone. Nenhuma porta serial é
aberta e nenhum byte é transmitido: os emuladores param antes da rotina TX.
"""
import hashlib
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXE = os.path.join(ROOT, 'src', 'OpenLadderStudio.Desktop', 'pc12.exe')
OUT = os.path.join(ROOT, 'tests', 'OpenLadderStudio.Core.Tests', 'Data', 'pc12-golden-vectors.tsv')

EMULATORS = [
    ('emulate_pc12_monitor_commands', []),
    ('emulate_pc12_memory_variants', []),
    ('emulate_pc12_clock_commands', []),
    ('emulate_pc12_frames', ['--sweep']),
    ('emulate_pc12_eeprom_commands', []),
    ('emulate_pc12_readprog', []),
    ('emulate_pc12_pg33_builder', []),
    ('emulate_pc12_pg33_operand_helper', []),
    ('emulate_pc12_pg33_f23_full', []),
    ('emulate_pc12_pg33_f24_full', []),
    ('emulate_pc12_pg33_f13w_batch20', []),
    ('emulate_pc12_pg33_f13w_blocks21', []),
    ('emulate_pc12_response_validator', []),
]

# Leitura múltipla do monitor Ladder: rótulo do emulador -> leituras do Core.
LADDER_READS = {
    'X0001': 'X:1:1',
    'X0008,X0009,Y0384': 'X:8:1 X:9:1 Y:384:1',
    'C0001,C2048,SC001,SC128': 'C:1:1 C:2048:1 SC:1:1 SC:128:1',
    'S0101,S0816,X0384': None,  # área S não existe em Tp02PgMemoryProtocol
}


def run_emulators():
    outputs = {}
    for name, args in EMULATORS:
        script = os.path.join(ROOT, 'scripts', name + '.py')
        proc = subprocess.run([sys.executable, script] + args, cwd=ROOT,
                              capture_output=True, text=True, encoding='utf-8')
        if proc.returncode != 0:
            sys.stderr.write(proc.stdout + proc.stderr)
            raise SystemExit('ERRO: %s terminou com código %d' % (name, proc.returncode))
        if 'FAIL' in proc.stdout:
            raise SystemExit('ERRO: %s relatou FAIL contra o próprio modelo' % name)
        outputs[name] = proc.stdout
    outputs.update(capture_batches())
    return outputs


def capture_batches():
    """Lotes PG09 e FL com os quadros completos, não só os tamanhos.

    A saída de texto do emulador imprime apenas o tamanho de cada pacote;
    aqui as mesmas rotinas são chamadas diretamente para guardar os bytes.
    Os registros sintéticos seguem as fórmulas de emulate_pc12_memory_variants,
    reproduzidas no autoteste C#.
    """
    sys.path.insert(0, os.path.join(ROOT, 'scripts'))
    cwd = os.getcwd()
    os.chdir(ROOT)
    try:
        import emulate_pc12_memory_variants as mv
        emulator = mv.MonitorEmulator(mv.EXE)
        mv.verify_imports(emulator)
        join = lambda frames: ' | '.join(f.hex(' ').upper() for f in frames)
        batches = []
        for area in mv.BANKS:
            for count in (1, 2, 39, 40, 41, 80, 81):
                records = [((i * 257) % mv.LIMITS[area] + 1, (i * 32767 + 0x1234) & 65535)
                           for i in range(count)]
                batches.append((area, count, join(mv.scatter(emulator, area, records))))
        files = []
        for hex_mode in (False, True):
            for count in (1, 9, 10, 11):
                files.append(('HEX' if hex_mode else 'ASCII', count,
                              join(mv.file_write(emulator, count, hex_mode))))
        return {'batches': batches, 'files': files}
    finally:
        os.chdir(cwd)


def hexnorm(text):
    return ' '.join(text.split())


def need(pattern, text, what, flags=re.M):
    m = re.search(pattern, text, flags)
    if not m:
        raise SystemExit('ERRO: vetor não encontrado na saída do emulador: ' + what)
    return m


def extract(o):
    rows = []

    def add(group, command, expected, description):
        rows.append((group, command, hexnorm(expected), description))

    mon = o['emulate_pc12_monitor_commands']
    for m in re.finditer(r'^OK PG35 ([XYC])(\d+)=(\d) -> ([0-9A-F ]+)$', mon, re.M):
        add('PG35 SET/RESET', 'SR %s %d %s' % (m[1], int(m[2]), m[3]), m[4], '%s%s=%s' % (m[1], m[2], m[3]))
    for m in re.finditer(r'^OK PG09 (WC|V|D)(\d+)=(\d+) -> ([0-9A-F ]+)$', mon, re.M):
        add('PG09 escrita de registrador', 'REG %s %d %s' % (m[1], int(m[2]), m[3]), m[4], '%s%s=%s' % (m[1], m[2], m[3]))

    mem = o['emulate_pc12_memory_variants']
    for area, count, frames in o['batches']:
        add('PG09 lotes', 'BATCH %s %d' % (area, count), frames, '%d registros %s' % (count, area))
    for kind, count, frames in o['files']:
        add('FL escrita', 'FLBATCH %s %d' % (kind, count), frames, 'FL %s %d arquivos' % (kind, count))
    m = need(r'^D nao contiguo: ([0-9A-F ]+)$', mem, 'D não contíguo')
    add('PG09 lotes', 'REGS D 1,257,2048 4660,32768,65535', m[1], 'D não contíguo')
    for m in re.finditer(r'^(V|D|WC|FL) (\d+) paginas: primeira=([0-9A-F ]+) ultima=([0-9A-F ]+) PASS', mem, re.M):
        add('Varredura por páginas', 'PAGES %s' % m[1], 'n=%s %s / %s' % (m[2], hexnorm(m[3]), hexnorm(m[4])), m[1])
    ws1 = need(r'^WS pagina 1: ([0-9A-F ]+?)\s+PASS', mem, 'WS página 1')[1]
    ws2 = need(r'^WS pagina 2: ([0-9A-F ]+?)\s+PASS', mem, 'WS página 2')[1]
    add('Varredura por páginas', 'PAGES WS', 'n=2 %s / %s' % (hexnorm(ws1), hexnorm(ws2)), 'WS')
    for m in re.finditer(r'^V(\d+): ([0-9A-F ]+) PASS', mem, re.M):
        add('Leitura de registrador', 'READ V %s 2' % m[1], m[2], 'V' + m[1])
    for m in re.finditer(r'^Ladder (grade|lista) ([\w,]+): ([0-9A-F ]+) PASS', mem, re.M):
        reads = LADDER_READS[m[2]]
        add('Monitor Ladder', 'MULTI ' + reads if reads else 'SEM_API area S', m[3], '%s %s' % (m[1], m[2]))
    for label, command in [('Leitura WS039', 'READ WS 39 2'), ('Leitura WS040', 'READ WS 40 2'),
                           ('Leitura no handler de erro A', 'READ WS 6 2'),
                           ('Leitura no handler de erro B', 'READ WS 5 2'),
                           ('Sair de Remote I/O', 'REG WS 43 0'),
                           ('RTC hora/minuto/segundo', 'READ V 1018 6')]:
        add('Leitura/escrita WS', command, need('^' + re.escape(label) + r': ([0-9A-F ]+) PASS', mem, label)[1], label)
    for m in re.finditer(r'^Escrita sintetica WS(\d+): (09 05 60 \w\w 02 (\w\w) (\w\w) \w\w) PASS', mem, re.M):
        add('Leitura/escrita WS', 'REG WS %d %d' % (int(m[1]), int(m[3] + m[4], 16)), m[2], 'escrita sintética WS' + m[1])

    clk = o['emulate_pc12_clock_commands']
    add('RTC e tempo de varredura', 'CLOCKREAD', need(r'READ rtc: ([0-9A-F ]+)$', clk, 'RTC read')[1], 'leitura RTC')
    add('RTC e tempo de varredura', 'SCANREAD', need(r'READ scan: ([0-9A-F ]+)$', clk, 'scan read')[1], 'leitura scan')
    for m in re.finditer(r'RTC read sec/min/hour/day/weekday/month/year=\(([\d, ]+)\).*\nOK RTC write: ([0-9A-F ]+)', clk):
        add('RTC e tempo de varredura', 'CLOCKW ' + m[1].replace(' ', ''), m[2], 'escrita RTC ' + m[1].replace(' ', ''))
    for m in re.finditer(r'SCAN payload=([0-9A-F ]+) -> atual=(\d+) minimo=(\d+) maximo=(\d+)', clk):
        data = [0, 6] + [int(x, 16) for x in m[1].split()]
        data.append((0xFF - sum(data)) & 0xFF)
        add('RTC e tempo de varredura', 'SCANDEC ' + ' '.join('%02X' % x for x in data),
            'valores=%s,%s,%s' % (m[2], m[3], m[4]), 'decodificar scan ' + hexnorm(m[1]))

    for m in re.finditer(r'^0x[0-9A-F]+\s+((?:[0-9A-F]{2} ){2}[0-9A-F]{2})\s+3\s+FF', o['emulate_pc12_frames'], re.M):
        add('Quadros curtos', 'SHORT ' + m[1][:2], m[1], 'quadro curto ' + m[1][:2])
    for m in re.finditer(r'frame=([0-9A-F ]+)$|-> ([0-9A-F]{2} 00 [0-9A-F]{2})$', o['emulate_pc12_eeprom_commands'], re.M):
        frame = m[1] or m[2]
        add('Quadros curtos', 'SHORT ' + frame[:2], frame, 'quadro curto ' + frame[:2])
    rp = o['emulate_pc12_readprog']
    add('Quadros curtos', 'SHORT 38', need(r'^2\s+(38 00 C7)', rp, '38 00 C7')[1], 'preâmbulo 38')
    add('Leitura de programa', 'P34 0', need(r'^3\s+(34 03 00 00 A0 28)', rp, '34 inicial')[1], 'primeira página 34')

    for m in re.finditer(r'OK: (\S+)\s+start=0x(\w+).*\n\s+PC12 : ([0-9A-F ]+)', o['emulate_pc12_pg33_builder']):
        f = m[3].split()
        words = int(f[5], 16) // 2
        add('PG33 escrita de programa', 'PG33RAW %s %s %s' % (m[2], ','.join(f[6:6 + 2 * words]),
            ','.join(f[6 + 2 * words:6 + 3 * words])), m[3], 'builder ' + m[1])
    for m in re.finditer(r'OK: (\S+)\s+HIGH/LOW=\[(\w\w \w\w)\] EXT=\[(\w\w)\]', o['emulate_pc12_pg33_operand_helper']):
        token = m[1]
        command = 'OPD D %d' % int(token[1:]) if token[0] == 'D' else 'LIT %d' % int(token)
        add('PG33 codificação', command, '%s|%s' % (m[2], m[3]), 'operando ' + token)
    add('PG33 codificação', 'PFX F-13w', '0D 77|00', 'prefixo F-13w ADD')
    for name, key in (('emulate_pc12_pg33_f23_full', 'F-23'), ('emulate_pc12_pg33_f24_full', 'F-24')):
        m = need(r'OK HIGH/LOW=\[(\w\w \w\w) (\w\w \w\w)\]', o[name], key)
        add('PG33 codificação', 'PFX ' + key, m[1] + '|00', key + ' prefixo')
        add('PG33 codificação', 'SPB Y 1', m[2] + '|00', key + ' operando Y0001')
    block = r'bytes=(\d+) (?:CMD=33 )?LEN=(\w+) start=(\w+) HL(?:_count)?=(\w+) checksum=(\w+)'
    fmt = lambda m: 'bytes=%s LEN=%s start=%s HL=%s chk=%s' % (m[1], m[2], m[3], m[4], m[5])
    add('PG33 escrita de programa', 'PG33ADD 20', fmt(need(block, o['emulate_pc12_pg33_f13w_batch20'], 'batch20')),
        '20 x F-13w ADD D0002,D0001,00010')
    add('PG33 escrita de programa', 'PG33ADD 21',
        ' ; '.join(fmt(m) for m in re.finditer(block, o['emulate_pc12_pg33_f13w_blocks21'])),
        '21 x F-13w ADD (20+1)')

    rv = o['emulate_pc12_response_validator']
    for m in re.finditer(r'OK: (\S+) bypass=(\d) RX=\[([0-9A-F ]+)\]\n\s+got\s+timeout=(\w+) checksum=(\w+) error=(\w+) bit20=(\w+)', rv):
        accepted = m[5] == '00' and m[6] == '00'
        # O OpenLadder não possui o desvio de checksum do global 0x5301AE.
        command = ('SEM_API desvio de checksum ' if m[2] == '1' else 'PAYLOAD ') + m[3]
        add('Validação de resposta', command, 'ACEITA' if accepted else 'REJEITA',
            '%s (bypass=%s)' % (m[1], m[2]))

    unique = {}
    ordered = []
    for row in rows:
        previous = unique.get(row[1])
        if previous is None:
            unique[row[1]] = row
            ordered.append(row)
        elif previous[2] != row[2]:
            raise SystemExit('ERRO: comando %s com dois resultados PC12: %s / %s' % (row[1], previous[2], row[2]))
    return ordered


def render(rows):
    with open(EXE, 'rb') as f:
        sha = hashlib.sha256(f.read()).hexdigest()
    lines = [
        '# Vetores de referência do PC12 v2.1 emulado (Unicorn, offline, nenhum TX).',
        '# Gerado por scripts/export_pc12_golden_vectors.py. Não editar à mão.',
        '# pc12.exe SHA256 ' + sha,
        '# grupo\tcomando\tesperado_pc12\tdescricao',
    ]
    for row in rows:
        lines.append('\t'.join(row))
    return '\n'.join(lines) + '\n'


def main():
    text = render(extract(run_emulators()))
    if '--check' in sys.argv:
        current = open(OUT, encoding='utf-8').read() if os.path.exists(OUT) else ''
        if current != text:
            sys.stderr.write('ERRO: %s está desatualizado. Rode scripts/export_pc12_golden_vectors.py.\n' % OUT)
            return 1
        print('Vetores PC12 atualizados: %d linhas.' % (text.count('\n') - 4))
        return 0
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    print('Gravado %s (%d vetores).' % (os.path.relpath(OUT, ROOT), text.count('\n') - 4))
    return 0


if __name__ == '__main__':
    sys.exit(main())
