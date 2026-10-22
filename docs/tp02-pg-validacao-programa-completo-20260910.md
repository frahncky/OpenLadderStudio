# Validação física — programa completo de diagnóstico (2026-09-10 13:55 BRT)

## Objetivo

Registrar a captura `TP02-PG-Lab-20260910-135502.txt`, obtida com **OpenLadder Studio v1.04 / TP02 PG Lab 1.16**, PLC em STOP e fluxo estritamente READ-ONLY.

Foi usado um único programa de diagnóstico com 11 rungs para cobrir, numa só captura, a matriz booleana básica e duas fronteiras de grupo de entradas e saídas.

## Sessão válida

Na sessão 7, o fluxo conhecido completou:

```text
HELLO -> 80 01 09 75
F0    -> 00 02 10 22 CB
38    -> 00 02 00 32 CB
34    -> quadro de 243 bytes, LEN=F0, payload=240 bytes, checksum válido
```

Checksum final do quadro 34: `0x98`.

## Programa e decodificação física

A região A contém 26 instruções ativas consecutivas. A captura corresponde exatamente ao programa de diagnóstico proposto:

```text
R1   STR X0001        00 10   BRAW=01
     OUT Y0001        20 40   BRAW=06

R2   STR NOT X0002    00 19   BRAW=0A
     OUT Y0002        20 41   BRAW=07

R3   STR X0009        01 10   BRAW=02
     AND X0002        00 21   BRAW=03
     OUT Y0003        20 42   BRAW=08

R4   STR X0009        01 10   BRAW=02
     AND NOT X0002    00 29   BRAW=0B
     OUT Y0004        20 43   BRAW=09

R5   STR X0009        01 10   BRAW=02
     OR X0002         00 31   BRAW=04
     OUT Y0005        20 44   BRAW=0A

R6   STR X0009        01 10   BRAW=02
     OR NOT X0002     00 39   BRAW=0C
     OUT Y0006        20 45   BRAW=0B

R7   STR X0008        00 17   BRAW=08
     OUT Y0007        20 46   BRAW=0C

R8   STR X0009        01 10   BRAW=02
     OUT Y0008        20 47   BRAW=0D

R9   STR X0016        01 17   BRAW=09
     OUT Y0009        21 40   BRAW=07

R10  STR X0017        02 10   BRAW=03
     OUT Y0010        21 41   BRAW=08

R11  STR X0018        02 11   BRAW=04
     OUT Y0011        21 42   BRAW=09
```

Todos os pares HIGH/LOW previstos antes do ensaio coincidiram com o hardware.

## Regra do plano A confirmada no escopo booleano ensaiado

Para X e Y nos endereços testados:

```text
group = (n - 1) >> 3
bit   = (n - 1) & 0x07

HIGH(X) = 0x00 + group
HIGH(Y) = 0x20 + group
LOW     = opcode | bit
```

Ficaram observadas no mesmo quadro as fronteiras:

```text
X0008 -> X0009
X0016 -> X0017
Y0008 -> Y0009
```

com reinício do índice de bit no LOW e incremento do grupo no HIGH.

## Descoberta principal: BRAW é soma dos nibbles de HIGH e LOW

Para **todas as 26 instruções ativas desta captura**, sem exceção:

```text
BRAW = (HIGH >> 4)
     + (HIGH & 0x0F)
     + (LOW  >> 4)
     + (LOW  & 0x0F)
```

Exemplos:

```text
STR X0018: HIGH=02 LOW=11
BRAW = 0 + 2 + 1 + 1 = 04

OR NOT X0002: HIGH=00 LOW=39
BRAW = 0 + 0 + 3 + 9 = 0C

OUT Y0009: HIGH=21 LOW=40
BRAW = 2 + 1 + 4 + 0 = 07
```

A mesma regra também é compatível com as capturas controladas anteriores de STR, STR NOT, AND, AND NOT, OR, OR NOT e OUT.

Conclusão recomendada: no escopo booleano já ensaiado, a Região B não é um campo independente de endereço; ela se comporta como uma **soma de nibbles redundante por instrução**, derivada diretamente do par HIGH/LOW.

Não generalizar ainda essa fórmula para TMR, CNT ou funções F-xx sem captura física equivalente.

## Comando 38: nova relação estrutural forte

O programa contém 26 instruções booleanas ativas, ocupando pares de dois bytes na Região A. O último par começa no deslocamento:

```text
2 * (26 - 1) = 50 = 0x32
```

O comando 38 retornou exatamente:

```text
00 02 00 32 CB
```

Nos programas anteriores com 2 e 3 instruções ativas, o byte variável foi respectivamente `0x02` e `0x04`, também iguais ao deslocamento do primeiro byte da última instrução na Região A.

Assim, há **EVIDÊNCIA FORTE** de que `38 payload[1]` representa o deslocamento/endereço do último par ativo da Região A, ou grandeza equivalente a `2*(N-1)` para instruções booleanas de um passo.

A semântica deve permanecer classificada como não totalmente resolvida até um teste com instrução de tamanho variável, como TMR/CNT ou função F-xx.

## Matriz booleana básica

A captura única confirma novamente, no mesmo programa:

```text
STR      -> 0x10
STR NOT  -> 0x18
AND      -> 0x20
AND NOT  -> 0x28
OR       -> 0x30
OR NOT   -> 0x38
OUT      -> 0x40
```

com:

```text
opcode = LOW & 0x78
```

## Resultado do ensaio completo

O teste único cumpriu o objetivo: não é necessário repetir individualmente os casos booleanos básicos nem X0018 isolado. A partir desta captura, a prioridade técnica deixa de ser repetir combinações de contatos e passa a ser:

1. implementar/validar no software a decodificação completa do primeiro bloco 34 usando HIGH/LOW e a regra de BRAW;
2. usar BRAW como verificação de integridade no escopo booleano, não como fonte independente de endereço;
3. investigar o significado final do comando 38 com uma instrução de tamanho variável;
4. depois, estender a mesma metodologia para TMR, CNT e funções F-xx.

## Segurança

Nenhuma escrita foi habilitada. O teste permaneceu no fluxo:

```text
HELLO -> F0 -> 38 -> 34
```

com PLC em STOP. `0F 00 F0` continua bloqueado; nenhum comando de escrita, download, apagamento, firmware ou RUN/STOP remoto foi transmitido.
