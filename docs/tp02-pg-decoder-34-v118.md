# TP02 PG Lab 1.18 — decoder misto do quadro 34

## Marco físico

A captura `TP02-PG-Lab-20260910-183546.txt` fechou o primeiro ensaio com instruções de tamanho variável no mesmo programa. O fluxo físico permaneceu estritamente READ-ONLY:

```text
HELLO -> F0 -> 38 -> 34
```

Não foi adicionado qualquer TX de escrita, RUN/STOP remoto, download, erase ou firmware.

O quadro observado foi:

```text
F0 = 00 02 10 22 CB
38 = 00 02 00 2C D1
34 = FLAGS 00, LEN F0, 240 bytes de payload, checksum final 52
```

## Programa de bancada

O programa gravado pelo PC12 continha:

```text
X0001 -> TMR V0001, 1000 -> C0001
X0002 + X0003(reset) -> CNT V0002, 10 -> C0002
X0004 -> F-23 SET Y0001
X0005 -> F-24 RST Y0001
X0006 -> F-13w ADD D0002, D0001, 10
X0007 -> OUT Y0002
F-00 END
```

O CNT usa duas entradas no Ladder do PC12: contagem e reset. No fluxo de passos do quadro 34 elas aparecem como dois `STR` consecutivos antes do prefixo CNT.

## Decodificação física dos 23 passos ativos

```text
000  00 10  B=01  STR X0001
001  00 60  B=06  TMR V0001
002  87 68  B=0D  K1000
003  40 40  B=08  OUT C0001
004  00 11  B=02  STR X0002
005  00 12  B=03  STR X0003
006  01 68  B=0F  CNT V0002
007  80 0A  B=02  K10
008  40 41  B=09  OUT C0002
009  00 13  B=04  STR X0004
010  17 71  B=00  F-23 SET
011  C8 80  B=0C  ARG Y0001
012  00 14  B=05  STR X0005
013  18 71  B=01  F-24 RST
014  C8 80  B=0C  ARG Y0001
015  00 15  B=06  STR X0006
016  0D 77  B=0B  F-13w ADD
017  F0 01  B=00  ARG D0002
018  F0 00  B=0F  ARG D0001
019  80 0A  B=02  K10
020  00 16  B=07  STR X0007
021  20 41  B=07  OUT Y0002
022  00 70  B=07  F-00 END
```

Os dados normalizados estão em:

```text
docs/data/tp02_pg_variable_instructions_20260910.tsv
```

## Regras agora fisicamente confirmadas

### TMR e CNT

```text
TMR V0001 = 00 60
CNT V0002 = 01 68
```

A numeração observada coincide com a reconstrução estática já existente no encoder:

```text
index = V - 1
HIGH  = index & 7F
LOW(TMR) = 60 | ((index >> 7) & 07)
LOW(CNT) = 68 | ((index >> 7) & 07)
```

### Constante imediata

Os valores `10` e `1000` confirmaram a codificação já reconstruída para a parte HIGH/LOW:

```text
10   -> 80 0A
1000 -> 87 68
```

Para os valores ensaiados:

```text
lowByte = valor & FF
highNibble = (valor >> 8) & 0F
HIGH = 80 | (highNibble << 1) | ((lowByte >> 7) & 01)
LOW  = lowByte & 7F
```

Não usar esta captura isolada para concluir como valores que dependam do byte externo estático do PC12 são transportados por outros comandos.

### SET / RST

```text
F-23 SET = 17 71
Y0001    = C8 80

F-24 RST = 18 71
Y0001    = C8 80
```

Logo, SET e RST ocupam dois passos físicos no quadro 34.

### F-13w ADD

```text
F-13w ADD = 0D 77
D0002     = F0 01
D0001     = F0 00
K10       = 80 0A
```

A função ocupa quatro passos físicos, exatamente como previa o mapa estático.

### END

```text
F-00 END = 00 70
```

O END está agora fisicamente confirmado no quadro 34.

## Região B — regra ampliada

A nova captura é importante porque força somas de nibble maiores que 15. Em todos os 23 passos ativos, sem exceção:

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
87 68 -> 8+7+6+8 = 29 -> 0D
18 71 -> 1+8+7+1 = 17 -> 01
F0 01 -> F+0+0+1 = 16 -> 00
```

A regra, antes confirmada somente em 26 instruções booleanas, agora também fechou para TMR, CNT, constantes, OUT C, SET, RST, operandos Y/D, F-13w e END.

O decoder 1.18 usa essa relação como verificação diagnóstica em todos os passos ativos. Uma divergência é registrada, mas não invalida automaticamente um quadro 34 com checksum global válido.

## Comando 38 com instruções multistep

O retorno foi:

```text
00 02 00 2C D1
```

A cauda ativa do quadro 34 termina no passo 22. Portanto:

```text
2 * 22 = 44 = 2C
```

Assim, a relação observada continua sendo:

```text
38.payload[1] = 2 * (N - 1)
```

agora também em programa com instruções de 2 e 4 passos. O decoder ainda mantém comportamento conservador: usa o 38 como hint somente quando ele coincide com a cauda ativa do próprio payload 34.

## Software 1.18

`Tp02Pg34Decoder.cs` foi ampliado para reconhecer fisicamente:

```text
STR/STR NOT/AND/AND NOT/OR/OR NOT/OUT em X/Y/C
TMR Vxxxx
CNT Vxxxx
constantes imediatas observadas
F-23 SET
F-24 RST
operando bit especial Y observado
F-13w ADD
operandos D observados
F-00 END
```

O autoteste agora possui dois fixtures físicos independentes:

```text
- matriz booleana de 26 passos / checksum 98 / 38=32
- programa misto de 23 passos / checksum 52 / 38=2C
```

Também foram adicionados testes explícitos do `BRAW mod 16`, inclusive `87 68 -> 0D`, `18 71 -> 01` e `F0 01 -> 00`.

O PG Lab 1.18 é produzido aplicando:

```text
PreparePgLabDecode34V27.ps1
PreparePgLabMixedDecodeV28.ps1
```

O V28 não acrescenta nenhuma ação serial; apenas expõe a decodificação ampliada e atualiza a identificação do motor para 1.18.

## Limites que permanecem

Ainda não considerar universalmente resolvido:

- todos os operandos normais X/Y/C/V/WX/WY/WC em funções;
- valores imediatos que exercitem todos os bits/escapes do formato estático;
- TMR/CNT acima das fronteiras de índice ainda não ensaiadas fisicamente;
- demais funções F-xx;
- paginação 34 em programas com mais de 80 passos;
- protocolo físico de escrita/download do programa.

A escrita continua fora de escopo até haver validação específica e deliberada de segurança.
