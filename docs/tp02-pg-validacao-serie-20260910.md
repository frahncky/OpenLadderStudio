# Validação física — captura com dois contatos em série (2026-09-10 11:55 BRT)

## Objetivo

Registrar de forma permanente a captura `TP02-PG-Lab-20260910-115554.txt`, obtida com **OpenLadder Studio v1.04 / TP02 PG Lab 1.16**, PLC em STOP e fluxo estritamente READ-ONLY.

O operador confirmou posteriormente que o primeiro contato gravado no PC12 era **X0009 aberto**. Portanto, o programa desta captura era:

```text
X0009 aberto -- X0002 aberto -- (Y0003)
```

## Sessão válida

Após várias sessões silenciosas, a sessão 7 completou a sequência conhecida:

```text
HELLO -> 80 01 09 75
F0    -> 00 02 10 22 CB
38    -> 00 02 00 04 F9
34    -> quadro de 243 bytes, LEN=F0, payload=240 bytes, checksum válido
```

O checksum final do quadro 34 foi `0x6E`.

## Payload 34 observado

Os únicos bytes não nulos do payload foram:

```text
payload[0x000] = 01
payload[0x001] = 10
payload[0x003] = 21
payload[0x004] = 20
payload[0x005] = 42
payload[0x0A0] = 02
payload[0x0A1] = 03
payload[0x0A2] = 08
```

Pela geometria já recuperada:

```text
passo i:
  HIGH = payload[2*i]
  LOW  = payload[2*i+1]
  BRAW = payload[0x0A0+i]
```

resulta:

```text
passo 0: HIGH=01 LOW=10 BRAW=02
passo 1: HIGH=00 LOW=21 BRAW=03
passo 2: HIGH=20 LOW=42 BRAW=08
```

## Interpretação confirmada

### Passo 0 — STR X0009

O primeiro passo é:

```text
HIGH=01 LOW=10 BRAW=02
```

Esse triplo é idêntico ao obtido no teste mínimo anterior `X0009 aberto -> Y0003`. Com a confirmação explícita do operador de que o primeiro contato permaneceu X0009, a identificação deixa de ser inferência e passa a ser **CONFIRMADA EM BANCADA**.

### Passo 1 — AND X0002

`LOW=0x21` satisfaz:

```text
0x21 & 0x78 = 0x20
```

confirmando em hardware o opcode **AND** recuperado do `pc12.exe`. Os três bits inferiores valem `1`, compatíveis com X0002 dentro do primeiro grupo.

### Passo 2 — OUT Y0003

A bobina aparece como:

```text
HIGH=20 LOW=42 BRAW=08
```

exatamente como nas capturas mínimas anteriores com `Y0003`.

## Sequência física recuperada

A sequência de LOWs é:

```text
10 21 42
```

correspondendo a:

```text
STR X0009
AND X0002
OUT Y0003
```

Portanto, a estrutura serial de **dois contatos abertos em série seguidos de bobina** está fisicamente demonstrada no TP02 no nível de opcode e, neste caso, também com os operandos confirmados pelo operador.

## O que esta captura confirma

- o retorno `38 = 00 02 00 04 F9` é reproduzível para este programa com dois contatos em série;
- o PG Lab 1.16 aceita corretamente esse retorno por validação estrutural e chega ao 34;
- o `34` é válido com 240 bytes de payload e checksum FF;
- `STR X0009` aparece como `HIGH=01 LOW=10 BRAW=02`;
- `AND X0002` aparece como `HIGH=00 LOW=21 BRAW=03`;
- `OUT Y0003` aparece como `HIGH=20 LOW=42 BRAW=08`;
- o opcode AND está confirmado fisicamente;
- a bobina Y0003 permanece estável no plano A;
- a antiga preocupação de que X0001 tivesse mudado contextualmente nesta captura está encerrada: o primeiro operando era X0009.

## O que NÃO está confirmado

- a semântica completa de BRAW;
- a transformação entre BRAW e a representação interna usada por rotinas do decoder do PC12;
- a representação física de OR/paralelo;
- a posição/representação explícita de `End` dentro deste primeiro bloco;
- se há campos estruturais adicionais além dos três bytes por passo já observados.

## Próximo experimento recomendado

Manter **exatamente os mesmos operandos** e alterar apenas a topologia de série para paralelo:

```text
      +-- X0009 aberto --+
------|                  |------(Y0003)
      +-- X0002 aberto --+
```

Objetivo principal: comparar byte a byte com esta captura em série. A análise estática prevê que o caminho booleano deverá expor **OR** (`LOW & 0x78 = 0x30`) ou palavras/estruturas auxiliares equivalentes, mas a bancada deve decidir a representação real.

## Segurança

Nenhuma escrita foi habilitada. O teste permaneceu no fluxo:

```text
HELLO -> F0 -> 38 -> 34
```

com PLC em STOP. `0F 00 F0` continua bloqueado, e nenhum comando de escrita, download, apagamento, firmware ou RUN/STOP remoto foi transmitido.
