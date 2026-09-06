# Formato de projeto `.pladder`

## Escopo

Este documento define os formatos de projeto aceitos pelo OpenLadder Studio. O leitor aceita as versões 1 e 2; toda nova gravação usa a versão 2.

O arquivo é texto UTF-8, com ou sem BOM, e aceita finais de linha CRLF ou LF. O cabeçalho deve ocupar a primeira linha do arquivo.

## Estrutura comum

Cada projeto começa por um destes cabeçalhos exatos:

```text
PC12-LADDER|1
PC12-LADDER|2
```

As linhas seguintes representam linhas Ladder e começam por `RUNG`. Cada linha contém exatamente oito colunas separadas por `|`. Linhas vazias são ignoradas. Um arquivo sem linhas Ladder é aberto com uma linha vazia no editor.

## Versão 1

Uma coluna da versão 1 contém um único elemento em série:

```text
RUNG|NO:X0001|NC:C0001|EMPTY|EMPTY|EMPTY|EMPTY|EMPTY|COIL:Y0001
```

Elementos aceitos:

| Token | Elemento |
|---|---|
| `EMPTY` | célula vazia |
| `NO:<endereço>` | contato normalmente aberto |
| `NC:<endereço>` | contato normalmente fechado |
| `COIL:<endereço>` | bobina |

A versão 1 não possui ramificação paralela nem regra de escape. O texto posterior ao primeiro `:` é preservado como endereço. Ao salvar um projeto importado, o aplicativo o migra para a versão 2.

## Versão 2

Cada coluna possui uma via em série e uma via paralela, separadas por `~`:

```text
<elemento-em-serie>~<elemento-em-paralelo>
```

O gravador sempre emite as duas vias, usando `EMPTY` quando necessário. Por compatibilidade, o leitor também aceita uma coluna sem `~` e considera vazia a via paralela.

Exemplo:

```text
PC12-LADDER|2
RUNG|NO:X0001~NC:C0001|TMR:V0001:25:RESET~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|EMPTY~EMPTY|COIL:Y0001~EMPTY
```

Tokens aceitos:

| Token | Campos |
|---|---|
| `EMPTY` | nenhum |
| `NO:<endereço>` | endereço |
| `NC:<endereço>` | endereço |
| `COIL:<endereço>` | endereço |
| `TMR:<endereço>[:<parâmetro>[:<modo>]]` | endereço, parâmetro e modo |
| `CNT:<endereço>[:<parâmetro>]` | endereço e parâmetro |
| `SET:<endereço>` | endereço |
| `RST:<endereço>` | endereço |
| `EUP` | nenhum |
| `EDN` | nenhum |
| `FUN:<endereço>[:<parâmetro>]` | endereço e parâmetro |
| `END` | nenhum |

## Escape dos campos na versão 2

Endereço, parâmetro e modo usam o escape percentual de `Uri.EscapeDataString`. Como `~` é o separador de vias e normalmente não é escapado por essa API, o gravador o converte obrigatoriamente para `%7E`.

Os delimitadores e controles devem aparecer codificados dentro de campos:

| Valor | Codificação |
|---|---|
| `~` | `%7E` |
| `|` | `%7C` |
| `:` | `%3A` |
| `%` | `%25` |
| CR | `%0D` |
| LF | `%0A` |

Cada `%` deve ser seguido por exatamente dois dígitos hexadecimais. Entradas como `%`, `%2` e `%2G` são inválidas e devem gerar erro com linha e coluna. Letras hexadecimais maiúsculas e minúsculas são aceitas.

Exemplo de endereço:

```text
NO:Sinal%7EA%7CB%3AC%25%2Facao~EMPTY
```

O valor decodificado é `Sinal~A|B:C%/acao`.

## Compatibilidade e validação

- cabeçalhos desconhecidos são recusados;
- cada linha Ladder deve ter oito colunas;
- cada coluna v2 pode ter no máximo duas vias;
- tipos desconhecidos e quantidades inválidas de campos são recusados;
- a versão 1 permanece somente para leitura e migração;
- a versão 2 é o único formato produzido pelo gravador.

As fixtures normativas ficam em `tests/OpenLadderStudio.Core.Tests/Fixtures` e são executadas por `OpenLadderCoreTest.exe` durante o build e no GitHub Actions.
