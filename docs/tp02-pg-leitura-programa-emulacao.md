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

## O que continua em aberto

- **Semântica da região B.** A estrutura está confirmada (1 byte por passo a partir de
  `payload[0x0A0]`), mas os valores observados não correspondem ao byte `EXT` calculado
  pelo encoder — `EXT` seria 0 para grupo 0, e a bancada observou `01`, `02`, `09`, `0A`
  para X e `07`, `08` para Y. É um plano distinto, ainda não decodificado.
- **Tabela de salto do decodificador.** Em `0x004AFD3D` há um `jmp` indexado sobre
  `(payload[2i+1] & 0x7F) - 8`, para valores 8 a 13. Mapear essa tabela dá o decodificador
  de instruções completo.
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
