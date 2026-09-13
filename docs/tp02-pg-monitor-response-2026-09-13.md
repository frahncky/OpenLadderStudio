# TP02 PG — parser de respostas do monitor Ladder

Data: 2026-09-13.

Esta etapa fecha a interpretação estática do parser usado pelo PC12 v2.1 depois das leituras PG0A do monitor Ladder. A análise foi feita sobre o `pc12.exe` original enviado, SHA-256 `05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0`, sem abrir serial e sem executar TX.

## Dispatcher de resposta

Em `004C4EF2` o PC12 lê um tipo interno entre 0 e 7 e salta pela tabela em `004C4F09`:

```text
tipo 0 -> 004C4F29
tipo 1 -> 004C4F47
tipo 2 -> 004C5142
tipo 3 -> 004C56B6
tipo 4 -> 004C5303
tipo 5 -> 004C5488
tipo 6 -> 004C54DF
tipo 7 -> 004C5536
```

O número de bytes consumidos por descritor é:

```text
tipo 0: 0 bytes
tipo 1: 1 byte
tipo 2: 1 byte
tipo 3: 0 bytes
tipo 4: 4 bytes
tipo 5: 2 bytes
tipo 6: 2 bytes
tipo 7: 4 bytes
```

Isso resolve a pendência da v1.44 sobre a distribuição das respostas Q=4: o PC12 já registra, junto de cada descritor, qual dos oito parsers deve consumir os bytes retornados.

## Tipos booleanos

Os tipos 1 e 2 selecionam um bit de 0 a 7 do primeiro byte consumido:

- tipo 1: ativo-alto; bit 1 produz `On`;
- tipo 2: ativo-baixo; bit 0 produz `On`.

Os tipos 5 e 6 usam o segundo byte de uma resposta de dois bytes:

- tipo 5: `On` quando `payload[1] == seletor`;
- tipo 6: `On` quando `payload[1] != seletor`.

Os literais `Off` e `On` estão embutidos no próprio PC12.

## Tipos numéricos Q=4

O tipo 4 consome sempre quatro bytes. O campo interno de largura escolhe quantos deles participam do valor exibido:

```text
largura 1: value = b0
largura 2: value = b0 | (b1 << 8)
largura 3: value = b0 | (b1 << 8) | (b2 << 16) | (b3 << 24)
```

Portanto, o tipo 4 é little-endian para 16 e 32 bits.

O tipo 7 também consome quatro bytes, mas usa outra ordem:

```text
largura 1: value = b1
largura 2: value = (b0 << 8) | b1
largura 3: value = ((b0 << 8) | b1) | (((b2 << 8) | b3) << 16)
```

Na largura 3, cada palavra de 16 bits chega em ordem big-endian, mas a primeira palavra forma os 16 bits menos significativos do inteiro de 32 bits. É uma ordem por palavras, não um big-endian de 32 bits convencional.

Os dois parsers compartilham os formatos de exibição nativos `%03d`, `%05d` e `%010u`; o PC12 também contém variantes hexadecimais `%02X`, `%04X` e `%08X` controladas por flags de interface.

## Implementação no OpenLadderStudio

A v1.45 adiciona `Tp02PgProtocol.DecodeMonitor(...)`, um decoder offline que reproduz os oito tipos do parser nativo. Ele retorna:

- quantidade de bytes consumidos;
- estado booleano quando aplicável;
- valor numérico quando aplicável.

Não há acesso serial nesse método e nenhum novo comando é liberado para TX.

Os autotestes cobrem:

- tipos 0 e 3 sem consumo;
- tipos 1 e 2 ativo-alto/ativo-baixo;
- tipos 4 e 7 nas três larguras;
- tipos 5 e 6 em igualdade/desigualdade;
- respostas curtas e parâmetros inválidos.

## Reprodutibilidade

```bash
python3 scripts/analyze_pc12_monitor_response.py /caminho/pc12.exe
```

Saída de referência:

```text
docs/data/pc12-monitor-response-static.txt
```

O script usa somente a biblioteca padrão e exige `RESULT=PASS`.

## O que ainda permanece aberto

Com a distribuição Q=4 fechada, os principais pontos ainda dependentes de trabalho são:

- associação nominal de todos os IDs internos `70001..70027` às instruções Ladder;
- validação física das respostas de monitor no TP02 real;
- paginação física PG34 acima de 80 passos;
- validação física dos comandos 09/0A/35, RTC e EEPROM;
- diferenças possíveis entre revisões de firmware do TP02.

Nenhuma conclusão física foi inferida a partir da análise offline.
