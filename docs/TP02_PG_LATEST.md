# TP02 PG — ponto canônico de retomada

> Estado mais recente da engenharia reversa do WEG TP02 após o teste completo de diagnóstico e a implementação do decoder local do quadro 34.

## Ambiente de bancada confirmado

```text
PLC: WEG TP02-60MR / TP02-40/60MR(T) V2.2.4K
OpenLadder Studio: v1.04
PG Lab usado nas capturas: 1.16
Serial: 19200 8O1, DTR ON, RTS OFF
Estado: STOP
Fluxo READ-ONLY: HELLO -> F0 -> 38 -> 34
```

Nenhuma escrita está habilitada. `0F 00 F0` é Clear All Memory e permanece bloqueado.

## Software após a captura

O código-fonte já contém o **PG Lab 1.17**, ainda sem alterar a versão pública do OpenLadder Studio.

Novidades do 1.17:

```text
- Tp02Pg34Decoder no Core;
- separação automática HIGH/LOW/BRAW do primeiro bloco 34;
- reconstrução de STR, STR NOT, AND, AND NOT, OR, OR NOT e OUT para X/Y/C;
- validação BRAW pela soma dos nibbles somente no escopo booleano confirmado;
- uso conservador do 38 apenas como hint de delimitação;
- registros DECOD34 e IL34 no relatório do PG Lab;
- nenhum novo TX serial.
```

O workflow `validate-tp02-compiler` run #11 (`34515659960`) passou integralmente, incluindo autotestes antigos, novo autoteste do decoder e compilação do PG Lab 1.17.

Leia também:

1. `docs/tp02-pg-validacao-programa-completo-20260910.md`
2. `docs/tp02-pg-decoder-34-v117.md`
3. `docs/data/tp02_pg_complete_matrix_20260910.tsv`
4. `docs/TP02_PG_ESTADO_DA_ARTE.md`
5. `docs/data/tp02_pg_observations.tsv`

## Última captura física

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

Matriz física:

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

## Região B — regra confirmada para o conjunto booleano ensaiado

Nas 26 instruções ativas do teste completo, sem exceção:

```text
BRAW = (HIGH >> 4)
     + (HIGH & 0x0F)
     + (LOW  >> 4)
     + (LOW  & 0x0F)
```

No escopo booleano ensaiado, BRAW se comporta como soma redundante dos quatro nibbles de HIGH/LOW. Não generalizar ainda para TMR, CNT ou funções F-xx.

## Comando 38 — relação estrutural forte

Resultados físicos:

```text
2 instruções  -> payload[1] = 02
3 instruções  -> payload[1] = 04
26 instruções -> payload[1] = 32
```

Isto coincide com:

```text
2 * (N - 1)
```

para os programas booleanos de um passo ensaiados. No decoder 1.17, essa relação é deliberadamente tratada apenas como hint: se o 38 discordar da cauda ativa do payload 34, ele não é usado para truncar o programa.

## Próxima etapa física — um único teste completo

Não repetir microtestes booleanos. O próximo programa deve reunir instruções de tamanho variável em uma única captura para resolver TMR, CNT, operandos e testar o significado do 38 fora do caso puramente booleano.

Programa-alvo recomendado:

```text
R1  X0001 aberto -> TMR 1, preset K1000
R2  X0002 aberto -> CNT 1, preset K10
R3  X0003 aberto -> SET Y0001
R4  X0004 aberto -> RST Y0001
R5  X0005 aberto -> F-13w ADD D0001, K10, D0002
R6  X0006 aberto -> OUT Y0002
```

Esse ensaio permanece pequeno o suficiente para caber integralmente no primeiro bloco 34 e contém uma âncora booleana final conhecida.

Objetivos:

```text
- observar fisicamente TMR e seu preset;
- observar fisicamente CNT e seu preset;
- validar F-23 SET e F-24 RST;
- validar uma F-xx de quatro passos (F-13w ADD);
- testar BRAW fora do domínio booleano;
- testar 38 com instruções de múltiplos passos;
- manter tudo em uma única execução de bancada.
```
