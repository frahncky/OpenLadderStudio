# TP02 PG — ponto canônico de retomada

> Estado mais recente da engenharia reversa do WEG TP02 após a captura física com instruções de tamanho variável e a ampliação do decoder local do quadro 34.

## Ambiente de bancada confirmado

```text
PLC: WEG TP02-60MR / TP02-40/60MR(T) V2.2.4K
OpenLadder Studio: v1.04
PG Lab usado na captura mista: 1.16
PG Lab atual no código-fonte: 1.18
Serial: 19200 8O1, DTR ON, RTS OFF
Estado: STOP
Fluxo READ-ONLY: HELLO -> F0 -> 38 -> 34
```

Nenhuma escrita está habilitada. `0F 00 F0` é Clear All Memory e permanece bloqueado.

## Última captura física

Arquivo:

```text
TP02-PG-Lab-20260910-183546.txt
```

O fluxo válido final foi:

```text
HELLO = 80 01 09 75
F0    = 00 02 10 22 CB
38    = 00 02 00 2C D1
34    = FLAGS 00, LEN F0, 240 bytes, checksum final 52
```

O programa gravado pelo PC12 continha:

```text
X0001 -> TMR V0001, 1000 -> C0001
X0002 + X0003(reset) -> CNT V0002, 10 -> C0002
X0004 -> F-23 SET Y0001
X0005 -> F-24 RST Y0001
X0006 -> F-13w ADD D0002, D0001, 10
X0007 -> OUT Y0002
F-00 END
```

O quadro 34 contém 23 passos ativos, de 0 a 22.

Dados normalizados:

```text
docs/data/tp02_pg_variable_instructions_20260910.tsv
```

Documento detalhado:

```text
docs/tp02-pg-decoder-34-v118.md
```

## Geometria do quadro 34

Para `LEN=F0=240`:

```text
payload[0x000..0x09F] = Região A = 160 bytes = 80 pares HIGH/LOW
payload[0x0A0..0x0EF] = Região B = 80 bytes = 1 BRAW por passo

step i:
HIGH = payload[2*i]
LOW  = payload[2*i+1]
BRAW = payload[0xA0+i]
```

## Booleano fisicamente confirmado

```text
opcode = LOW & 0x78

10 STR
18 STR NOT
20 AND
28 AND NOT
30 OR
38 OR NOT
40 OUT
```

Endereçamento confirmado para X/Y/C no escopo ensaiado:

```text
group = (n - 1) >> 3
bit   = (n - 1) & 07

HIGH(X) = 00 + group
HIGH(Y) = 20 + group
HIGH(C) = 40 + group
LOW     = opcode | bit
```

A matriz física anterior de 26 passos continua sendo a referência para STR, STR NOT, AND, AND NOT, OR, OR NOT e OUT, incluindo fronteiras X0008/X0009, X0016/X0017 e Y0008/Y0009.

## TMR / CNT fisicamente confirmados

Nesta captura:

```text
TMR V0001 = 00 60
K1000     = 87 68
OUT C0001 = 40 40

STR X0002 = 00 11
STR X0003 = 00 12   <- reset do CNT no Ladder do PC12
CNT V0002 = 01 68
K10       = 80 0A
OUT C0002 = 40 41
```

A geometria do PC12 ficou esclarecida: o CNT usa uma entrada de contagem e uma entrada de reset; no fluxo de passos elas aparecem como dois `STR` antes do prefixo CNT.

## Funções fisicamente confirmadas

```text
F-23 SET Y0001:
17 71
C8 80

F-24 RST Y0001:
18 71
C8 80

F-13w ADD D0002,D0001,10:
0D 77
F0 01
F0 00
80 0A

F-00 END:
00 70
```

Assim, SET/RST ocupam 2 passos, F-13w ADD ocupa 4 passos e END ocupa 1 passo no quadro 34 observado.

## Constantes imediatas

Os valores ensaiados confirmam:

```text
10   -> 80 0A
1000 -> 87 68
```

Para esses valores, a parte HIGH/LOW coincide com a reconstrução estática já presente no encoder. Não extrapolar ainda para todas as combinações que dependam de bits externos do formato estático do PC12.

## Região B — regra ampliada

A regra agora fechou tanto na matriz booleana de 26 passos quanto em todos os 23 passos ativos do programa misto:

```text
BRAW = (
    (HIGH >> 4)
  + (HIGH & 0F)
  + (LOW >> 4)
  + (LOW & 0F)
) & 0F
```

Exemplos que exigem o módulo 16:

```text
87 68 -> 0D
18 71 -> 01
F0 01 -> 00
```

O decoder usa essa regra como verificação diagnóstica em todos os passos ativos. Divergência BRAW é reportada, mas não invalida automaticamente um quadro com checksum global válido.

## Comando 38 — relação estrutural mais forte

Resultados físicos relevantes:

```text
2 passos  -> 02
3 passos  -> 04
26 passos -> 32
23 passos mistos -> 2C
```

Todos coincidem com:

```text
38.payload[1] = 2 * (N - 1)
```

ou seja, o offset em bytes do primeiro byte do último par HIGH/LOW ativo da Região A.

A captura de 23 passos é especialmente importante porque inclui instruções de 2 e 4 passos. Mesmo assim, o decoder permanece conservador: o 38 é usado como hint somente quando coincide com a cauda ativa do próprio quadro 34.

## Software atual — PG Lab 1.18

O OpenLadder Studio público permanece **v1.04**.

O código-fonte agora contém o **PG Lab 1.18**, produzido pela cadeia de patches até:

```text
PreparePgLabDecode34V27.ps1
PreparePgLabMixedDecodeV28.ps1
```

O `Tp02Pg34Decoder.cs` reconhece atualmente, no escopo fisicamente confirmado:

```text
STR / STR NOT / AND / AND NOT / OR / OR NOT / OUT em X/Y/C
TMR Vxxxx
CNT Vxxxx
constantes imediatas observadas
F-23 SET
F-24 RST
operando bit especial Y observado
F-13w ADD
operandos D observados
F-00 END
```

O autoteste do decoder possui dois fixtures físicos independentes:

```text
- matriz booleana: 26 passos, 38=32, checksum 98
- programa misto: 23 passos, 38=2C, checksum 52
```

O PG Lab continua READ-ONLY. O V28 apenas amplia a apresentação/identificação do decoder; não acrescenta qualquer TX serial.

## Próximas prioridades

Não é necessário repetir o mesmo programa misto. Os próximos ensaios úteis devem atacar lacunas específicas:

```text
1. paginação 34: programa com mais de 80 passos;
2. fronteiras TMR/CNT de índice (por exemplo V0128/V0129), se necessário;
3. operandos de funções em outras famílias: V, X/Y/C normal, WX/WY/WC;
4. constantes que exercitem bits externos ainda não observados;
5. outras F-xx prioritárias para o OpenLadder;
6. somente depois, estudo separado e deliberado do protocolo de escrita/download.
```

A escrita física permanece fora de escopo até haver decisão explícita e validação de segurança.
