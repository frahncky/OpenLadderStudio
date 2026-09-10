# TP02 PG — ponto canônico de retomada

> Arquivo curto e estável para recuperação rápida do estado mais recente da engenharia reversa.

## Leia primeiro

1. `docs/TP02_PG_ESTADO_DA_ARTE.md` — base histórica detalhada.
2. `docs/TP02_PG_CHECKPOINT_20260910_1255.md` — checkpoint operacional mais recente.
3. `docs/data/tp02_pg_observations.tsv` — observações estruturadas acumuladas.

## Estado atual em 2026-09-10 12:55 BRT

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
TP02-PG-Lab-20260910-125412.txt
```

Programa:

```text
X0009 aberto -- X0002 fechado -- (Y0003)
```

Resultado:

```text
STR X0009:      HIGH=01 LOW=10 BRAW=02
AND NOT X0002:  HIGH=00 LOW=29 BRAW=0B
OUT Y0003:      HIGH=20 LOW=42 BRAW=08
checksum 34 = 5E
38 = 00 02 00 04 F9
```

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

## Próximo teste

Gravar exatamente:

```text
X0017 aberto -- (Y0003)
```

PLC em STOP, PC12 fechado, executar PG Lab 1.16 sem alterar o perfil serial.

Objetivo: observar o segundo salto de grupo de endereço e restringir o modelo de HIGH/BRAW.

Depois, testar três contatos em série para investigar o significado do byte variável do comando 38.
