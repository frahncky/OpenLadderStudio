# TP02 PG — leitura de programa recuperada por emulação do PC12

Data: 2026-09-10 · Método: análise estática e emulação do `pc12.exe` · **Nenhum byte transmitido a um PLC**

## Objetivo

O documento canônico [`TP02_PG_ESTADO_DA_ARTE.md`](TP02_PG_ESTADO_DA_ARTE.md) registra como
desconhecidos a paginação do comando `34`, o significado do byte `0x20` em
`payload[0x002]` e a razão de a informação aparecer em duas regiões do bloco. Esta
investigação responde os três pontos sem bancada, executando as próprias rotinas do
PC12 2.1 dentro de um emulador.

A ferramenta é [`scripts/emulate_pc12_readprog.py`](../scripts/emulate_pc12_readprog.py).

## Método

O `emulate_pc12_frames.py` existente para no primeiro acesso à rotina de transmissão,
então só recupera o primeiro quadro de cada fluxo. A leitura de programa é um laço, e o
que interessa é a progressão.

A ferramenta nova intercepta a rotina de transmissão (`0x46F5E6`): captura o quadro
montado, sintetiza uma resposta estruturalmente válida no buffer de recepção e devolve o
controle ao chamador. O laço do PC12 prossegue e monta o quadro seguinte.

Contrato da rotina de transmissão, recuperado do binário:

| Endereço | Papel |
|---|---|
| `0x4FA7A8` | buffer de transmissão |
| `0x4FA8AC` | comprimento a transmitir |
| `0x530230` | buffer de recepção |
| `0x4FA8B0` | quantidade de bytes recebidos |
| `0x4FA8B7` | falha de comunicação |
| `0x4FA8B8` | resposta de erro (bit `0x80` do primeiro byte) |
| `0x4FA8B9` | checksum inválido |

Sucesso para o chamador é `0x4FA8B7 = 0x4FA8B8 = 0x4FA8B9 = 0`.

Nada roda no sistema hospedeiro: o código executa dentro do emulador, sem acesso a disco,
rede ou porta serial. As chamadas de API são interceptadas e retornam sucesso sem efeito.

## Duas guardas antes de qualquer transmissão

| Global | Significado | Mensagem quando falha |
|---|---|---|
| `0x5703E8` | porta serial enlaçada | `COM Port Unlink !` |
| `0x5704A8` | código do modelo de PLC | `Machine Type Error !` |

O modelo seleciona o tamanho de programa, gravado no global `0x560368`:

| Código | Modelo | Passos |
|---:|---|---:|
| 1 | `TP02-40/60MR(T)` | 4000 (`0xFA0`) |
| 3 | — (rótulo `1.5K`) | 1500 (`0x5DC`) |

`0x560368` é exatamente o limite do laço de paginação. Para o TP02, 4000 passos.

## Sequência reproduzida

Com as duas guardas satisfeitas, a emulação do fluxo em `0x004AEC35` monta:

```text
1   F0 00 0F            preflight
2   38 00 C7            metadado do programa
3   34 03 00 00 A0 28   primeira leitura: passo 0x0000, quantidade 0xA0
```

É a mesma sequência que a bancada descobriu experimentalmente — obtida aqui sem PLC.

## Paginação do comando 34 — resolvida

```text
34 03 [passo_hi] [passo_lo] A0 chk
```

O endereço **não é um deslocamento em bytes nem um passo fixo**. É o contador de passos
do programa, formatado com `%04X` (string em `0x4F4FB2`) e reconvertido dígito a dígito
para dois bytes, gravados nos índices 2 e 3 do quadro. A quantidade é fixa em `0xA0`
(160) e o checksum é `0xFF` menos a soma dos cinco primeiros bytes.

O contador avança **de 1 a 4 por instrução decodificada**, conforme o tamanho de cada
instrução. Ou seja: **a paginação é determinada pelo conteúdo do programa, não por um
passo constante.** Isso descarta a hipótese de trabalho anterior de incrementos fixos de
`0xF0` (`00F0`, `01E0`, `02D0`), que o estado da arte corretamente nunca promoveu a fato.

O laço termina quando o contador alcança o tamanho de programa do modelo.

## Geometria do bloco — resolvida

O decodificador do PC12 usa dois cursores sobre o mesmo bloco:

```text
início da região B = 2 + (LEN * 2) / 3        fim do bloco = LEN + 2
cursor da região A começa no índice 3         avança 2 bytes por instrução
cursor da região B começa no início de B      avança 1 byte por instrução
```

Para `LEN = 0xF0` (240 bytes):

```text
quadro: [FLAGS] [LEN] [payload de 240 bytes] [CHECKSUM]

região A   payload[0x000 .. 0x09F]   160 bytes   2 por passo
região B   payload[0x0A0 .. 0x0EF]    80 bytes   1 por passo

80 passos por bloco, 3 bytes por passo

passo i:   A = payload[2i], payload[2i+1]      B = payload[0x0A0 + i]
```

A região B começa em `payload[0x0A0]` — exatamente onde a bancada observou a "segunda
região". Os campos que o estado da arte descreve como duplicados em `0x001`/`0x0A0` e
`0x003`/`0x0A1` **não são duplicatas**: são os planos A e B do mesmo passo.

O cursor da região A começa no índice 3 do quadro, isto é, em `payload[0x001]` — que é
precisamente onde a bancada observou o opcode do contato. O PC12 lê o byte baixo antes do
alto.

## Conferência com as capturas de bancada

Aplicando a fórmula do encoder já implementada em
[`Tp02TargetCompiler.cs`](../src/OpenLadderStudio.Core/Tp02TargetCompiler.cs) sobre a
geometria acima:

| Teste | Ladder | `[0x001]` baixo | `[0x002]` alto | `[0x003]` baixo |
|---|---|---|---|---|
| A | X0001 NF → Y0002 | `18` ✓ | `20` ✓ | `41` ✓ |
| B | X0002 NF → Y0002 | `19` ✓ | `20` ✓ | `41` ✓ |
| C | X0002 NF → Y0003 | `19` ✓ | `20` ✓ | `42` ✓ |
| D | X0002 NA → Y0003 | `11` ✓ | `20` ✓ | `42` ✓ |
| E | X0001 NA → Y0003 | `10` ✓ | `20` ✓ | `42` ✓ |

As cinco capturas batem. O `0x20` de `payload[0x002]` deixa de ser desconhecido: é o byte
**alto** do passo da bobina — base do dispositivo `Y`, grupo 0. `payload[0x000]` era zero
nas capturas porque é o byte alto do passo do contato, e a base de `X` é `0x00`.

### Onde a fórmula linear da bancada quebra

As fórmulas candidatas do estado da arte (`payload[0x001] ≈ 0x0F + n`) coincidem com a
fórmula do encoder de `n = 1` a `n = 8` e divergem a partir de `X0009`, porque
`bit = (n-1) & 7` reinicia:

| `n` | linear | encoder |
|---:|---:|---:|
| 8 | `0x17` | `0x17` |
| 9 | `0x18` | `0x10` |
| 10 | `0x19` | `0x11` |

Uma única captura com `X0009` decide entre os dois modelos.

## Decodificador de instruções — recuperado

O laço em `0x4AFEDD` mascara o byte **baixo** de cada passo com `0x78` — descartando os
três bits inferiores, que carregam o índice de bit — e desce uma árvore de subtrações até
o mnemônico:

| byte baixo `& 0x78` | instrução |
|---:|---|
| `0x10` | STR |
| `0x18` | STR NOT |
| `0x20` | AND |
| `0x28` | AND NOT |
| `0x30` | OR |
| `0x38` | OR NOT |
| `0x40` | OUT |
| `0x60` | TMR |
| `0x68` | CNT |

As sete primeiras coincidem com as bases já reconstruídas em
[`Tp02TargetCompiler.cs`](../src/OpenLadderStudio.Core/Tp02TargetCompiler.cs). **`TMR = 0x60`
e `CNT = 0x68` são novas** — não estavam na tabela de bits do encoder.

`AND STR` e `OR STR` são tratadas em outro ramo (`0x4B01EF` e `0x4B0227`), e há uma segunda
tabela, por salto indexado em `0x4AFD44`, para o byte baixo `0x08`–`0x0D`, com os mesmos seis
mnemônicos booleanos sem índice de bit. Essa família não apareceu em nenhuma captura de
bancada e seu papel continua desconhecido.

## Região B — para que serve

Esta é a resposta ao ponto que estava em aberto. Em `0x4B036C`–`0x4B03C6` o PC12 remonta o
número do dispositivo **a partir dos dois planos**:

```text
bits 0-2  <- byte baixo do passo & 0x07
bit  3    <- byte baixo do passo & 0x80
bits 4-6  <- byte da região B & 0x10 / 0x20 / 0x40
depois soma 1        (a numeração exibida é 1-based)
```

Ou seja: **a região B carrega os bits altos do número do dispositivo.** Ela não é o byte
`EXT` do encoder — é um plano de extensão de endereço.

Conferido contra as capturas de bancada:

| captura | byte baixo | região B | número | esperado |
|---|---:|---:|---:|---:|
| X0001 NF | `18` | `09` | 1 | 1 ✓ |
| X0002 NF | `19` | `0A` | 2 | 2 ✓ |
| X0002 NA | `11` | `02` | 2 | 2 ✓ |
| X0001 NA | `10` | `01` | 1 | 1 ✓ |
| Y0002 | `41` | `07` | 2 | 2 ✓ |
| Y0003 | `42` | `08` | 3 | 3 ✓ |

Os seis endereços saem certos. Isso também explica por que as fórmulas lineares da bancada
funcionavam: com `n ≤ 8` os bits altos são todos zero e sobra só `LOW & 7`.

## Palavras fixas e funções F-xx

Se o byte baixo não casa com nenhum opcode booleano, o PC12 o testa **inteiro** contra três
palavras fixas (`0x4B0183`):

| byte baixo | palavra |
|---:|---|
| `0x00` | vazio |
| `0x01` | AND STR |
| `0x02` | OR STR |

As três coincidem com as palavras fixas já reconstruídas do encoder (`NOP = 00 00 00`,
`AND STR = 00 01 00`, `OR STR = 00 02 00`).

Se também não casar, o laço chama o decodificador de funções em `0x4B481C`. **É aqui que o
byte alto do passo é usado:** a rotina o lê em `0x53022F` — isto é, `payload[2i]` — e
despacha por uma tabela de saltos de 72 entradas em `0x4B4884`, com 64 handlers distintos.

### O byte alto é o número da função

Cruzando essa tabela com
[`tp02_function_map_normalized.csv`](data/tp02_function_map_normalized.csv), que já estava no
repositório:

- 64 índices da tabela têm handler próprio;
- o mapa tem exatamente 64 números de função (`b0`);
- os 8 índices sem handler — `0x1C`, `0x1D`, `0x23`–`0x26`, `0x45`, `0x46` — **não têm função
  no mapa**;
- nenhum handler sem função, nenhuma função sem handler.

A coincidência é exata nos dois sentidos. Portanto o byte alto de cada passo é o **número da
função F-xx**, e o mapa de funções extraído do lado do encoder descreve o mesmo conjunto que
o decodificador reconhece.

### Fim de programa

O índice `0x00` dessa tabela é a função `End`. **É assim que o programa termina** — não por
um comprimento, mas por uma instrução de fim no fluxo.

## O que continua em aberto

- **Bits 0-3 e 7 da região B.** Os bits 4-6 são endereço; os demais não são consumidos por
  este caminho. Os valores observados (`01`, `02`, `09`, `0A` para X; `07`, `08` para Y)
  variam, então carregam algo — mas o quê, continua desconhecido.
- **A classe do dispositivo (X/Y/C) nas instruções booleanas.** O byte alto do passo é lido
  em exatamente quatro pontos do binário, todos dentro do decodificador de funções — nunca
  no caminho booleano, que só toca os índices ímpares e a região B. Os fluxos A e B remontam
  o endereço com código byte a byte idêntico, e nenhum dos dois lê a classe por passo. A
  bancada observou `payload[0x000] = 0x00` e `payload[0x002] = 0x20`, coerentes com as bases
  de `X` e `Y`, mas de onde o PC12 tira a letra ao montar um contato ou bobina segue sem
  resposta.
- **A família de opcodes `0x08`–`0x0D`.** Mesmos mnemônicos booleanos, sem índice de bit.
  Nenhuma captura de bancada caiu nela.
- **Fluxo B** (`0x004B0F94`) abre um diálogo antes de transmitir; a emulação atual para no
  preflight `F0` porque os stubs de OWL não preservam a pilha nas chamadas de membro.

## Como reproduzir

```bash
pip install unicorn
python3 scripts/emulate_pc12_readprog.py --so-geometria   # só a conferência estática
python3 scripts/emulate_pc12_readprog.py                  # com emulação dos fluxos
```

A ferramenta confere cada afirmação de geometria contra os bytes do `pc12.exe` antes de
imprimir qualquer conclusão. Se o binário mudar, ela falha em vez de repetir uma conclusão
antiga.

## Segurança

Toda esta investigação é estática e emulada. Nenhum quadro foi transmitido a um PLC real,
nenhuma porta serial foi aberta e nenhum comando de escrita, RUN/STOP remoto, download,
apagamento ou firmware foi exercitado. `0F 00 F0` continua bloqueado no PG Lab.

Conhecimento recuperado do binário **não equivale a comportamento validado em hardware**.
Para leitura, o modelo estático entra como hipótese forte a ser confirmada em bancada; para
escrita, a política do projeto permanece inalterada.
