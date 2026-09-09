# Pesquisa de opcodes TP02 — RBP, Boolean/IL e encoder do PC12

Este documento registra o estado atual da pesquisa do formato de programa TP02. A política permanece conservadora: **código reconstruído não equivale a download validado em hardware**.

## O que o manual confirma

O comando `RBP` lê até 100 passos da memória de programa. Cada passo é retornado como uma palavra de **3 bytes**, representada por 6 caracteres hexadecimais:

- byte HIGH;
- byte LOW;
- byte externo (`EXT`).

Para TP02-40MR/TP02-60MR, a faixa documentada do RBP é `0000–4000`.

A amostra oficial para os passos `0000–0002` retorna:

```text
0000  5E1509
0001  204006
0002  20C10F
```

O manual documenta também as instruções Boolean básicas, TMR/CNT e o Host Protocol usado por RBP/WBP.

## Nova evidência: análise estática do PC12 2.1

A etapa anterior tratava todos os WORDs como semanticamente desconhecidos até calibração em bancada. Isso continua correto para **dados arbitrários lidos de um PLC**, mas já não representa todo o conhecimento disponível no projeto.

A análise estática do `pc12.exe` original localizou a rotina que transforma a representação Boolean/IL do PC12 em palavras HIGH/LOW/EXT. A partir dela foram reconstruídos:

- `STR`, `STR NOT`, `AND`, `AND NOT`, `OR`, `OR NOT`, `OUT`;
- palavras fixas `NOP`, `AND STR` e `OR STR`;
- endereçamento X/Y/C;
- primeira palavra de TMR/CNT;
- literais de 16 bits;
- forma normal de operandos de funções;
- modo especial de bit X/Y/C usado por determinadas F-xx;
- labels `F-42`, `JMP F-43` e `CALL F-44`;
- **91 formas internas de funções F-xx**, incluindo variantes `w` e `d` e as formas múltiplas de `F-33`/`F-33w`.

A documentação completa está em [`tp02-machine-code-reverse-engineering.md`](tp02-machine-code-reverse-engineering.md).

O mapa auditável está em [`data/tp02_function_map_normalized.csv`](data/tp02_function_map_normalized.csv).

O encoder experimental está em:

```text
src/OpenLadderStudio.Core/Tp02TargetCompiler.cs
```

## Exemplos de vetores reconstruídos

```text
STR X0001      -> 00 10 00
AND NOT X0002  -> 00 29 00
OUT Y0001      -> 20 40 00
TMR 0001       -> 00 60 00
CNT 0001       -> 00 68 00
literal 1000   -> 87 68 00
F-13 ADD       -> 0D 73 00
F-13w ADD      -> 0D 77 00
F-13d ADD      -> 0D F3 00
F-50w STMR     -> 32 77 00
```

## Critérios de confiança

O projeto passa a usar quatro categorias de evidência:

- `Manual` — comportamento publicado na documentação WEG;
- `Análise estática PC12` — comportamento obtido diretamente da rotina do executável original, com endereço auditável;
- `Teste controlado em hardware` — resultado reproduzido por PC12 -> PLC -> RBP;
- `Não confirmado` — observação ainda sem associação segura.

A análise estática é evidência mais forte que inferência por comparação, mas **não substitui o teste de escrita em hardware**. Por isso a montagem de WBP existe somente em dry-run.

## Método de validação em bancada

A campanha diferencial continua válida e agora serve para confirmar o encoder reconstruído:

1. criar no PC12 um programa mínimo de referência;
2. transferir pelo meio oficial já validado;
3. ler a mesma faixa com RBP;
4. gerar localmente os mesmos passos com `Tp02TargetCompiler`;
5. comparar HIGH/LOW/EXT byte a byte;
6. variar somente uma instrução ou operando por experimento;
7. promover a regra para `Teste controlado em hardware` quando a reprodução for consistente.

### Série mínima de endereço

```text
STR X0001
STR X0002
STR X0003
STR X0010
STR X0016
```

Depois repetir para `Y` e `C`.

### Série mínima de opcode

```text
STR X0001
STR NOT X0001
AND X0001
AND NOT X0001
OR X0001
OR NOT X0001
```

### Saídas

```text
OUT Y0001
OUT Y0002
OUT C0001
```

### Dois words e funções

Validar TMR/CNT e depois funções representativas como `F-23 SET`, `F-50w STMR` e `F-51 ZRST`.

## Regra de segurança

O driver de comunicação e o compilador permanecem separados. O compilador pode gerar bytes e quadros WBP para inspeção, mas **nenhuma rotina de transmissão WBP deve ser habilitada enquanto o mapa não estiver confirmado em um TP02 físico de teste**.
