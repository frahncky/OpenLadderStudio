# TP02 PG — ponto canônico de retomada

> Estado mais recente da engenharia reversa do WEG TP02 após a captura física com instruções de tamanho variável, a implementação READ-ONLY da paginação genérica do comando 34 e a reconstrução dinâmica OFFLINE do caminho `Write PLC Program` do PC12.

## Ambiente e política de segurança

```text
PLC: WEG TP02-60MR / TP02-40/60MR(T) V2.2.4K
OpenLadder Studio público: v1.04
PG Lab usado na captura física mista: 1.16
PG Lab atual no código-fonte: 1.20
Serial de bancada: 19200 8O1, DTR ON, RTS OFF
Fluxo READ-ONLY conhecido: HELLO -> F0 -> 38 -> 34
```

A bancada continua **READ-ONLY**. Nenhuma escrita, download, apagamento, firmware ou RUN/STOP remoto foi habilitado no PG Lab. `0F 00 F0` é Clear All Memory e permanece bloqueado.

Toda a pesquisa recente sobre escrita (`0x33`) é feita por análise estática e emulação Unicorn do `pc12.exe`, sem COM, sem PLC e com a rotina de TX interceptada ou evitada.

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

Dados normalizados:

```text
docs/data/tp02_pg_variable_instructions_20260910.tsv
```

Documento detalhado:

```text
docs/tp02-pg-decoder-34-v118.md
```

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

O pedido é:

```text
34 03 [step_hi] [step_lo] A0 chk
```

O campo de endereço acompanha o contador de passos do programa.

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

A matriz física de 26 passos continua sendo a referência para STR, STR NOT, AND, AND NOT, OR, OR NOT e OUT, incluindo fronteiras X0008/X0009, X0016/X0017 e Y0008/Y0009.

## TMR / CNT fisicamente confirmados

Na captura mista:

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

O CNT usa entrada de contagem e entrada de reset; no fluxo de passos elas aparecem como dois `STR` antes do prefixo CNT.

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

SET/RST ocupam 2 passos, F-13w ADD ocupa 4 passos e END ocupa 1 passo no quadro 34 observado.

## Constantes imediatas

Fisicamente observadas:

```text
10   -> 80 0A
1000 -> 87 68
```

Para esses valores, HIGH/LOW coincide com a reconstrução estática do encoder. Não extrapolar automaticamente para todos os valores que dependam de bits externos ainda não ensaiados.

## Região B — regra física ampliada

A regra fechou na matriz booleana e nos 23 passos ativos do programa misto:

```text
BRAW = (
    (HIGH >> 4)
  + (HIGH & 0F)
  + (LOW >> 4)
  + (LOW & 0F)
) & 0F
```

Exemplos:

```text
87 68 -> 0D
18 71 -> 01
F0 01 -> 00
```

O decoder usa essa regra como verificação diagnóstica. Divergência BRAW não invalida automaticamente um quadro cujo checksum global seja válido.

## Comando 38

Resultados físicos:

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

ou seja, o offset em bytes do primeiro byte do último par HIGH/LOW ativo da Região A para os programas ensaiados. O decoder continua conservador e trata o 38 como metadado/hint, não como autoridade universal para o tamanho total do programa.

## PG Lab 1.20 — paginação genérica READ-ONLY

O código-fonte atual aplica a cadeia até:

```text
PreparePgLabDecode34V27.ps1
PreparePgLabMixedDecodeV28.ps1
PreparePgLabPaginationV29.ps1
PreparePgLabPaginationV30.ps1
```

O V30 generaliza o pedido `34`:

```text
página 0 -> start 0000 -> 34 03 00 00 A0 28
página 1 -> start 0050 -> 34 03 00 50 A0 D8
página 2 -> start 00A0 -> 34 03 00 A0 A0 88
página 3 -> start 00F0 -> 34 03 00 F0 A0 38
...
```

Incremento implementado: `0x0050 = 80` passos.

Critério de parada implementado:

```text
F-00 END = 00 70
```

Se END não aparecer, a próxima página é pedida em `start + 80`, sempre com uma única tentativa por página e guardas locais de estrutura/checksum. O limite absoluto é 4000 passos. O 38 é preservado apenas como hint.

**Estado epistemológico importante:** a geometria da página 34 e o contador de passos têm forte base física/estática, mas a paginação completa cruzando a fronteira 79/80 ainda precisa de uma captura física de programa com mais de 80 passos para ser promovida a fato de bancada.

Documento:

```text
docs/tp02-pg-pagination-34-v120.md
```

## Escrita de programa — reconstrução OFFLINE do comando 33

O caminho exato `Write PLC Program...` do PC12 monta um quadro dedicado com comando `0x33`. O construtor original em `0x004B7958` foi executado dentro do Unicorn e sua saída bateu byte a byte com o modelo independente para blocos de 1, 2, 3 e 20 registros.

Formato confirmado **dinamicamente offline no código original do PC12**:

```text
33 [3*N+4] 00 [step_hi] [step_lo] [2*N]
   [2*N bytes HIGH/LOW]
   [N bytes EXTERNAL]
   [checksum]

1 <= N <= 20 registros por quadro
sum(quadro) mod 256 = FF
```

Para `N=20`:

```text
LEN = 40h
HIGH/LOW = 28h = 40 bytes
EXTERNAL = 20 bytes
total = 67 bytes incluindo checksum
```

`N` é contador de registros do bloco. O cursor real de programa é separado e pode avançar 1, 2, 3 ou 4 passos por registro.

### Avanço de passo e limite de 20 registros

O trecho original `0x004B7799..0x004B7893` também foi executado no Unicorn. Resultado:

```text
span 1 -> cursor +1
span 2 -> cursor +2
span 3 -> cursor +3
span 4 -> cursor +4
```

Em todos os casos o contador de registros avança apenas uma unidade. Quando o contador passa de 19 para 20, o PC12 encerra o bloco e segue para a montagem do quadro, independentemente do span da instrução.

Relatório:

```text
docs/data/pc12-pg33-record-boundary-emulation.txt
```

### Transição entre blocos

Depois de um envio considerado bem-sucedido, o PC12 reinicializa o acumulador do bloco e preserva o cursor real de passos como endereço inicial do bloco seguinte:

```text
+56 = 0
+5E = 6
+62 = 0
+7A = 0
+7E = +76
```

O fluxo termina quando `cursor >= tamanho do programa`.

Essa transição também foi confirmada por execução offline do código original no Unicorn.

### Validador genérico de resposta

A rotina `0x0046F5E6` foi analisada estaticamente e seu trecho de validação foi executado no Unicorn com RX sintético.

Regras observadas:

```text
checksum: soma de todos os bytes RX deve fechar em FF
RX[0] & 80 -> F_ERROR
RX[0] & 20 -> flag separada 0x4FA8BE
```

Os fixtures offline passaram:

```text
00 02 10 22 CB -> aceito (resposta F0 física conhecida)
00 00 FF       -> aceito pelo validador genérico
80 00 7F       -> F_ERROR=1
00 00 FE       -> erro de checksum
20 00 DF       -> bit 20 armazenado em flag separada
```

`00 00 FF` ser aceito pelo parser **não prova** que seja o ACK real do `0x33`. O caminho PG33 não contém uma comparação específica adicional do payload depois das flags genéricas, portanto o ACK físico exato continua desconhecido.

Relatório:

```text
docs/data/pc12-response-validator-emulation.txt
```

### Dry-run multi-bloco no Core

Foi adicionada `Tp02Pg33DryRunProgram`, ainda sem qualquer serial. Ela recebe registros com HIGH/LOW/EXTERNAL e `StepSpan` 1..4, divide em blocos de até 20 registros e usa o cursor acumulado de passos no cabeçalho do bloco seguinte.

Autoteste principal:

```text
21 registros
spans 1,2,3,4 repetidos
primeiro bloco: 20 registros, avanço total = 50 passos
segundo bloco: começa em start + 50, não em start + 20
```

O workflow `validate-tp02-compiler` run `34555869424` passou integralmente, incluindo esse novo autoteste e a compilação do PG Lab.

O workflow offline `Analyze PC12 protocol offline` run `34555762703` também passou integralmente, incluindo validador RX, construtor PG33, avanço/limite de registros e transição de blocos.

## O que está confirmado e o que não está

### Confirmado fisicamente em leitura

```text
HELLO STOP/RUN
F0 conhecido (sem semântica final)
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

### Confirmado no código original do PC12 por análise + emulação offline

```text
quadro de escrita de programa 0x33
geometria e checksum do 0x33
limite de 20 registros por quadro
span de 1..4 passos por registro
cursor real preservado entre blocos
condição genérica de sucesso do parser RX
```

### Ainda pendente fisicamente

```text
paginação 34 cruzando a fronteira 79/80
aceitação do 0x33 pelo PLC real
ACK/payload exato do 0x33
sequência completa de sessão/handshake de escrita observada no fio
```

## Próximas prioridades

A prioridade de bancada continua sendo uma ação READ-ONLY: confirmar fisicamente a página 1 do comando 34 com o programa de 91 passos descrito em `docs/tp02-pg-pagination-34-v119.md`, usando o PG Lab 1.20.

Em paralelo, a pesquisa de escrita pode continuar **somente offline**:

```text
1. ampliar a emulação do coletor Write PLC Program para dois blocos consecutivos;
2. correlacionar os ramos do PC12 que produzem spans 1, 2, 3 e 4 com classes concretas de instrução;
3. comparar os registros produzidos pelo PC12 com o encoder do OpenLadder, sem TX;
4. manter o ACK físico do 0x33 como desconhecido enquanto não houver evidência direta;
5. não habilitar qualquer escrita física como consequência automática dessa pesquisa.
```

Depois da paginação física, continuar a ampliação READ-ONLY do decoder para fronteiras de TMR/CNT, operandos de outras famílias e constantes que exercitem bits externos ainda não observados.

A escrita física permanece fora de escopo até decisão explícita e validação de segurança separada.
