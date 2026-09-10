# Validação física — dois contatos abertos em série (2026-09-10 11:55 BRT)

## Objetivo

Registrar de forma permanente a captura física do programa:

```text
X0001 aberto -- X0002 aberto -- (Y0003)
```

usando **OpenLadder Studio v1.04 / TP02 PG Lab 1.16**, PLC em STOP e fluxo estritamente READ-ONLY.

Arquivo de bancada: `TP02-PG-Lab-20260910-115554.txt`.

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

## Interpretação controlada

### Passo 1 — AND X0002 confirmado fisicamente

`LOW=0x21` satisfaz:

```text
0x21 & 0x78 = 0x20
```

portanto confirma em hardware o opcode **AND** recuperado do `pc12.exe`. Os três bits inferiores valem `1`, coerentes com o segundo endereço dentro do grupo para X0002.

### Passo 2 — OUT Y0003 permanece estável

A bobina aparece como:

```text
HIGH=20 LOW=42 BRAW=08
```

exatamente como nos programas mínimos anteriores com Y0003. Isso mostra que a inserção de um segundo contato desloca a bobina para o passo seguinte sem alterar seu par HIGH/LOW observado.

### Passo 0 — STR X0001 com metadados contextuais

O primeiro contato conserva `LOW=0x10`, portanto continua sendo **STR**. Entretanto, comparado ao programa mínimo `X0001 aberto -> Y0003`, seus outros campos mudam:

```text
programa mínimo: HIGH=00 LOW=10 BRAW=01
programa em série: HIGH=01 LOW=10 BRAW=02
```

Como o operando X0001 não mudou, `HIGH` e `BRAW` não podem ser tratados como campos puramente independentes contendo apenas endereço do dispositivo. Há informação contextual/estrutural ou transformação adicional ainda não isolada.

Esse resultado reforça a correção feita após X0009: `BRAW` bruto não deve ser interpretado diretamente como o byte interno consumido pela rotina estática do decoder.

## O que esta captura confirma

- o retorno `38 = 00 02 00 04 F9` é reproduzível para o programa com dois contatos em série;
- o PG Lab 1.16 aceita corretamente esse retorno por validação estrutural e chega ao 34;
- o `34` é válido com 240 bytes de payload e checksum FF;
- a sequência de LOWs `10 21 42` é compatível exatamente com `STR`, `AND`, `OUT`;
- `AND X0002` está agora confirmado fisicamente no TP02;
- a bobina Y0003 permanece codificada como `20/42` no plano A;
- HIGH/BRAW do primeiro contato são dependentes do contexto do programa e não podem ser tratados como endereço isolado sem análise adicional.

## O que NÃO está confirmado

- o significado exato de `HIGH=01` no primeiro contato desta captura;
- o significado exato de `BRAW=02` e `BRAW=03` nos dois contatos;
- se esses campos codificam posição, ligação, estrutura de rung, índice interno ou outra transformação;
- a representação física de OR/paralelo;
- a posição/representação explícita de `End` dentro deste primeiro bloco.

## Próximo experimento recomendado

Construir os mesmos contatos em paralelo, sem mudar endereços nem a bobina:

```text
      +-- X0001 aberto --+
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
