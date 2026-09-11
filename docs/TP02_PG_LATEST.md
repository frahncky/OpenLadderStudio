# TP02 PG — ponto canônico de retomada

> Estado canônico em 2026-09-11 após a captura física de instruções variáveis, paginação READ-ONLY do `34`, reconstrução OFFLINE do `Write PLC Program`/PG33 e validação física completa do PG33 e restauração confirmada no PC12.

## Ambiente e segurança

```text
PLC: WEG TP02-60MR / TP02-40/60MR(T) V2.2.4K
OpenLadder Studio público: v1.22
PG Lab no código-fonte: 1.22
Serial de leitura já observado: 19200 8O1, com respostas em perfis de DTR/RTS distintos
Sessão exigida pela PG Lab 1.22 para escrita: mesmo perfil deve confirmar HELLO STOP e F0 sem fechar a COM
Fluxo READ-ONLY: HELLO -> F0 -> 38 -> 34
Fluxo controlado preparado: HELLO -> F0 -> 38 -> backup 34 -> PG33 TESTE -> readback 34 -> PG33 RESTORE -> readback 34
```

A PG Lab possui dois escopos distintos:

- a paginação do `34` e os ensaios ordinários permanecem **READ-ONLY**;
- a PG Lab 1.22 contém um procedimento físico separado de alteração mínima e restauração por `0x33`, acionado somente após confirmação explícita do operador.

O procedimento de escrita exige PLC em STOP, sessão `19200 8O1` em um perfil que confirme HELLO STOP e F0 na mesma porta aberta, backup prévio, uma única transmissão do PG33 de teste e, se necessário, uma única transmissão do PG33 de restauração. Nenhum RUN remoto, Clear All, WBP, apagamento ou firmware faz parte desse fluxo. `0F 00 F0` = Clear All Memory e permanece bloqueado.

A geometria do `0x33` foi obtida por análise estática e emulação Unicorn do `pc12.exe`. Em 2026-09-11, o ciclo físico foi confirmado no TP02-60MR: ACK `00 00 FF` no teste e na restauração, sentinela confirmada por `34` e programa original confirmado posteriormente no PC12.

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

# Escrita de programa `0x33` — modelo offline e procedimento físico controlado

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
mas o payload físico exato do ACK do 0x33 só pode ser promovido a fato confirmado
quando houver captura de bancada preservada no repositório.
```

A PG Lab 1.20 exige `00 00 FF` como ACK do teste e da restauração. Esse valor é uma hipótese operacional restritiva: satisfaz o parser genérico e impede aceitar respostas diferentes, mas o código e as notas de versão não substituem a captura física.

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
execução documentada do ciclo v1.20 completo
captura contínua da sequência completa de sessão/handshake no fio
caracterização dos NAK/erros físicos do 0x33
validação com programas diferentes do caso canônico de 23 passos
```

## Auditoria da PG Lab 1.22

O código preparado implementa as travas essenciais:

```text
OFF/OFF, ON/OFF e ON/ON são testados somente com HELLO/F0
HELLO RUN bloqueia antes do F0 e do PG33
HELLO STOP e F0 precisam responder na mesma COM e no mesmo perfil
F0 precisa retornar 00 02 10 22 CB
backup 34 é salvo antes da alteração
PG33 TESTE é transmitido no máximo uma vez
PG33 RESTORE é transmitido no máximo uma vez
não existe retransmissão cega após um PG33 duvidoso
falha após escrita manda manter o PLC em STOP
readback final precisa ser idêntico ao backup
nenhum RUN remoto é enviado
```

Limites constatados na auditoria:

```text
a v1.22 foi preparada especificamente para o programa conhecido de 23 passos
o ACK 00 00 FF ainda precisa de evidência física arquivada
o sucesso não pode ser inferido de mensagens, comentários ou release notes
a ausência de energia durante a janela teste/restauração pode deixar a sentinela gravada
o operador precisa preservar todos os arquivos da sessão antes de considerar o caminho fechado
```

### Validação física concluída em 2026-09-11

O ciclo controlado comprovou:

```text
backup original: 23 passos / END=0022
PG33 TESTE TX única
ACK TESTE: 00 00 FF
readback 34: sentinela exata STR X0001 / OUT Y0001 / END
PG33 RESTORE TX única
ACK RESTORE: 00 00 FF
quadro RESTORE: 23 palavras HIGH/LOW idênticas ao backup
programa final: confirmado no PC12 com as sete linhas originais e END
```

O readback final automático não conseguiu reacquirir HELLO+F0, mas a confirmação posterior pelo PC12 mostrou que a restauração foi concluída. A falha era de reconexão, não de conteúdo nem de ACK.

### Protocolo do próximo teste físico

Pré-condições obrigatórias:

```text
PLC desacoplado de máquina/cargas perigosas
seletor físico em STOP durante todo o procedimento
fonte estável e cabo TP-232PG já validado
programa original conhecido com 23 passos / END=0022
porta COM exclusiva para a PG Lab
proibição de RUN até o readback final aprovado
```

Critério único de aprovação:

```text
HELLO STOP em OFF/OFF
F0 = 00 02 10 22 CB
backup 34 salvo e validado
PG33 TESTE TX única
ACK TESTE = 00 00 FF
readback 34 = sentinela exata de 3 passos
PG33 RESTORE TX única, salvo se o original já estiver presente
ACK RESTORE = 00 00 FF, quando houver restore
readback final = backup original byte a byte
```

Artefatos mínimos a preservar:

```text
log completo da sessão
backup original
quadro PG33 TESTE transmitido
RX bruto do ACK TESTE
readback da sentinela
quadro PG33 RESTORE transmitido
RX bruto do ACK RESTORE
readback final
relatório PASS/FAIL
```

## Próximas prioridades

OFFLINE:

```text
1. repetir o coletor completo para F-24 RST e, se útil, TMR/CNT;
2. mapear o preâmbulo e a finalização da sessão Write PLC Program sem executar I/O;
3. manter o ACK físico como pendente até existir captura real arquivada.
```

Bancada READ-ONLY:

```text
4. confirmar paginação do 34 com programa >80 passos.
```

Bancada de escrita controlada:

```text
5. capturar em ponte a sequência completa já validada para documentar o handshake no fio;
6. estudar NAK/erros físicos sem repetir escrita automaticamente;
7. generalizar a escrita para programas compilados somente após novos casos controlados.
```

Se qualquer etapa posterior ao PG33 TESTE falhar, manter o PLC em STOP e verificar por leitura `34` qual programa está armazenado antes de qualquer nova ação.
