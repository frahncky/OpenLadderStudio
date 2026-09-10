# Validação física — AND NOT com X0002 (2026-09-10 12:54 BRT)

## Objetivo

Registrar a captura `TP02-PG-Lab-20260910-125412.txt`, obtida com **OpenLadder Studio v1.04 / TP02 PG Lab 1.16**, PLC em STOP e fluxo estritamente READ-ONLY.

O teste foi:

```text
X0009 aberto -- X0002 fechado -- (Y0003)
```

Baseline controlado anterior, com o mesmo circuito em série e X0002 aberto:

```text
X0009 aberto -- X0002 aberto -- (Y0003)
```

## Sessão válida

Na sessão 7, o fluxo conhecido completou:

```text
HELLO -> 80 01 09 75
F0    -> 00 02 10 22 CB
38    -> 00 02 00 04 F9
34    -> quadro de 243 bytes, LEN=F0, payload=240 bytes, checksum válido
```

O checksum final do quadro 34 foi `0x5E`.

## Payload 34 observado

Os únicos bytes não nulos do payload foram:

```text
payload[0x000] = 01
payload[0x001] = 10
payload[0x003] = 29
payload[0x004] = 20
payload[0x005] = 42
payload[0x0A0] = 02
payload[0x0A1] = 0B
payload[0x0A2] = 08
```

Pela geometria conhecida:

```text
passo i:
  HIGH = payload[2*i]
  LOW  = payload[2*i+1]
  BRAW = payload[0x0A0+i]
```

resulta:

```text
passo 0: HIGH=01 LOW=10 BRAW=02   -> STR X0009
passo 1: HIGH=00 LOW=29 BRAW=0B   -> AND NOT X0002
passo 2: HIGH=20 LOW=42 BRAW=08   -> OUT Y0003
```

## AND NOT confirmado fisicamente

Para o segundo passo:

```text
LOW = 0x29
0x29 & 0x78 = 0x28
```

A análise estática já associava `0x28` a **AND NOT**. Como o experimento manteve X0009, X0002, Y0003, a topologia em série e mudou somente o segundo contato de aberto para fechado em relação ao baseline série, esta captura confirma fisicamente:

```text
STR X0009
AND NOT X0002
OUT Y0003
```

A previsão anterior de `LOW=0x29` foi satisfeita exatamente.

## Comparação controlada com AND X0002

Série com X0002 aberto:

```text
passo 1: HIGH=00 LOW=21 BRAW=03
checksum = 0x6E
```

Série com X0002 fechado:

```text
passo 1: HIGH=00 LOW=29 BRAW=0B
checksum = 0x5E
```

Somente dois bytes do payload mudaram:

```text
payload[0x003]: 0x21 -> 0x29   (+0x08)
payload[0x0A1]: 0x03 -> 0x0B  (+0x08)
```

A soma das mudanças foi `+0x10`; o checksum caiu exatamente `0x10`, de `0x6E` para `0x5E`, mantendo a soma total do quadro em `0xFF`.

## Previsão de BRAW também confirmada

Antes da captura, `BRAW=0x0B` havia sido previsto apenas empiricamente com base no padrão de inversão. O hardware retornou exatamente:

```text
AND X0002:      BRAW=03
AND NOT X0002:  BRAW=0B
                  +08
```

Isso fornece um terceiro par controlado em que a negação/inversão acrescenta `0x08` ao BRAW, além de acrescentar `0x08` ao LOW.

## Matriz booleana básica agora fechada em bancada

As seis operações de contato básicas estão fisicamente observadas:

```text
STR      -> LOW & 0x78 = 0x10
STR NOT  -> LOW & 0x78 = 0x18
AND      -> LOW & 0x78 = 0x20
AND NOT  -> LOW & 0x78 = 0x28
OR       -> LOW & 0x78 = 0x30
OR NOT   -> LOW & 0x78 = 0x38
```

Além disso, `OUT` permanece observado com `LOW & 0x78 = 0x40`.

## Padrão de inversão

Agora há três pares independentes com o mesmo comportamento:

```text
STR      -> STR NOT  : LOW +08, BRAW +08
AND      -> AND NOT  : LOW +08, BRAW +08
OR       -> OR NOT   : LOW +08, BRAW +08
```

Classificação recomendada: **CONFIRMADO EM BANCADA para os pares ensaiados**. A semântica completa de BRAW continua desconhecida; o que está confirmado é apenas o efeito diferencial da inversão nesses casos.

## Comando 38

O `38` permaneceu:

```text
00 02 00 04 F9
```

Assim, `PAYLOAD[1]=0x04` permaneceu igual entre AND, AND NOT, OR e OR NOT para este programa de três passos. Isso mostra que esse byte não codifica diretamente o opcode booleano do segundo contato nos casos testados.

## Próximo experimento recomendado

A etapa mais informativa agora é voltar a um programa mínimo e testar uma nova fronteira de endereço:

```text
X0017 aberto -- (Y0003)
```

Objetivo: verificar como HIGH e BRAW evoluem no segundo salto de grupo, após X0009, e ajudar a separar endereçamento de metadados/transformações da Região B.

Em paralelo, permanece útil um teste futuro com três contatos para investigar se o byte variável do comando 38 acompanha quantidade de passos/instruções ou outra grandeza estrutural.

## Segurança

Nenhuma escrita foi habilitada. O teste permaneceu no fluxo:

```text
HELLO -> F0 -> 38 -> 34
```

com PLC em STOP. `0F 00 F0` continua bloqueado; nenhum comando de escrita, download, apagamento, firmware ou RUN/STOP remoto foi transmitido.
