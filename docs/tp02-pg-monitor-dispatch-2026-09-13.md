# TP02 PG — dispatcher do monitor Ladder e descritores de 4 bytes

Data: 2026-09-13.

Esta etapa fecha a estrutura estática do dispatcher usado pelos dois caminhos de monitor Ladder do PC12 v2.1. A análise foi feita sobre o `pc12.exe` original enviado, SHA-256 `05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0`, sem abrir porta serial e sem executar qualquer TX.

## Resultado principal

O dispatcher em `004C3959` cobre exatamente os identificadores internos `70001` a `70027`. Cada ID seleciona uma entre cinco classes de tratamento:

```text
class 0 -> 004C3FFD
class 1 -> 004C3A89
class 2 -> 004C3BA4
class 3 -> 004C3988
class 4 -> 004C39A9
```

Tabela nativa de classes:

```text
ID      classe
70001   4
70002   4
70003   0
70004   0
70005   4
70006   1
70007   1
70008   3
70009   2
70010   0
70011   0
70012   0
70013   0
70014   0
70015   0
70016   0
70017   2
70018   0
70019   0
70020   0
70021   0
70022   0
70023   0
70024   0
70025   0
70026   0
70027   1
```

## Onde surgem os pedidos PG0A com Q=4

Foram confirmadas cinco instruções de máquina que gravam literalmente `04` no campo de quantidade do descritor PG0A:

```text
004C3AB9
004C3B45
004C3CCA
004C3E21
004C3F4F
```

O formato continua sendo o já identificado:

```text
0A LEN [A_H A_L Q] ... CHK
```

Os caminhos com Q=4 pertencem às classes 1 e 2 do dispatcher. Portanto, os IDs que podem alcançar esses ramos são:

```text
classe 1: 70006, 70007, 70027
classe 2: 70009, 70017
```

Isso fecha a dúvida deixada na v1.42: os descritores de quatro bytes não são uma variação genérica escolhida ao acaso; são ramos específicos do tipo de instrução monitorada.

## Vínculos textuais que o executável prova diretamente

A região de serialização Ladder contém os seguintes vínculos textuais próximos aos IDs. Eles são registrados como evidência, sem extrapolar os IDs restantes:

```text
70013 -> TMR
70020 -> CNT
70026 -> TMR
70015 -> OUT
70022 -> F-05
70023 -> F-06
70016 -> F-34
70024 -> F-41
```

Esses vínculos ajudam a interpretar a tabela, mas não autorizam preencher por inferência os nomes de todos os 27 IDs. A pesquisa mantém separados “ID interno identificado” e “semântica Ladder completamente nomeada”.

## Relação com os resultados anteriores

A v1.42 já havia comprovado que o monitor monta listas de descritores `A_H A_L Q` e que contatos/flags simples usam pedidos menores. Esta etapa acrescenta:

- a tabela completa de despacho dos 27 IDs internos;
- os cinco destinos de classe;
- a identificação estática de todos os cinco sites Q=4 neste dispatcher;
- o conjunto exato de IDs que alcança as classes com Q=4;
- associações textuais conservadoras para TMR, CNT, OUT e quatro funções especiais.

## Reprodutibilidade

Executar, apontando para o binário original:

```bash
python3 scripts/analyze_pc12_monitor_dispatch.py /caminho/pc12.exe
```

A saída de referência está em:

```text
docs/data/pc12-monitor-dispatch-static.txt
```

O script usa apenas a biblioteca padrão, valida o SHA-256 do executável, lê as seções PE diretamente e exige `RESULT=PASS`.

## O que ainda permanece aberto

Esta análise fecha a estrutura de seleção dos ramos, mas não substitui:

- emulação de todos os estados/operandos que levam a cada subramo das classes 1 e 2;
- distribuição completa dos quatro bytes recebidos na UI para cada ID;
- validação no TP02 físico desses pedidos;
- associação nominal dos IDs para os quais o executável não oferece vínculo textual suficientemente inequívoco nesta etapa.

Nenhum novo comando foi liberado para transmissão.
