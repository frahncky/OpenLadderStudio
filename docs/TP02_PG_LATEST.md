# TP02 PG — ponto canônico de retomada

> Estado canônico em 2026-09-11 após a captura física de instruções variáveis, paginação READ-ONLY do `34`, reconstrução OFFLINE do `Write PLC Program`/PG33, emulação completa de F-13w/F-23, blocos máximos e gate de resposta/retry.

## Ambiente e segurança

```text
PLC: WEG TP02-60MR / TP02-40/60MR(T) V2.2.4K
OpenLadder Studio público: v1.04
PG Lab no código-fonte: 1.20
Serial de bancada: 19200 8O1, DTR ON, RTS OFF
Fluxo READ-ONLY: HELLO -> F0 -> 38 -> 34
```

A bancada permanece **READ-ONLY**. Nenhuma escrita, download, apagamento, firmware ou RUN/STOP remoto foi habilitado no PG Lab. `0F 00 F0` = Clear All Memory e permanece bloqueado.

A pesquisa do `0x33` é feita por análise estática e emulação Unicorn do `pc12.exe`, sem COM, sem PLC e com a rotina TX interceptada antes de qualquer I/O.

## Leitura física consolidada

HELLO:

```text
TX: CON-ICB\r
STOP RX: 80 01 09 75
RUN  RX: C0 01 09 35
```

F0 conhecido:

```text
TX: F0 00 0F
RX: 00 02 10 22 CB
```

Semântica exata do F0 ainda desconhecida; ele não deve ser tratado como STOP.

Comando `34`:

```text
34 03 [step_hi] [step_lo] A0 chk
```

Para `LEN=F0=240`:

```text
Região A = 160 bytes = 80 pares HIGH/LOW
Região B = 80 bytes  = 80 BRAW
```

Checksum físico:

```text
sum(quadro) mod 256 = FF
```

## Código de máquina fisicamente confirmado

Booleanos:

```text
LOW & 78:
10 STR
18 STR NOT
20 AND
28 AND NOT
30 OR
38 OR NOT
40 OUT
```

Endereçamento ensaiado:

```text
group = (n - 1) >> 3
bit   = (n - 1) & 07
HIGH(X) = 00 + group
HIGH(Y) = 20 + group
HIGH(C) = 40 + group
LOW = opcode | bit
```

Captura física mista canônica:

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

F-23/F-24 ocupam 2 passos; F-13w ocupa 4; END ocupa 1 no programa observado.

Constantes diretas observadas:

```text
HIGH = 80 | (value >> 7)
LOW  = value & 7F
```

Exemplos: `10 -> 80 0A`, `1000 -> 87 68`.

## Região B do `34`

Fisicamente confirmada nos booleanos e na captura mista:

```text
BRAW = ((HIGH >> 4) + (HIGH & 0F) + (LOW >> 4) + (LOW & 0F)) & 0F
```

Não identificar automaticamente esse `BRAW` com o `EXTERNAL` do caminho PG33.

## Comando `38`

Nos programas ensaiados:

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

O software trata `38` apenas como hint/metadado; a regra ainda não está provada universalmente.

## Paginação READ-ONLY do `34`

`Tp02Pg34Pager` / PG Lab 1.20 geram páginas sucessivas de 80 passos:

```text
0000 -> 34 03 00 00 A0 28
0050 -> 34 03 00 50 A0 D8
00A0 -> 34 03 00 A0 A0 88
00F0 -> 34 03 00 F0 A0 38
...
```

A leitura termina ao encontrar `F-00 END = 00 70` ou no limite de 4000 passos.

**Ainda falta confirmação física cruzando a fronteira 79/80.** O ensaio de 91 passos em `docs/tp02-pg-pagination-34-v119.md` continua sendo o próximo teste de bancada READ-ONLY recomendado.

# Escrita de programa `0x33` — somente OFFLINE

O caminho `Write PLC Program...` do PC12 usa quadro dedicado `0x33`; `0x09` permanece uma família separada de escrita de memória/registradores.

Construtor final:

```text
0x004B7958
```

O PC12 separa:

```text
INSTRUÇÃO LÓGICA
  StepSpan = 1..4
  conta 1 em +0x7A
  produz 1..4 palavras de máquina

PALAVRA DE MÁQUINA
  HIGH + LOW + EXTERNAL
  ocupa um passo expandido
```

A primeira palavra é emitida pelo chamador. Cada passo adicional é emitido por `0x004BCA65`.

No final comum do helper:

```text
TX[+0x5E] = HIGH
+0x5E++
TX[+0x5E] = LOW
+0x5E++
(+0xE0)[+0x62] = EXTERNAL
+0x62++
+0x56 += 2
```

Depois da instrução completa:

```text
+0x76 += StepSpan
+0x7A += 1
```

`cmp +0x7A, 0x14` confirma limite de **20 instruções lógicas por bloco**. Como cada instrução pode ocupar até 4 palavras, um bloco pode chegar a **80 palavras de máquina**.

## Geometria do PG33

Para `W` palavras de máquina já expandidas:

```text
33 [3*W+4] 00 [step_hi] [step_lo] [2*W]
   [2*W bytes HIGH/LOW]
   [W bytes EXTERNAL]
   [checksum]

1 <= W <= 80
sum(quadro) mod 256 = FF
```

Pior caso reconstruído e emulado no construtor original:

```text
20 instruções x 4 passos
W = 80
LEN = F4
HIGH/LOW count = A0
EXTERNAL = 80 bytes
quadro total = 247 bytes
```

O Core modela isso em `Tp02Pg33DryRunFrame` e `Tp02Pg33DryRunProgram`, sem acesso serial.

## Operand helper confirmado OFFLINE

O helper original `0x004BCA65`, executado no Unicorn, gerou:

```text
D0002 -> F0 01 / EXTERNAL 00
D0001 -> F0 00 / EXTERNAL 00
00010 -> 80 0A / EXTERNAL 00
00000 -> 80 00 / EXTERNAL 00
01000 -> 87 68 / EXTERNAL 00
```

Evidência:

```text
docs/data/pc12-pg33-operand-helper-emulation.txt
```

## F-13w completo confirmado OFFLINE

Programa sintético:

```text
F-13w ADD D0002,D0001,00010
```

O coletor original do PC12 produziu:

```text
0D 77 | F0 01 | F0 00 | 80 0A
EXTERNAL = 00 | 00 | 00 | 00
StepSpan = 4
cursor = 4
logical instructions = 1
```

Isso coincide byte a byte nos pares HIGH/LOW com a captura física PG34 do mesmo ADD.

Evidências:

```text
docs/data/pc12-pg33-f13w-full-emulation.txt
docs/tp02-pg-f13w-offline-confirmation.md
```

## F-23 SET completo confirmado OFFLINE

Programa sintético:

```text
F-23 SET Y0001
```

O coletor original produziu:

```text
17 71 | C8 80
EXTERNAL = 00 | 00
StepSpan = 2
cursor = 2
logical instructions = 1
```

Também coincide byte a byte nos pares HIGH/LOW com a captura física PG34.

Evidência:

```text
docs/data/pc12-pg33-f23-full-emulation.txt
```

## Bloco máximo confirmado OFFLINE

Vinte instruções:

```text
20 x F-13w ADD D0002,D0001,00010
```

produziram no coletor + builder originais:

```text
logical instructions = 20
cursor = 80
HIGH/LOW bytes = 160
EXTERNAL bytes = 80
frame bytes = 247
CMD = 33
LEN = F4
start = 0000
HIGH/LOW count = A0
checksum = 8C
RESULT = PASS
```

Evidência:

```text
docs/data/pc12-pg33-f13w-batch20-emulation.txt
```

## Divisão 20 + 1 confirmada OFFLINE

Com 21 F-13w, o PC12 foi reproduzido em dois blocos:

```text
bloco 1:
  20 instruções / 80 palavras
  start=0000
  LEN=F4
  HIGH/LOW=A0
  total=247 bytes

cauda de sucesso:
  +56=0
  +5E=6
  +62=0
  +76=80
  +7A=0
  +7E=80

bloco 2:
  1 instrução / 4 palavras
  start=0050
  LEN=10
  HIGH/LOW=08
  total=19 bytes
```

Evidência:

```text
docs/data/pc12-pg33-f13w-blocks21-emulation.txt
```

## Gate de resposta/retry do PG33 confirmado OFFLINE

Faixa do chamador emulada:

```text
0x004B7A07..0x004B7C50
```

A rotina genérica de comunicação `0x0046F5E6` foi interceptada antes de I/O e substituída apenas pelas três flags que entrega ao chamador:

```text
0x4FA8B7 = timeout
0x4FA8B9 = checksum inválido
0x4FA8B8 = erro/status bit7
```

Resultados:

```text
sucesso imediato                 -> 1 chamada
1 falha + sucesso                -> 2 chamadas
2 falhas + sucesso               -> 3 chamadas
falha permanente por timeout     -> 3 chamadas
falha permanente por checksum    -> 3 chamadas
falha permanente por status      -> 3 chamadas
```

Portanto, o máximo confirmado é **3 tentativas por quadro**, não 15.

O chamador aceita o quadro quando as três flags ficam zeradas. A faixa de retry não possui referência direta a `RX_BUF` nem `RX_LEN`; ela depende da classificação feita pela rotina genérica.

Consequência epistemicamente importante:

```text
conhecemos a condição de sucesso do PC12,
mas NÃO conhecemos ainda o payload físico exato do ACK do 0x33.
```

Um quadro genérico como `00 00 FF` satisfazer o parser não prova que seja o ACK real do TP02.

Evidência:

```text
docs/data/pc12-pg33-retry-gate-emulation.txt
```

## Estado de evidência

### Confirmado fisicamente em leitura

```text
HELLO STOP/RUN
F0 conhecido, sem semântica final
38 nos casos ensaiados
34 página 0
booleanos X/Y/C
TMR V0001
CNT V0002
K10 / K1000
F-23 SET
F-24 RST
F-13w ADD
F-00 END
BRAW nos tipos ensaiados
```

### Confirmado no PC12 por análise + emulação OFFLINE

```text
comando de escrita de programa 0x33
construtor/checksum do 0x33
instrução lógica x palavra de máquina
StepSpan 1..4
20 instruções lógicas por bloco
até 80 palavras por quadro
helper de operandos
D0001/D0002/K10/K1000
F-13w completo
F-23 SET completo
bloco máximo W=80 / LEN=F4 / 247 bytes
divisão 20+1 e start real do segundo bloco
gate de sucesso/retry: máximo 3 tentativas
parser RX genérico
```

### Ainda pendente fisicamente

```text
paginação 34 >80 passos
aceitação do 0x33 pelo TP02
ACK exato do 0x33
sequência completa de sessão/handshake de escrita observada no fio
```

## Próximas prioridades

OFFLINE:

```text
1. repetir o coletor completo para F-24 RST e, se útil, TMR/CNT;
2. mapear o preâmbulo e a finalização da sessão Write PLC Program sem executar I/O;
3. manter o ACK físico classificado como desconhecido até existir observação real.
```

Bancada READ-ONLY:

```text
4. confirmar paginação do 34 com programa >80 passos.
```

Qualquer ensaio físico de escrita continua fora de escopo até decisão explícita e validação de segurança separada.
