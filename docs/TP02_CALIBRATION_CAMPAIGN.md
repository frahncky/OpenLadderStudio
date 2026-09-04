# Campanha guiada de calibração TP02

A campanha organiza os experimentos controlados usados para descobrir a codificação das palavras de máquina retornadas pelo comando RBP.

## Objetivo

Separar, com evidência experimental, os bits que representam:

- opcode Boolean/IL;
- família do operando (X, Y, C, SC, V etc.);
- endereço do operando;
- segundo word de instruções como TMR/CNT.

Nenhuma regra inferida é considerada confirmada automaticamente.

## Como abrir

`PC12_v2.1_Windows7_v3_portatil/INICIAR_CAMPANHA_CALIBRACAO.bat`

## Testes pré-cadastrados

### Grupo A — campo de operando

- A1: STR X0001
- A2: STR X0002
- A3: STR X0004
- A4: STR X0016

### Grupo B — campo de opcode

- B1: STR X0001
- B2: STR NOT X0001
- B3: AND X0001
- B4: AND NOT X0001
- B5: OR X0001
- B6: OR NOT X0001

### Grupo C — famílias de endereço

- C1: STR X0001
- C2: STR Y0001
- C3: STR C0001
- C4: STR SC001

### Grupo D — saídas

- D1: STR X0001 / OUT Y0001
- D2: STR X0001 / OUT Y0002
- D3: STR X0001 / OUT C0001

### Grupo E — instruções de dois words

- E1: TMR V0001 preset 10
- E2: TMR V0002 preset 10
- E3: TMR V0001 preset 20
- E4: CNT V0001 preset 10

## Procedimento de bancada

1. Crie o projeto mínimo correspondente no PC12 original.
2. Compile usando o PC12.
3. Transfira para o TP02 apenas pelos meios oficiais já validados.
4. No leitor RBP moderno, leia sempre a mesma faixa de memória.
5. Salve o dump com o ID do teste no início do nome, por exemplo `A1_STR_X0001.rbpdump`.
6. Repita para os demais testes, alterando somente o item indicado.
7. Abra a campanha e use `IMPORTAR PASTA` para associar automaticamente os dumps.
8. Use `ANALISAR CAMPANHA`.
9. Exporte as regras candidatas em `.rules.tsv` e o relatório `.cal.txt`.

## Critério de segurança científica

Uma regra permanece `CANDIDATE` até que seja reproduzida em múltiplos endereços e famílias e não gere colisões com outras instruções. Somente depois dessa validação ela deve ser promovida para uso automático no decodificador.

A campanha é totalmente offline e não envia comandos ao PLC.
