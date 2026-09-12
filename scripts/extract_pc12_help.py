#!/usr/bin/env python3
"""
Extrai o conteudo dos arquivos de ajuda WinHelp que acompanham o PC12 original.

Alvos
-----
    src/OpenLadderStudio.Desktop/Tp022.hlp      (identico a PC12HELP.HLP)
    src/OpenLadderStudio.Desktop/PC12HELP.HLP
    src/OpenLadderStudio.Desktop/HELPDLG.HLP

Esses arquivos sao a unica documentacao oficial do TP02/PC12 presente no
repositorio. Sao WinHelp 3.x (HC31), gerados em 2001, e o |SYSTEM declara
flags=0, isto e, sem compressao LZ77 nem compressao por frases.

Metodo
------
1. O cabecalho de 16 bytes aponta o diretorio interno (B-tree) em DirectoryStart.
2. Cada arquivo interno e precedido por FILEHEADER de 9 bytes
   (ReservedSpace, UsedSpace, FileFlags).
3. |TOPIC e dividido em blocos de 4096 bytes. Cada bloco comeca com
   TOPICBLOCKHEADER de 12 bytes (LastTopicLink, FirstTopicLink, LastTopicHeader).
   As posicoes usadas nesses campos sao TOPICPOS = (bloco << 14) | offset,
   com o offset contado desde o inicio do bloco, cabecalho incluido.
4. Os registros TOPICLINK tem 21 bytes de cabecalho
   (BlockSize, DataLen2, PrevBlock, NextBlock, DataLen1, RecordType) e podem
   atravessar a fronteira de bloco; a leitura precisa pular o cabecalho do
   bloco seguinte. RecordType 0x02 e cabecalho de topico e 0x20 e texto.
5. O texto util fica em LinkData2 como cadeias terminadas em NUL.
6. Os arquivos |bmNN sao hipergraficos "lp" com DIB de 24 bpp comprimido por
   RunLen. --bitmaps grava cada um como .bmp sem dependencia externa.

Percorrer o |TOPIC linearmente, ignorando a codificacao por bloco, recupera
apenas os primeiros topicos e da a impressao falsa de que o arquivo e pequeno.
A contagem de topicos confere com o numero de entradas de |TTLBTREE.

Este script apenas le os arquivos de ajuda. Nao executa nada e nao os modifica.
"""

import argparse
import pathlib
import struct

HLP_MAGIC = 0x00035F3F
TOPIC_BLOCK = 4096
TOPIC_BLOCK_HEADER = 12
TOPIC_LINK_HEADER = 21
RECORD_TOPIC_HEADER = 0x02
RECORD_TEXT = 0x20
RECORD_TABLE = 0x23

SYSTEM_RECORDS = {
    1: 'Title', 2: 'Copyright', 3: 'Contents', 4: 'Macro', 5: 'Icon',
    6: 'Window', 8: 'Citation', 9: 'LCID', 10: 'CNT', 11: 'CharSet',
    12: 'DefFont', 13: 'GroupsAdd', 14: 'IndexSep', 18: 'Language', 19: 'DLLMaps',
}


class WinHelp(object):
    """Leitor minimo de WinHelp 3.x sem compressao."""

    def __init__(self, data):
        magic, self.dir_start, _free, size = struct.unpack_from('<Iiii', data, 0)
        if magic != HLP_MAGIC:
            raise ValueError('nao e um arquivo WinHelp 3.x')
        self.data = data
        self.declared_size = size
        self.files = {}
        self._read_directory()
        self.minor, self.major, self.gendate, self.flags = self._read_system_header()

    # ---------- estrutura interna ----------

    def _file_header(self, off):
        reserved, used, flags = struct.unpack_from('<iiB', self.data, off)
        return reserved, used, flags, off + 9

    def _zstring(self, off):
        end = self.data.index(b'\0', off)
        return self.data[off:end].decode('latin-1'), end + 1

    def _read_directory(self):
        _reserved, _used, _flags, body = self._file_header(self.dir_start)
        pagesize = struct.unpack_from('<H', self.data, body + 4)[0]
        rootpage, _mustbe_neg1, _pages, levels = struct.unpack_from('<4h', self.data, body + 26)
        base = body + 38

        def walk(page, level):
            off = base + page * pagesize
            if level < levels:
                _unused, nentries, previous = struct.unpack_from('<3h', self.data, off)
                cursor = off + 6
                children = [previous]
                for _ in range(nentries):
                    _name, cursor = self._zstring(cursor)
                    children.append(struct.unpack_from('<h', self.data, cursor)[0])
                    cursor += 2
                for child in children:
                    walk(child, level + 1)
            else:
                _unused, nentries, _prev, _next = struct.unpack_from('<4h', self.data, off)
                cursor = off + 8
                for _ in range(nentries):
                    name, cursor = self._zstring(cursor)
                    self.files[name] = struct.unpack_from('<i', self.data, cursor)[0]
                    cursor += 4

        walk(rootpage, 1)

    def internal(self, name):
        _reserved, used, _flags, body = self._file_header(self.files[name])
        return self.data[body:body + used]

    def _read_system_header(self):
        system = self.internal('|SYSTEM')
        _magic, minor, major = struct.unpack_from('<3h', system, 0)
        gendate, flags = struct.unpack_from('<iH', system, 6)
        return minor, major, gendate, flags

    def system_records(self):
        """Registros do |SYSTEM no formato HC31: RecordType, DataSize, Data."""
        system = self.internal('|SYSTEM')
        out = []
        cursor = 12
        while cursor + 4 <= len(system):
            rtype, size = struct.unpack_from('<2H', system, cursor)
            out.append((SYSTEM_RECORDS.get(rtype, 'tipo%d' % rtype), system[cursor + 4:cursor + 4 + size]))
            cursor += 4 + size
        return out

    # ---------- |TOPIC ----------

    def _topic_read(self, topic, pos, count):
        """Le count bytes a partir do TOPICPOS pos, atravessando blocos."""
        out = bytearray()
        block, off = pos >> 14, pos & 0x3FFF
        while count > 0:
            base = block * TOPIC_BLOCK
            if base >= len(topic):
                break
            available = min(TOPIC_BLOCK, len(topic) - base) - off
            if available <= 0:
                block += 1
                off = TOPIC_BLOCK_HEADER
                continue
            take = min(available, count)
            out += topic[base + off:base + off + take]
            count -= take
            off += take
            if count > 0:
                block += 1
                off = TOPIC_BLOCK_HEADER
        return bytes(out)

    def topic_links(self):
        """Itera (RecordType, LinkData1, LinkData2, TOPICPOS) seguindo NextBlock."""
        if self.flags != 0:
            raise ValueError('|TOPIC comprimido (flags=%d); este leitor cobre apenas flags=0' % self.flags)
        topic = self.internal('|TOPIC')
        pos = TOPIC_BLOCK_HEADER
        seen = set()
        while pos >= 0 and pos not in seen:
            seen.add(pos)
            header = self._topic_read(topic, pos, TOPIC_LINK_HEADER)
            if len(header) < TOPIC_LINK_HEADER:
                break
            block_size, _len2, _prev, next_pos, len1 = struct.unpack_from('<5i', header, 0)
            record_type = header[20]
            if block_size < TOPIC_LINK_HEADER or len1 < TOPIC_LINK_HEADER:
                break
            body = self._topic_read(topic, pos, block_size)
            yield record_type, body[TOPIC_LINK_HEADER:len1], body[len1:block_size], pos
            pos = next_pos

    def titles(self):
        """Titulos de topico do |TTLBTREE (estrutura 'Lz': TOPICPOS + STRINGZ)."""
        if '|TTLBTREE' not in self.files:
            return {}
        raw = self.internal('|TTLBTREE')
        pagesize = struct.unpack_from('<H', raw, 4)[0]
        rootpage = struct.unpack_from('<h', raw, 26)[0]
        off = 38 + rootpage * pagesize
        _unused, nentries, _prev, _next = struct.unpack_from('<4h', raw, off)
        cursor = off + 8
        out = {}
        for _ in range(nentries):
            pos = struct.unpack_from('<i', raw, cursor)[0]
            cursor += 4
            end = raw.index(b'\0', cursor)
            out[pos] = raw[cursor:end].decode('latin-1')
            cursor = end + 1
        return out

    def topics(self):
        """Agrupa os TOPICLINK em topicos: [(TOPICPOS, titulo, [paragrafos])]."""
        titles = self.titles()
        out = []
        for record_type, _link1, link2, pos in self.topic_links():
            if record_type == RECORD_TOPIC_HEADER:
                out.append((pos, titles.get(pos, ''), []))
            elif record_type in (RECORD_TEXT, RECORD_TABLE) and out:
                text = link2.decode('latin-1').replace('\0', '')
                if text.strip():
                    out[-1][2].append(text)
        return out

    # ---------- |bmNN ----------

    @staticmethod
    def _cword(raw, off):
        if raw[off] & 1:
            return struct.unpack_from('<H', raw, off)[0] >> 1, off + 2
        return raw[off] >> 1, off + 1

    @staticmethod
    def _cdword(raw, off):
        if raw[off] & 1:
            return struct.unpack_from('<I', raw, off)[0] >> 1, off + 4
        return struct.unpack_from('<H', raw, off)[0] >> 1, off + 2

    @staticmethod
    def _runlen(src):
        out = bytearray()
        cursor = 0
        while cursor < len(src):
            control = src[cursor]
            cursor += 1
            if control & 0x80:
                count = control & 0x7F
                out += src[cursor:cursor + count]
                cursor += count
            elif cursor < len(src):
                out += bytes([src[cursor]]) * control
                cursor += 1
        return bytes(out)

    def pictures(self, name):
        """Objetos de um |bmNN. Cobre DDB/DIB, sem compressao ou RunLen."""
        raw = self.internal(name)
        if raw[:2] != b'lp':
            return []
        count = struct.unpack_from('<H', raw, 2)[0]
        offsets = struct.unpack_from('<%dI' % count, raw, 4)
        out = []
        for base in offsets:
            cursor = base
            ptype, pack = raw[cursor], raw[cursor + 1]
            cursor += 2
            if ptype not in (5, 6):
                out.append({'name': name, 'type': ptype, 'pack': pack})
                continue
            pic = {'name': name, 'type': ptype, 'pack': pack}
            for field in ('xdpi', 'ydpi'):
                pic[field], cursor = self._cdword(raw, cursor)
            for field in ('planes', 'bpp'):
                pic[field], cursor = self._cword(raw, cursor)
            for field in ('width', 'height', 'colors', 'important', 'datasize', 'hotspotsize'):
                pic[field], cursor = self._cdword(raw, cursor)
            data_offset = struct.unpack_from('<I', raw, cursor)[0]
            blob = raw[base + data_offset:base + data_offset + pic['datasize']]
            pic['data'] = self._runlen(blob) if pack == 1 else blob
            out.append(pic)
        return out

    @staticmethod
    def to_bmp(pic):
        """Monta um .bmp a partir do DIB decodificado."""
        width, height, bpp = pic['width'], pic['height'], pic['bpp']
        ncolors = pic['colors'] if pic['colors'] else (1 << bpp if bpp <= 8 else 0)
        palette = pic['data'][:ncolors * 4]
        stride = ((width * bpp + 31) // 32) * 4
        needed = stride * height
        pixels = pic['data'][ncolors * 4:][:needed].ljust(needed, b'\0')
        info = struct.pack('<IiiHHIIiiII', 40, width, height, 1, bpp, 0, len(pixels), 0, 0, ncolors, 0)
        start = 14 + 40 + len(palette)
        header = struct.pack('<2sIHHI', b'BM', start + len(pixels), 0, 0, start)
        return header + info + palette + pixels


def report(path, dump_bitmaps=None):
    hlp = WinHelp(pathlib.Path(path).read_bytes())
    lines = []
    lines.append('ARQUIVO DE AJUDA: %s' % path)
    lines.append('=' * 100)
    bitmaps = sorted(k for k in hlp.files if k.startswith('|bm'))
    others = sorted(k for k in hlp.files if not k.startswith('|bm'))
    lines.append('WinHelp HC3%d  minor=%d  flags=%d (0 = sem compressao)'
                 % (1 if hlp.minor >= 16 else 0, hlp.minor, hlp.flags))
    lines.append('arquivos internos: %s' % ' '.join(others))
    lines.append('bitmaps: %d (|bm0..|bm%d)' % (len(bitmaps), len(bitmaps) - 1) if bitmaps else 'bitmaps: 0')
    for label, value in hlp.system_records():
        text = value.rstrip(b'\0').decode('latin-1')
        printable = all(32 <= ord(c) < 127 for c in text) and text
        lines.append('  |SYSTEM %-10s %s' % (label, repr(text) if printable else '%d bytes' % len(value)))

    topics = hlp.topics()
    lines.append('')
    lines.append('topicos: %d   titulos em |TTLBTREE: %d' % (len(topics), len(hlp.titles())))
    lines.append('')
    lines.append('=' * 100)
    lines.append('TEXTO DOS TOPICOS')
    lines.append('=' * 100)
    for index, (pos, title, paragraphs) in enumerate(topics):
        lines.append('')
        lines.append('---------- topico %d  TOPICPOS=%d  %s' % (index, pos, title))
        for paragraph in paragraphs:
            lines.append(paragraph.rstrip())

    if dump_bitmaps:
        target = pathlib.Path(dump_bitmaps)
        target.mkdir(parents=True, exist_ok=True)
        lines.append('')
        lines.append('=' * 100)
        lines.append('BITMAPS')
        lines.append('=' * 100)
        for name in bitmaps:
            for order, pic in enumerate(hlp.pictures(name)):
                if 'data' not in pic:
                    lines.append('%-8s tipo %d nao suportado' % (name, pic['type']))
                    continue
                out = target / ('%s_%dx%d.bmp' % (name.lstrip('|'), pic['width'], pic['height']))
                out.write_bytes(WinHelp.to_bmp(pic))
                lines.append('%-8s %dx%d %d bpp pack=%d -> %s'
                             % (name if order == 0 else '', pic['width'], pic['height'],
                                pic['bpp'], pic['pack'], out.name))
    return '\n'.join(lines) + '\n'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('hlp', nargs='*',
                        default=['src/OpenLadderStudio.Desktop/Tp022.hlp',
                                 'src/OpenLadderStudio.Desktop/HELPDLG.HLP'])
    parser.add_argument('-o', '--output')
    parser.add_argument('--bitmaps', help='pasta onde gravar os |bmNN como .bmp')
    args = parser.parse_args()

    text = '\n\n'.join(report(path, args.bitmaps) for path in args.hlp)
    if args.output:
        pathlib.Path(args.output).write_text(text, encoding='utf-8')
        print('relatorio gravado em %s' % args.output)
    else:
        print(text)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
