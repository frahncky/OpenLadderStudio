# TP02 PG — ponto canônico de retomada

> Estado mais recente da engenharia reversa do WEG TP02 após a captura física de instruções de tamanho variável, a implementação READ-ONLY da paginação genérica do comando 34 e a correção do modelo OFFLINE do `Write PLC Program`/PG33.

## Ambiente e política de segurança

```text
PLC: WEG TP02-60MR / TP02-40/60MR(T) V2.2.4K
OpenLadder Studio público: v1.04
PG Lab atual no código-fonte: 1.20
Serial de bancada: 19200 8O1, DTR ON, RTS OFF
Fluxo READ-ONLY conhecido: HELLO -> F0 -> 38 -> 34
```

A bancada continua **READ-ONLY**. Nenhuma escrita, download, apagamento, firmware ou RUN/STOP remoto foi habilitado no PG Lab. `0F 00 F0` é Clear All Memory e permanece bloqueado.

Toda a pesquisa atual sobre escrita `0x33` é feita por análise estática e emulação Unicorn do `pc12.exe`, sem COM, sem PLC e com a rotina de TX interceptada ou evitada.

## Última captura física relevante

Arquivo:

```text
TP02-PG-Lab-20260910-183546.txt
```

Fluxo válido:

```text
HELLO = 80 01 09 75
F0    = 00 02 10 22 CB
38    = 00 02 00 2C D1
34    = FLAGS 00, LEN F0, 240 bytes, checksum final 52
```

Programa gravado pelo PC12:

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

## Geometria física do quadro 34

Para `LEN=F0=240`:

```text
payload[0x000..0x09F] = Região A = 160 bytes = 80 pares HIGH/LOW
payload[0x0A0..0x0EF] = Região B = 80 bytes = 1 BRAW por posição

posição i:
HIGH = payload[2*i]
LOW  = payload[2*i+1]
BRAW = payload[0xA0+i]
```

Pedido:

```text
34 03 [step_hi] [step_lo] A0 chk
```

O endereço acompanha o contador de passos do programa.

## Código de máquina fisicamente confirmado

Booleanos:

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

Endereçamento X/Y/C confirmado no escopo ensaiado:

```text
group = (n - 1) >> 3
bit   = (n - 1) & 07

HIGH(X) = 00 + group
HIGH(Y) = 20 + group
HIGH(C) = 40 + group
LOW     = opcode | bit
```

Instruções mistas confirmadas:

```text
TMR V0001 = 00 60
K1000     = 87 68
OUT C0001 = 40 40

STR X0002 = 00 11
STR X0003 = 00 12
CNT V0002 = 01 68
K10       = 80 0A
OUT C0002 = 40 41

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

Assim, SET/RST ocupam 2 passos, F-13w ADD ocupa 4 e END ocupa 1 passo na leitura física observada.

## Região B do 34

Regra física confirmada na matriz booleana e no programa misto:

```text
BRAW = (
    (HIGH >> 4)
  + (HIGH & 0F)
  + (LOW >> 4)
  + (LOW & 0F)
) & 0F
```

Esse `BRAW` é um campo do caminho de leitura 34. **Não deve ser identificado automaticamente com o byte EXTERNAL do quadro de escrita PG33.**

## Comando 38

Observações físicas:

```text
2 passos  -> 02
3 passos  -> 04
26 passos -> 32
23 passos mistos -> 2C
```

Todos coincidem, nos programas ensaiados, com:

```text
38.payload[1] = 2 * (N - 1)
```

O decoder continua tratando o 38 como hint/metadado, não como autoridade universal para o tamanho total do programa.

## PG Lab 1.20 — paginação genérica READ-ONLY

O V30 gera páginas sucessivas do 34 em incrementos de 80 passos:

```text
0000 -> 34 03 00 00 A0 28
0050 -> 34 03 00 50 A0 D8
00A0 -> 34 03 00 A0 A0 88
00F0 -> 34 03 00 F0 A0 38
...
```

A leitura para em `F-00 END = 00 70` ou no limite de 4000 passos. Cada página dinâmica tem guarda local de estrutura/checksum e uma única tentativa.

**A paginação cruzando fisicamente a fronteira 79/80 ainda está pendente.** O programa de 91 passos de `docs/tp02-pg-pagination-34-v119.md` permanece como ensaio de bancada recomendado.

## Escrita de programa — comando 0x33, somente OFFLINE

O caminho `Write PLC Program...` do PC12 monta um quadro dedicado `0x33`. O construtor final fica em `0x004B7958`.

### Correção conceitual fechada em 2026-09-11

A interpretação antiga dizia “20 registros/palavras por quadro”. O rastreamento completo do helper `0x004BCA65` mostrou que isso estava incompleto.

O PC12 separa:

```text
INSTRUÇÃO LÓGICA
  StepSpan = 1..4
  conta 1 unidade em +0x7A
  produz 1..4 PALAVRAS DE MÁQUINA

PALAVRA DE MÁQUINA
  HIGH + LOW + EXTERNAL
  ocupa um passo no fluxo expandido
  é a unidade colocada no corpo do PG33
```

A primeira palavra é emitida pelo chamador. Para cada passo adicional, o helper `0x004BCA65` acrescenta outra palavra. No final comum do helper foram confirmadas estaticamente:

```text
TX[+0x5E] = HIGH
+0x5E++
TX[+0x5E] = LOW
+0x5E++
(+0xE0)[+0x62] = EXTERNAL
+0x62++
+0x56 += 2
```

O helper não incrementa `+0x7A`. Depois da expansão da instrução, o caminho comum faz:

```text
+0x76 += StepSpan
+0x7A += 1
```

Logo:

```text
+0x7A = quantidade de instruções lógicas do bloco
+0x56/2 = quantidade de palavras de máquina acumuladas
+0x62   = quantidade de bytes EXTERNAL acumulados
```

### Limite do bloco

`cmp +0x7A, 0x14` significa:

```text
máximo = 20 instruções lógicas por bloco
```

Como cada instrução pode ocupar 1..4 palavras:

```text
máximo = 80 palavras de máquina por quadro PG33
```

### Geometria corrigida do PG33

Defina `W` = total de palavras de máquina expandidas no bloco.

```text
33 [3*W+4] 00 [step_hi] [step_lo] [2*W]
   [2*W bytes HIGH/LOW]
   [W bytes EXTERNAL]
   [checksum]

1 <= W <= 80
sum(quadro) mod 256 = FF
```

Pior caso, 20 instruções × 4 passos:

```text
W = 80
LEN = F4h
HIGH/LOW = A0h = 160 bytes
EXTERNAL = 50h = 80 bytes
quadro total = 247 bytes
```

A emulação anterior do construtor com W=1,2,3,20 continua válida para confirmar a fórmula do quadro, mas o caso W=20 não representa o limite superior.

### Dry-run corrigido no Core

`Tp02Pg33DryRunFrame` agora trabalha com **1..80 palavras de máquina**.

`Tp02Pg33DryRunProgram` agora trabalha com **instruções lógicas** contendo 1..4 palavras, divide em no máximo 20 instruções por bloco, concatena todas as palavras e usa o cursor real de passos no bloco seguinte.

Autotestes cobrem:

```text
4 instruções com spans 1+2+3+4 -> 10 palavras
21 instruções -> blocos 20 + 1
primeiro bloco misto -> 50 palavras/passos
20 instruções de 4 passos -> 80 palavras, LEN=F4, HIGH/LOW=A0, 247 bytes
limite final exatamente em 4000 passos
```

O workflow `validate-tp02-compiler` run **#28 / 34557577774** passou integralmente após essa correção, incluindo os dois autotestes PG33 e a compilação do PG Lab.

O workflow dedicado `Analyze PC12 PG33 encoder fields` run **#3 / 34557229816** também passou e persistiu o relatório:

```text
docs/data/pc12-pg33-encoder-fields-analysis.txt
```

### ACK do 0x33 continua desconhecido

O validador RX do PC12 aceita genericamente quadros com checksum FF e usa bits do primeiro byte para flags. `00 00 FF` ser aceito no emulador significa apenas que satisfaz o parser; **não prova que seja o ACK físico do comando 0x33**.

Ainda não confirmados fisicamente:

```text
aceitação do 0x33 pelo TP02 real
payload/ACK exato do 0x33
sequência completa de sessão/handshake de escrita observada no fio
```

## Estado de evidência

### Confirmado fisicamente em leitura

```text
HELLO STOP/RUN
F0 conhecido, sem semântica final
38 nos programas ensaiados
34 página 0 / LEN F0 / HIGH-LOW / BRAW
booleanos X/Y/C ensaiados
TMR V0001
CNT V0002
K10 e K1000
F-23 SET
F-24 RST
F-13w ADD
F-00 END
```

### Confirmado no PC12 por análise + emulação offline

```text
comando de escrita de programa 0x33
construtor e checksum do quadro
separação instrução lógica x palavra de máquina
StepSpan 1..4
helper 0x004BCA65 emitindo palavras adicionais
limite de 20 instruções lógicas por bloco
até 80 palavras de máquina por bloco
cursor real preservado entre blocos
parser RX genérico
```

### Ainda pendente fisicamente

```text
paginação 34 >80 passos
aceitação do 0x33
ACK do 0x33
sessão física completa de escrita
```

## Próximas prioridades

Na bancada, a próxima ação continua READ-ONLY: confirmar a página 1 do 34 com o programa de 91 passos e PG Lab 1.20.

Em paralelo, somente offline:

```text
1. executar o caminho completo de uma instrução de 2 passos e capturar as duas palavras que chegam ao PG33;
2. fazer o mesmo com F-13w ADD de 4 passos;
3. comparar essas palavras com o vetor físico já lido pelo 34;
4. emular 20 instruções de 4 passos no coletor original e verificar W=80 / LEN=F4 / HIGH-LOW=A0;
5. emular 21 instruções para confirmar a divisão 20+1 no quadro real do PC12;
6. manter qualquer TX físico de escrita fora de escopo até decisão explícita separada.
```
