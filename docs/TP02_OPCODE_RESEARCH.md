# Pesquisa de opcodes TP02 — RBP para Boolean/IL

Este documento registra apenas fatos observados ou documentados. Nenhuma palavra de máquina deve ser associada a uma instrução sem evidência reproduzível.

## Fatos confirmados no manual TP02

O comando `RBP` lê até 100 passos da memória de programa. Cada passo é retornado como uma palavra de **3 bytes**, representada por 6 caracteres hexadecimais:

- byte HIGH;
- byte LOW;
- byte externo (`EXT`).

Para TP02-40MR/TP02-60MR, a faixa de endereço do RBP é `0000–4000`.

O exemplo de comunicação publicado no manual para a leitura dos passos `0000–0002` retorna, na ordem:

```text
0000  5E1509
0001  204006
0002  20C10F
```

O manual comprova esses três WORDs como conteúdo do exemplo RBP, mas não publica a associação individual de cada WORD com uma instrução Boolean específica. Portanto, os três permanecem semanticamente `UNKNOWN` no software até calibração.

O conjunto básico de instruções documentado inclui `STR`, `STR NOT`, `AND`, `AND NOT`, `OR`, `OR NOT`, `AND STR`, `OR STR`, `OUT` e `NOP`. O manual também documenta `TMR` e `CNT`; essas duas instruções ocupam dois words na tabela de instruções.

Referência principal: manual WEG TP02 — Instalação e Programação, capítulos 9 e 13.

## Método de calibração

O PC12 Studio usa comparação diferencial de dumps. O procedimento recomendado é:

1. criar no PC12 um programa mínimo de referência;
2. ler o mesmo intervalo com RBP e salvar o dump A;
3. alterar **somente uma variável** no PC12;
4. ler novamente o mesmo intervalo e salvar o dump B;
5. usar `Decodificar RBP > COMPARAR DUMPS`;
6. observar o XOR dos 24 bits e quais bytes HIGH/LOW/EXT mudaram;
7. repetir com mudanças sistemáticas até separar os bits de opcode dos bits de operando.

### Série recomendada — endereço

Manter a instrução fixa e variar somente o endereço:

```text
STR X0001
STR X0002
STR X0003
STR X0010
STR X0016
```

Depois repetir para `Y`, `C` e `SC`.

### Série recomendada — opcode

Manter o mesmo operando e variar somente a instrução:

```text
STR X0001
STR NOT X0001
AND X0001
AND NOT X0001
OR X0001
OR NOT X0001
```

### Série recomendada — saída

```text
OUT Y0001
OUT Y0002
OUT C0001
```

### Instruções de dois words

`TMR` e `CNT` devem ser analisados separadamente porque o manual informa contagem de 2 words. Alterar apenas um parâmetro por experimento: identificador `V`, preset, presença de reset e tipo de preset.

## Critérios de confiança do mapa

- `Manual`: associação explicitamente publicada na documentação.
- `Teste controlado`: associação reproduzida por comparação de programas conhecidos.
- `Inferido por comparação`: padrão consistente, mas ainda sem quantidade suficiente de amostras para confirmação definitiva.
- `Não confirmado`: observação sem associação semântica segura.

O arquivo local `tp02_opcode_map.tsv` é gerado pelo decodificador e não é versionado pelo Git porque depende dos experimentos realizados com o hardware/projetos de referência.
