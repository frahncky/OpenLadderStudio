# TP02 PG — mapa offline dos comandos de escrita do PC12

Data: 2026-09-10/11 · Método: análise estática + emulação Unicorn exclusivamente offline do `pc12.exe` · **Nenhum byte transmitido a um PLC**

## Aviso de escopo

Este documento descreve o que o `pc12.exe` monta no caminho `Write PLC Program`. Nada aqui habilita download no OpenLadder Studio. A bancada continua estritamente READ-ONLY: `0F 00 F0` permanece bloqueado e não há escrita, apagamento, firmware ou RUN/STOP remoto.

“Confirmado dinamicamente offline” significa que trechos do código de máquina original do PC12 foram executados no Unicorn com o ponto de TX interceptado. Isso **não** equivale a confirmação física do comando pelo TP02.

## Inventário resumido

| CMD | classe | papel atual |
|---:|---|---|
| `0xF0` | leitura/preflight | semântica exata ainda desconhecida |
| `0x38` | leitura | metadado estrutural do programa |
| `0x34` | leitura | páginas do programa |
| `0x0A` | leitura | memória/registradores |
| `0x09` | escrita | memória/registradores; família distinta da escrita de programa |
| `0x33` | escrita de programa | quadro dedicado encontrado em `Write PLC Program` |
| `0x0F` | escrita destrutiva | `0F 00 F0` = Clear All Memory |

# Comando dedicado `0x33`

A string `Write PLC Program...` leva ao caminho que monta o quadro em `0x004B7958` e, depois, chama a rotina genérica de comunicação `0x0046F5E6`. O primeiro byte do buffer TX é fixado em `0x33`.

Campos do coletor/construtor:

```text
+0x56 = quantidade de bytes HIGH/LOW acumulados
+0x5E = próximo índice HIGH/LOW no TX
+0x62 = quantidade de bytes EXTERNAL acumulados
+0x6A = byte alto do endereço inicial
+0x6E = byte baixo do endereço inicial
+0x76 = cursor real de passos
+0x7A = contador de INSTRUÇÕES LÓGICAS do bloco
+0x7E = passo inicial do bloco
```

## Instrução lógica x palavra de máquina

O PC12 trabalha com duas unidades:

```text
instrução lógica
    StepSpan = 1..4 passos
    produz 1..4 palavras de máquina
    incrementa +0x7A uma única vez

palavra de máquina
    HIGH + LOW + EXTERNAL
    ocupa um passo expandido
    é a unidade colocada no corpo do PG33
```

A primeira palavra é emitida pelo chamador. Para cada passo adicional, o helper `0x004BCA65` gera e acrescenta outra palavra.

Final comum do helper:

```text
TX[+0x5E] = HIGH
+0x5E++
TX[+0x5E] = LOW
+0x5E++
(+0xE0)[+0x62] = EXTERNAL
+0x62++
+0x56 += 2
```

Depois da expansão completa:

```text
+0x76 += StepSpan
+0x7A += 1
```

## Limite de bloco

Em `0x004B7869`:

```text
cmp +0x7A, 0x14
```

Logo:

```text
máximo = 20 instruções lógicas por bloco
máximo = 80 palavras de máquina quando todas têm StepSpan=4
```

## Geometria do quadro `0x33`

Defina `W` como a quantidade total de palavras já expandidas:

```text
TX[0] = 33
TX[1] = 3*W + 4
TX[2] = 00
TX[3] = step_hi
TX[4] = step_lo
TX[5] = 2*W

[2*W bytes HIGH/LOW]
[W bytes EXTERNAL]
[checksum]
```

Forma compacta:

```text
33 [3*W+4] 00 [step_hi] [step_lo] [2*W]
   [2*W bytes HIGH/LOW]
   [W bytes EXTERNAL]
   [checksum]

1 <= W <= 80
sum(quadro) mod 256 = FF
```

## Quadro máximo confirmado no código original

`scripts/emulate_pc12_pg33_f13w_batch20.py` executou o coletor e o builder originais para:

```text
20 x F-13w ADD D0002,D0001,10
```

Resultado:

```text
logical_instructions = 20
cursor = 80
HIGH/LOW = 160 bytes
EXTERNAL = 80 bytes
frame = 247 bytes
CMD = 33
LEN = F4
start = 0000
TX[5] = A0
checksum = 8C
RESULT = PASS
```

Evidência:

```text
docs/data/pc12-pg33-f13w-batch20-emulation.txt
```

## Endereço inicial e divisão entre blocos

A cauda de sucesso do PC12 reinicializa os contadores de corpo, preserva o cursor real e usa esse cursor como início do bloco seguinte.

A emulação de **21 F-13w** reproduziu exatamente:

```text
bloco 1:
  20 instruções / 80 palavras
  start=0000
  LEN=F4
  HIGH/LOW=A0
  total=247 bytes

após sucesso:
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

## Operand helper confirmado OFFLINE

`0x004BCA65` produziu:

```text
D0002 -> F0 01 / EXTERNAL 00
D0001 -> F0 00 / EXTERNAL 00
00010 -> 80 0A / EXTERNAL 00
00000 -> 80 00 / EXTERNAL 00
01000 -> 87 68 / EXTERNAL 00
```

A normalização interna mostrou também:

```text
D0002 -> 0001
D0001 -> 0000
00010 -> 000A
01000 -> 03E8
```

Evidência:

```text
docs/data/pc12-pg33-operand-helper-emulation.txt
```

# Correlação com instruções físicas lidas pelo `0x34`

## F-13w ADD

O parser `0x004BA69E`, para `internal=513`, usa o case `0x004BAD14`:

```text
HIGH = 0D
LOW = 77
EXTERNAL = 00
StepSpan = 4
mode = 2
```

Para:

```text
F-13w ADD D0002,D0001,10
```

O coletor original gerou:

```text
0D 77 | F0 01 | F0 00 | 80 0A
EXTERNAL = 00 | 00 | 00 | 00
```

Os pares HIGH/LOW coincidem byte a byte com a captura física PG34.

Evidências:

```text
docs/data/pc12-pg33-f13w-full-emulation.txt
docs/tp02-pg-f13w-offline-confirmation.md
```

## F-23 SET

Para:

```text
F-23 SET Y0001
```

O coletor original produziu:

```text
17 71 | C8 80
EXTERNAL = 00 | 00
StepSpan = 2
```

Os pares HIGH/LOW também coincidem byte a byte com a captura física PG34.

Evidência:

```text
docs/data/pc12-pg33-f23-full-emulation.txt
```

Essas correlações tornam forte a hipótese de que o PG33 transporta a representação expandida armazenada no programa. Ainda assim, **não provam aceitação física do `0x33` pelo PLC**.

# Resposta / ACK / retentativas

## Validador RX genérico

A rotina genérica de comunicação classifica a resposta através de:

```text
0x4FA8B7 = timeout/falha de comunicação
0x4FA8B9 = checksum inválido
0x4FA8B8 = resposta marcada como erro pelo bit 7 do primeiro byte
```

O validador exige que a soma da resposta feche em `FF`. Um quadro sintético como `00 00 FF` satisfazer o parser **não prova** que seja o ACK físico do `0x33`.

## Gate PG33 confirmado OFFLINE

A faixa `0x004B7A07..0x004B7C50` foi executada no Unicorn substituindo a comunicação real somente pelas três flags acima.

Resultado:

```text
sucesso na 1ª tentativa -> 1 chamada
falha + sucesso         -> 2 chamadas
2 falhas + sucesso      -> 3 chamadas
falha permanente        -> 3 chamadas
```

O máximo confirmado é, portanto, **3 tentativas por quadro**.

Foram ensaiadas separadamente falhas de:

```text
timeout
checksum
status/error
```

Todas seguem a mesma política de retentativa.

A faixa do chamador possui **zero referências diretas a RX_BUF/RX_LEN**. Logo, a lógica específica de retry só enxerga a classificação genérica feita por `0x0046F5E6`.

Consequência:

```text
conhecemos a condição de sucesso do PC12:
  timeout=0 AND checksum=0 AND error=0

mas o payload físico exato do ACK do 0x33 continua desconhecido.
```

Evidência:

```text
docs/data/pc12-pg33-retry-gate-emulation.txt
```

## Implementação segura no Core

```text
Tp02Pg33DryRunFrame
  recebe palavras já expandidas
  aceita 1..80 palavras
  monta somente a geometria do quadro

Tp02Pg33DryRunProgram
  recebe instruções lógicas de 1..4 palavras
  limita 20 instruções por bloco
  concatena as palavras
  avança o cursor pelo StepSpan real
```

Nenhuma dessas classes conhece `SerialPort` ou transmite bytes.

## `0x09` continua separado

O `0x09` permanece classificado como escrita de memória/registradores. Não deve ser confundido com o quadro dedicado `0x33` de `Write PLC Program`.

## Comando destrutivo

`0F 00 F0` é Clear All Memory. Continua bloqueado no PG Lab e não foi utilizado nos ensaios offline.

## Estado atual

Confirmado por análise + emulação OFFLINE:

```text
0x33 no caminho Write PLC Program
geometria/checksum
StepSpan 1..4
20 instruções lógicas por bloco
até 80 palavras por quadro
helper de operandos
F-13w completo
F-23 completo
quadro máximo de 247 bytes
divisão 20+1 / segundo start=0050
cauda de sucesso entre blocos
máximo de 3 tentativas por quadro
condição genérica de sucesso do chamador
```

Ainda não confirmado fisicamente:

```text
aceitação do 0x33 pelo TP02
payload/ACK real do 0x33
sequência completa da sessão de escrita observada no fio
```

## Próximas validações

OFFLINE:

1. Repetir o coletor completo para F-24 RST e, se necessário, TMR/CNT.
2. Mapear preâmbulo e finalização de `Write PLC Program` sem executar I/O.
3. Não promover um ACK sintético a protocolo físico sem observação real.

Bancada:

4. A próxima validação continua READ-ONLY: paginação `0x34` atravessando 80 passos.

Qualquer escrita física permanece fora de escopo até decisão explícita separada.
