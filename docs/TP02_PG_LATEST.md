# TP02 PG — ponto canônico de retomada

> Estado canônico em 2026-09-11 após a captura física de instruções variáveis, paginação READ-ONLY do `34`, reconstrução OFFLINE do `Write PLC Program`/PG33 e confirmação completa OFFLINE do `F-13w ADD`.

## Ambiente e segurança

```text
PLC: WEG TP02-60MR / TP02-40/60MR(T) V2.2.4K
OpenLadder Studio público: v1.04
PG Lab no código-fonte: 1.20
Serial de bancada: 19200 8O1, DTR ON, RTS OFF
Fluxo READ-ONLY: HELLO -> F0 -> 38 -> 34
```

A bancada permanece **READ-ONLY**. Nenhuma escrita, download, apagamento, firmware ou RUN/STOP remoto foi habilitado no PG Lab. `0F 00 F0` = Clear All Memory e permanece bloqueado.

A pesquisa do `0x33` é feita por análise estática e emulação Unicorn do `pc12.exe`, sem COM, sem PLC e sem execução da rotina TX.

## Leitura física consolidada

HELLO:

```text
TX STOP/RUN probe: CON-ICB\r
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

A regra física de checksum continua:

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

Constantes diretas observadas obedecem:

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

Não identificar automaticamente esse `BRAW` com o `EXTERNAL` do caminho PG33; continuam campos epistemicamente separados.

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

O software trata `38` apenas como hint/metadado, porque essa regra ainda não está provada universalmente e um único byte cria ambiguidade para programas maiores.

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

**Ainda falta confirmação física cruzando a fronteira 79/80.** O ensaio de 91 passos documentado em `docs/tp02-pg-pagination-34-v119.md` continua sendo o próximo teste de bancada READ-ONLY recomendado.

## Escrita de programa `0x33` — somente OFFLINE

O caminho `Write PLC Program...` do PC12 usa quadro dedicado `0x33`; `0x09` permanece uma família separada de escrita de memória/registradores.

Construtor final do `0x33`:

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

A primeira palavra é emitida pelo chamador. Para cada passo adicional, o helper `0x004BCA65` acrescenta outra palavra HIGH/LOW/EXTERNAL.

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

`cmp +0x7A, 0x14` confirma limite de **20 instruções lógicas por bloco**. Como cada uma pode ocupar até 4 palavras, um bloco pode chegar a **80 palavras de máquina**.

### Geometria do PG33

Para `W` palavras de máquina já expandidas:

```text
33 [3*W+4] 00 [step_hi] [step_lo] [2*W]
   [2*W bytes HIGH/LOW]
   [W bytes EXTERNAL]
   [checksum]

1 <= W <= 80
sum(quadro) mod 256 = FF
```

Pior caso reconstruído:

```text
20 instruções x 4 passos
W = 80
LEN = F4
HIGH/LOW count = A0
EXTERNAL = 80 bytes
quadro total = 247 bytes
```

O Core já modela isso em `Tp02Pg33DryRunFrame` e `Tp02Pg33DryRunProgram`, sem qualquer acesso serial.

## Confirmação OFFLINE dos operandos

O helper original `0x004BCA65`, executado no Unicorn, gerou:

```text
D0002 -> F0 01 / EXTERNAL 00
D0001 -> F0 00 / EXTERNAL 00
00010 -> 80 0A / EXTERNAL 00
00000 -> 80 00 / EXTERNAL 00
01000 -> 87 68 / EXTERNAL 00
```

A normalização interna também mostrou `00010 -> 000A` e `01000 -> 03E8` antes da palavra final.

Evidência:

```text
docs/data/pc12-pg33-operand-helper-emulation.txt
```

## F-13w ADD — caminho completo confirmado OFFLINE

O parser `0x004BA69E` recebe o registro interno de 48 bytes. Para `internal=513` cai no case `0x004BAD14`:

```text
HIGH = 0D
LOW  = 77
EXTERNAL = 00
StepSpan = 4
mode = 2
```

Em seguida o coletor chama `0x004BCA65` para os três operandos.

A emulação completa do programa sintético:

```text
F-13w ADD D0002,D0001,00010
```

produziu exatamente:

```text
0D 77 | F0 01 | F0 00 | 80 0A
EXTERNAL = 00 | 00 | 00 | 00
StepSpan = 4
cursor = 4
logical instructions = 1
```

Resultado do workflow:

```text
Analyze PC12 PG33 encoder fields
run #12 / 34559256817
RESULT=PASS
```

Isso coincide **byte a byte nos pares HIGH/LOW** com o F-13w já lido fisicamente do TP02 pelo `0x34`.

Evidências:

```text
docs/data/pc12-pg33-f13w-full-emulation.txt
docs/tp02-pg-f13w-offline-confirmation.md
```

Essa correlação é forte, mas ainda **não prova que o PLC aceite fisicamente um quadro `0x33`**.

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
codificação D0001/D0002/K10/K1000
F-13w completo = 0D77 F001 F000 800A
cursor real de passos
parser RX genérico
```

### Ainda pendente fisicamente

```text
paginação 34 >80 passos
aceitação do 0x33 pelo TP02
ACK exato do 0x33
sequência completa de sessão/handshake de escrita no fio
```

## Próximas prioridades

OFFLINE:

```text
1. repetir o coletor completo para uma instrução real de 2 passos, preferencialmente F-23 SET;
2. emular 20 instruções de 4 passos no coletor completo e confirmar W=80 no builder;
3. emular 21 instruções para confirmar a divisão física interna 20 + 1 e endereço do segundo quadro;
4. aprofundar a rotina de resposta do 0x33 para tentar recuperar o ACK esperado sem TX físico.
```

Bancada READ-ONLY:

```text
5. confirmar paginação do 34 com programa >80 passos.
```

Qualquer ensaio físico de escrita continua fora de escopo até decisão explícita e validação de segurança separada.
