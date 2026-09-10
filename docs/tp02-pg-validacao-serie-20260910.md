# Validação física — captura com dois contatos em série (2026-09-10 11:55 BRT)

## Objetivo

Registrar de forma permanente a captura `TP02-PG-Lab-20260910-115554.txt`, obtida com **OpenLadder Studio v1.04 / TP02 PG Lab 1.16**, PLC em STOP e fluxo estritamente READ-ONLY.

A intenção experimental anterior era testar dois contatos abertos em série antes da bobina `Y0003`. O log serial, porém, não registra textualmente os endereços presentes no ladder. Por isso, a identificação exata do primeiro contato deve ser inferida com cautela a partir dos bytes e, idealmente, confirmada pelo operador.

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

## Interpretação segura

### Passo 1 — opcode AND confirmado fisicamente

`LOW=0x21` satisfaz:

```text
0x21 & 0x78 = 0x20
```

Isso confirma em hardware o opcode **AND** recuperado do `pc12.exe`. Os três bits inferiores valem `1`, compatíveis com o segundo endereço dentro do grupo caso o contato seja `X0002`.

### Passo 2 — OUT Y0003 permanece estável

A bobina aparece como:

```text
HIGH=20 LOW=42 BRAW=08
```

exatamente como nas capturas mínimas anteriores com `Y0003`. A inserção de um segundo contato desloca a bobina para o passo seguinte sem alterar seu triplo observado.

### Passo 0 — forte evidência de que o primeiro contato continua X0009

O primeiro passo desta captura é:

```text
HIGH=01 LOW=10 BRAW=02
```

Esse triplo é **idêntico** ao obtido imediatamente antes no teste controlado `X0009 aberto -> Y0003`:

```text
X0009 aberto: HIGH=01 LOW=10 BRAW=02
```

Já o teste mínimo `X0001 aberto -> Y0003` havia produzido:

```text
X0001 aberto: HIGH=00 LOW=10 BRAW=01
```

Portanto, a explicação mais simples e atualmente mais forte é que o primeiro contato do ladder desta captura permaneceu **X0009 aberto**, e que foi adicionado um segundo contato compatível com `AND X0002` antes de `Y0003`.

Não se deve usar esta captura como prova de que `X0001` muda contextualmente para `HIGH=01/BRAW=02`, a menos que o operador confirme explicitamente que o ladder realmente continha X0001 como primeiro contato. Sem essa confirmação, promover tal interpretação seria confundir identidade de operando com contexto estrutural.

## Sequência física recuperada

Independentemente da identificação final do primeiro operando, a sequência de LOWs é:

```text
10 21 42
```

que corresponde a:

```text
STR
AND
OUT
```

Portanto, a estrutura serial de **dois contatos em série seguidos de bobina** está agora fisicamente demonstrada no TP02 no nível de opcode.

Se o primeiro contato for de fato X0009 e o segundo X0002, a leitura é:

```text
STR X0009
AND X0002
OUT Y0003
```

## O que esta captura confirma

- o retorno `38 = 00 02 00 04 F9` é reproduzível para um programa com dois contatos em série;
- o PG Lab 1.16 aceita corretamente esse retorno por validação estrutural e chega ao 34;
- o `34` é válido com 240 bytes de payload e checksum FF;
- a sequência de LOWs `10 21 42` confirma fisicamente `STR`, `AND`, `OUT`;
- `LOW=21` confirma o opcode AND em bancada;
- a bobina `Y0003` permanece `HIGH=20 LOW=42 BRAW=08`;
- o triplo do primeiro passo coincide exatamente com o X0009 previamente capturado.

## O que NÃO está confirmado

- que o primeiro contato desta captura era X0001; os bytes indicam fortemente X0009;
- a semântica completa de BRAW;
- a representação física de OR/paralelo;
- a posição/representação explícita de `End` dentro deste primeiro bloco;
- se há campos estruturais adicionais além dos três bytes por passo já observados.

## Próximo experimento recomendado

Antes de usar esta captura como baseline série/paralelo, confirmar visualmente qual foi o primeiro contato gravado no PC12.

Se ele era `X0009`, manter exatamente os mesmos operandos e converter apenas a ligação série para paralelo:

```text
      +-- X0009 aberto --+
------|                  |------(Y0003)
      +-- X0002 aberto --+
```

Se a intenção for comparar `X0001` e `X0002`, primeiro repetir a captura série garantindo explicitamente:

```text
X0001 aberto -- X0002 aberto -- (Y0003)
```

e só depois construir o paralelo equivalente.

## Segurança

Nenhuma escrita foi habilitada. O teste permaneceu no fluxo:

```text
HELLO -> F0 -> 38 -> 34
```

com PLC em STOP. `0F 00 F0` continua bloqueado, e nenhum comando de escrita, download, apagamento, firmware ou RUN/STOP remoto foi transmitido.
