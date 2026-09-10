# TP02 PG — ponto canônico de retomada

> Arquivo curto e estável para recuperação rápida do estado mais recente da engenharia reversa.

## Leia primeiro

1. `docs/TP02_PG_ESTADO_DA_ARTE.md` — base histórica detalhada.
2. `docs/tp02-pg-validacao-x0017-20260910.md` — validação física mais recente.
3. `docs/TP02_PG_CHECKPOINT_20260910_1255.md` — checkpoint da matriz booleana.
4. `docs/data/tp02_pg_observations.tsv` — observações estruturadas acumuladas.

## Estado atual em 2026-09-10 13:28 BRT

Hardware:

```text
WEG TP02-60MR / TP02-40/60MR(T) V2.2.4K
```

Software de bancada:

```text
OpenLadder Studio v1.04
TP02 PG Lab 1.16
```

Serial:

```text
19200 8O1
DTR ON
RTS OFF
```

Fluxo READ-ONLY conhecido:

```text
HELLO -> F0 -> 38 -> 34
```

Nenhuma escrita está habilitada. `0F 00 F0` é Clear All Memory e permanece bloqueado.

## Última captura física

Arquivo:

```text
TP02-PG-Lab-20260910-132650.txt
```

Programa:

```text
X0017 aberto -- (Y0003)
```

Resultado:

```text
STR X0017: HIGH=02 LOW=10 BRAW=03
OUT Y0003: HIGH=20 LOW=42 BRAW=08
checksum 34 = 90
38 = 00 02 00 02 FB
```

## Endereçamento X — fronteiras fisicamente confirmadas

Para contatos STR abertos no primeiro bit de cada grupo:

```text
X0001: HIGH=00 LOW=10 BRAW=01
X0009: HIGH=01 LOW=10 BRAW=02
X0017: HIGH=02 LOW=10 BRAW=03
```

O plano A confirma:

```text
group = (n - 1) >> 3
bit   = (n - 1) & 0x07
HIGH  = group                 [para classe X nos grupos ensaiados]
LOW   = opcode | bit
```

Assim, o segundo salto de grupo em X0017 está confirmado fisicamente e coincide com o encoder atual.

Para BRAW, há a regularidade física `01,02,03` nos inícios dos grupos 0,1,2, mas a semântica completa ainda não deve ser generalizada.

## Matriz booleana confirmada fisicamente

```text
STR      0x10
STR NOT  0x18
AND      0x20
AND NOT  0x28
OR       0x30
OR NOT   0x38
OUT      0x40
```

Identificação:

```text
opcode = LOW & 0x78
```

A negação/inversão acrescentou `0x08` tanto em LOW quanto em BRAW nos três pares controlados STR/STR NOT, AND/AND NOT e OR/OR NOT.

## Comando 38

Nos programas mínimos de um contato + bobina, X0009 e X0017 retornaram:

```text
00 02 00 02 FB
```

Logo, mudar o endereço do contato entre esses grupos não alterou o byte variável do 38 nesse formato mínimo.

## Próximo teste

Gravar exatamente:

```text
X0018 aberto -- (Y0003)
```

PLC em STOP, PC12 fechado, executar PG Lab 1.16 sem alterar o perfil serial.

Objetivo: medir o primeiro incremento dentro do grupo 2.

Previsão forte do plano A:

```text
HIGH = 02
LOW  = 11
```

BRAW permanece aberto; `04` é uma hipótese discriminatória, não um fato.

Depois disso, testar três contatos em série para investigar o significado do byte variável do comando 38.
