# TP02 PG — ponto canônico de retomada

> Estado mais recente da engenharia reversa do WEG TP02 após o teste completo de diagnóstico de 2026-09-10.

## Ambiente

```text
PLC: WEG TP02-60MR / TP02-40/60MR(T) V2.2.4K
OpenLadder Studio: v1.04
TP02 PG Lab: 1.16
Serial: 19200 8O1, DTR ON, RTS OFF
Estado: STOP
Fluxo READ-ONLY: HELLO -> F0 -> 38 -> 34
```

Nenhuma escrita está habilitada. `0F 00 F0` é Clear All Memory e permanece bloqueado.

## Leia primeiro

1. `docs/tp02-pg-validacao-programa-completo-20260910.md`
2. `docs/data/tp02_pg_complete_matrix_20260910.tsv`
3. `docs/TP02_PG_ESTADO_DA_ARTE.md`
4. `docs/data/tp02_pg_observations.tsv`

## Última captura

Arquivo:

```text
TP02-PG-Lab-20260910-135502.txt
```

Foi gravado um único programa com 11 rungs e 26 instruções booleanas, cobrindo STR, STR NOT, AND, AND NOT, OR, OR NOT, OUT e as fronteiras X0008/X0009, X0016/X0017 e Y0008/Y0009.

O quadro 34 foi válido, com 240 bytes de payload e checksum final `0x98`.

O comando 38 retornou:

```text
00 02 00 32 CB
```

## Plano A — regra fisicamente confirmada no escopo ensaiado

```text
group = (n - 1) >> 3
bit   = (n - 1) & 0x07

HIGH(X) = 0x00 + group
HIGH(Y) = 0x20 + group
LOW     = opcode | bit
```

Matriz de opcodes:

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

Todos os 26 pares HIGH/LOW do teste completo coincidiram com a previsão.

## Região B — descoberta principal

Nas 26 instruções ativas do teste completo, sem exceção:

```text
BRAW = (HIGH >> 4)
     + (HIGH & 0x0F)
     + (LOW  >> 4)
     + (LOW  & 0x0F)
```

Ou seja, no escopo booleano ensaiado, BRAW é exatamente a **soma dos quatro nibbles de HIGH e LOW**.

Exemplos:

```text
STR X0018:      HIGH=02 LOW=11 -> BRAW=04
OR NOT X0002:   HIGH=00 LOW=39 -> BRAW=0C
OUT Y0009:      HIGH=21 LOW=40 -> BRAW=07
```

A mesma regra é compatível com as capturas booleanas anteriores. Não generalizar ainda para TMR, CNT ou funções F-xx sem validação física.

## Comando 38 — relação estrutural forte

O programa completo possui 26 instruções booleanas consecutivas. O último par ativo da Região A começa em:

```text
2 * (26 - 1) = 50 = 0x32
```

O `38 payload[1]` retornou exatamente `0x32`.

Isso também coincide com os casos anteriores:

```text
2 instruções -> 0x02
3 instruções -> 0x04
26 instruções -> 0x32
```

Classificação atual: **EVIDÊNCIA FORTE** de que esse byte representa o deslocamento/endereço do último par ativo da Região A, ou grandeza equivalente a `2*(N-1)` para instruções booleanas de um passo.

Ainda falta testar instruções de tamanho variável antes de considerar a semântica do 38 completamente resolvida.

## Próxima etapa

Não é necessário repetir os testes booleanos isolados. A prioridade agora é de software:

```text
1. decodificar automaticamente o quadro 34 em HIGH/LOW/BRAW;
2. reconstruir X/Y e os opcodes booleanos;
3. validar BRAW pela soma dos nibbles;
4. usar o 38 para delimitar o trecho ativo quando a hipótese for aplicável;
5. depois estender a bancada para TMR, CNT e funções F-xx.
```

O próximo ensaio de hardware deve ser novamente um teste completo, não uma sequência de microtestes, e só é necessário quando formos validar instruções de tamanho variável.
