# TP02/PC12 — semântica dos tokens Ladder que alcançam Q=4

Data: 2026-09-13

## Escopo

Este relatório aprofunda os cinco identificadores internos do formato Ladder do PC12 que, pelo dispatcher reconstruído na v1.44, podem alcançar descritores PG0A com `Q=04`: `70006`, `70007`, `70009`, `70017` e `70027`.

Eles **não são opcodes PG**. São tokens internos do modelo/arquivo Ladder consumidos pelo monitor do PC12.

## Resultado

| ID | Constante interna | Classe do dispatcher | Classificação comprovada |
|---|---:|---:|---|
| 70006 | `0x11176` | 1 | token de circuito **CNT** |
| 70007 | `0x11177` | 1 | token estrutural de continuação/preenchimento |
| 70009 | `0x11179` | 2 | membro da família de circuito de saída/função |
| 70017 | `0x11181` | 2 | membro de topologia especial/multioperando |
| 70027 | `0x1118B` | 1 | token estrutural Boolean→Ladder pareado com 70008 |

Somente `70006` recebe aqui um mnemônico exato. Para os demais, o executável prova o papel estrutural/família, mas não fornece evidência inequívoca suficiente para atribuir um nome de instrução sem inferência.

## Evidência direta

### 70006 = CNT

Em `0x00454FE1` o validador compara o token com `0x11176` (`70006`). O caminho de erro associado usa literalmente:

`X:%04d/Y:%04d CNT Circuit is incorrect !`

Isso identifica `70006` como token de circuito CNT.

### 70007 = estrutura/continuação

O bloco iniciado em `0x0041AA43` carrega `0x11177`; em `0x0041AAB8` e `0x0041AACF` o PC12 continua varrendo registros anteriores enquanto o token permanecer `70007`, decrementando a posição/linha. Esse comportamento é estrutural e não o de uma instrução com valor monitorado isoladamente.

### 70009 = família de saída

Em `0x00455273` o validador compara `0x11179` (`70009`) dentro do conjunto aceito para a topologia de saída. O ramo de erro correspondente usa:

`X:%04d/Y:%04d OUT Circuit is incorrect !`

Isto prova associação à família de circuito de saída, mas não justifica reduzir o token simplesmente ao mnemônico `OUT`.

### 70017 = topologia especial

Em `0x0044808A` há comparação com `0x11181` (`70017`). No mesmo validador ele é agrupado com `70008`, `70016` e `70020`, condicionado pelos estados internos de topologia 5/6. A evidência sustenta classificação como membro especial/multioperando, não um mnemônico exato.

### 70027 = estrutura Boolean→Ladder

O conversor Boolean→Ladder emite o token `70008` em `0x00420FB7` e, no mesmo bloco de serialização, o literal `70027` (`0x004D3B18`) com forma de um operando: `70027 NNNNN 00000`. O token também participa de testes estruturais, como `0x0044BC65` (`0x1118B`).

## Relação com Q=4

A classificação de v1.44 permanece:

- classe 1: `70006`, `70007`, `70027`;
- classe 2: `70009`, `70017`.

A v1.45 mostrou que os quatro bytes podem ser consumidos como tipo interno 4 ou 7. A análise atual confirma que **o tipo 4/7 não é fixo por token**: a escolha depende do subramo e do estado do objeto, em particular do seletor interno já identificado (`[objeto+0x12DE]`). Portanto não se cria uma tabela falsa `token → tipo`.

## Integração no OpenLadderStudio

A v1.46 passa a executar no núcleo a decodificação já reconstruída:

- tipo 4: `b0 | b1<<8 | b2<<16 | b3<<24`;
- tipo 7: `b1 | b0<<8 | b3<<16 | b2<<24`;
- seletor não-zero → tipo 4;
- seletor zero → tipo 7;
- decimal com 10 dígitos e hexadecimal com 8 dígitos, como no PC12.

O decoder valida status, comprimento e checksum. Nenhuma transmissão nova é habilitada.

## Reprodutibilidade

Execute:

```text
python scripts/analyze_pc12_q4_tokens.py pc12.exe
```

Para o executável original esperado, o resultado termina em `RESULT=PASS`. A saída de referência está em `docs/data/pc12-q4-token-audit.txt`.

## Limites que permanecem

Ainda é necessário validar no TP02 físico os valores reais Q=4 e correlacioná-los com programas Ladder controlados. Também permanecem pendentes PG34 acima de 80 passos, validação física de 09/0A/35/RTC/EEPROM e compatibilidade entre firmwares.
