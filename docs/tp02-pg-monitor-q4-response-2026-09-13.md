# TP02 PG — respostas Q=4 do monitor Ladder (2026-09-13)

## Resultado

A análise estática do `pc12.exe` original fecha a interpretação dos descritores `PG0A` que pedem quatro bytes no monitor Ladder.

A v1.44 já havia identificado cinco produtores `Q=04` e os IDs internos capazes de alcançá-los. Nesta etapa foi localizado o consumidor dessas respostas e reconstruída a ordem exata dos quatro bytes.

## Produtores Q=4

Os cinco sites nativos permanecem:

- `0x004C3AB9`
- `0x004C3B45`
- `0x004C3CCA`
- `0x004C3E21`
- `0x004C3F4F`

Todos gravam literalmente `04` no campo quantidade do descritor `0A`.

## Dois tipos nativos de 32 bits

O PC12 classifica esses descritores internamente como tipo `4` ou tipo `7`.

### Tipo 4

Consumidor em `0x004C5303`:

```text
valor = b0 | (b1 << 8) | (b2 << 16) | (b3 << 24)
```

É a composição little-endian direta de 32 bits.

### Tipo 7

Consumidor em `0x004C5536`:

```text
valor = b1 | (b0 << 8) | (b3 << 16) | (b2 << 24)
```

Isto equivale a receber cada palavra de 16 bits em big-endian, mantendo a palavra baixa antes da palavra alta.

Exemplo, resposta `12 34 56 78`:

- tipo 4 → `0x78563412`;
- tipo 7 → `0x56781234`.

## Seleção de tipo

Nos produtores `Q=4`, o byte interno `[objeto+0x12DE]` decide a variante:

- diferente de zero → tipo `4`;
- zero → tipo `7`.

O significado funcional desse flag ainda deve ser associado a todas as instruções/operandos, mas a interpretação da resposta já não é hipótese.

## Formatação nativa

Ambos os tipos de quatro bytes são exibidos pelo PC12 como inteiro sem sinal de 32 bits:

- decimal: `%010u`;
- hexadecimal: `%08X`.

O cursor de resposta avança exatamente quatro bytes após cada valor.

## Consequência para o OpenLadderStudio

O monitor pode agora tratar `Q=4` de maneira determinística quando o tipo interno correspondente for conhecido. Não é correto interpretar todos os quatro bytes com uma única endianidade.

## O que ainda falta

A parte offline que continua aberta é menor:

1. associar, quando possível, cada ocorrência das classes 1/2 ao tipo `4` ou `7` pelo contexto da instrução;
2. identificar todos os ramos em que o seletor `[+0x12DE]` muda;
3. reconstruir a ligação completa entre os IDs `70006`, `70007`, `70009`, `70017`, `70027` e a semântica Ladder onde o executável não deixa rótulo textual direto.

Dependem de bancada: respostas reais desses descritores em diferentes firmwares e estados do TP02.

## Reprodutibilidade

Execute:

```bash
python scripts/analyze_pc12_monitor_q4_response.py pc12.exe
```

O binário esperado possui SHA-256:

`05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0`

A execução deve terminar com `RESULT=PASS`.
