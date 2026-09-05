# TP02 PG — inventário de quadros extraído do código do PC12

Data: 2026-09-04

Complementa `TP02_PG_READONLY_STATIC_ANALYSIS.md`. Ferramenta:
`scripts/extract_pc12_frames.py`. Apenas leitura do binário; nada é executado nem alterado.

## Método

A rotina de transmissão em `0x46F5E6` chama `WriteFile` com o buffer em `0x4FA7A8` e o
comprimento lido de `0x4FA8AC`. Confirmado por desmontagem: a chamada em `0x46F663` resolve,
pela IAT, para `WriteFile`.

Logo, todo quadro transmitido é montado por escritas imediatas nesses endereços absolutos:

```text
mov byte  ptr [0x4FA7A8 + n], imm8    ->  C6 05 <addr32> <imm8>
mov word  ptr [0x4FA7A8 + n], imm16   ->  66 C7 05 <addr32> <imm16>
mov dword ptr [0x4FA8AC],     imm32   ->  C7 05 <addr32> <imm32>
```

Varrer a seção `.text` atrás desses padrões recupera os quadros mecanicamente, sem
adivinhação. Foram encontradas **289 escritas**, agrupadas em **46 sítios** de montagem.

## Formato do quadro — derivado, não suposto

```text
CMD  LEN  payload[LEN]  CHECKSUM
```

- `CMD` — byte de comando;
- `LEN` — número de bytes de payload;
- `CHECKSUM` — fecha a soma de todos os bytes em `0xFF` módulo 256.

O comprimento total é sempre `LEN + 3`, e o valor gravado em `0x4FA8AC` confere com isso em
todos os sítios em que ele aparece:

| Quadro | LEN | Total esperado | Comprimento TX observado |
|---|---:|---:|---:|
| `01 00 FE` | 0 | 3 | 3 |
| `14 00 EB` | 0 | 3 | 3 |
| `38 00 C7` | 0 | 3 | 3 |
| `13 00 EC` | 0 | 3 | 3 |
| `37 02 FF FF C8` | 2 | 5 | 5 |
| `34 03 00 00 A0 28` | 3 | 6 | 6 |
| `0A 03 53 F9 0E` + checksum | 3 | 6 | 6 |
| `09 05 60 2A 02 00 00` + checksum | 5 | 8 | 8 |
| `09 11 53 F9 0E ...` | 17 | 20 | — |

Essa regra explica por que `38 00 C7` não aparece como bytes contíguos no arquivo: ele é
montado por três instruções separadas. A afirmação anterior de que o quadro é construído
explicitamente no código fica **confirmada**.

## Comandos observados

Opcodes que aparecem em posição de `CMD`:

| CMD | LEN visto | Sítios | Contexto de string na função |
|---|---|---:|---|
| `01` | 0 | 1 | — |
| `09` | 4, 5, 17 | 8 | password, checksum, timeout |
| `0A` | 3 | 14 | password, checksum, timeout |
| `13` | 0 | 1 | password, checksum, timeout |
| `14` | 0 | 6 | `PassWord Message`, `PassWord Error` |
| `33` | — | 1 | — |
| `34` | 3 | 2 | **`Compare PLC Program...`** |
| `35` | 3 | 1 | — |
| `37` | 2 | 2 | **`Please BIOS Refresh Repeat Again !`** |
| `38` | 0 | 2 | `This Function Need Password !` |
| `F0` | 0 | 1 | — |

`0A` e `09` são os mais frequentes e carregam endereço no payload; são os candidatos naturais
a leitura e escrita de área de memória. Isso é **inferência**, não fato observado.

## Limite importante deste método

A varredura só encontra quadros montados por **escrita imediata**. Quadros copiados de uma
cadeia ou de uma tabela não aparecem.

O caso conhecido é o próprio handshake: `CON-ICB` está armazenado como cadeia e é copiado para
o buffer, com `0D` acrescentado em execução. Por isso ele **não** consta da lista acima.
Concluir que a lista é o conjunto completo de comandos seria erro: ela é o conjunto dos
comandos com bytes fixos no código.

## O que continua desconhecido

- o significado da maioria dos opcodes;
- o formato das respostas por comando;
- a codificação de endereço dentro do payload;
- quantos comandos existem fora do alcance deste método.

## Segurança

Dois pontos concretos:

- `37` aparece junto de `Please BIOS Refresh Repeat Again !`. Comando ligado a atualização de
  firmware não deve ser transmitido em ensaio.
- O comando de apagamento de memória já identificado em trabalho anterior, `0F 00 F0`,
  permanece bloqueado e não deve ser reabilitado.

Enviar opcode de efeito desconhecido a um PLC vivo pode apagar programa, alterar saídas ou
corromper firmware. Toda transmissão nova deve passar pela mesma trava de segurança já usada
no laboratório PG.
