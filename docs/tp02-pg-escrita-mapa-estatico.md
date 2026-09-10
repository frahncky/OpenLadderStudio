# TP02 PG — mapa estático dos comandos de escrita do PC12

Data: 2026-09-10 · Método: análise estática do `pc12.exe` · **Nenhum byte transmitido a um PLC**

## Aviso de escopo

Este documento **apenas mapeia** o que o `pc12.exe` monta para escrever no TP02. Nada aqui é
transmitido, implementado no OpenLadder Studio ou testado em hardware. A política do projeto
continua estritamente READ-ONLY (seção 21 de [`TP02_PG_ESTADO_DA_ARTE.md`](TP02_PG_ESTADO_DA_ARTE.md)):
`0F 00 F0` bloqueado, nenhuma escrita, download, apagamento, firmware ou RUN/STOP remoto.

O objetivo é uma decisão informada: saber o que a escrita **seria**, sem dar nenhum passo em
direção a executá-la. Habilitar escrita real é uma ampliação consciente do escopo, que exige
decisão explícita do responsável e revisão da política — não é consequência deste mapa.

## Inventário de comandos montados pelo PC12

Varrendo todas as escritas imediatas no buffer de transmissão (`0x4FA7A8`), o primeiro byte
de cada quadro que o `pc12.exe` monta:

| CMD | classe | papel |
|---:|---|---|
| `0xF0` | leitura | preflight/status |
| `0x38` | leitura | metadado do programa |
| `0x34` | leitura | bloco de programa |
| `0x0A` | leitura | memória/registradores |
| `0x14` | leitura | consulta |
| `0x09` | **escrita** | espelho do `0A` |
| `0x0F` | **escrita destrutiva** | Clear All Memory (`0F 00 F0`) |
| `0x01`–`0x04`, `0x11`, `0x12`, `0x13`, `0x33`, `0x35`, `0x37` | — | controle/sessão, não classificados |

## A primitiva de escrita: comando 09

O `09` é o espelho exato do `0A` de leitura. Lado a lado, na mesma família de rotinas
(`0x4BD45E` lê, `0x4BD5FE` escreve o mesmo endereço `0x60xx`):

```text
leitura   0A [n=3] [end_hi] [end_lo] [qtd]              chk
escrita   09 [n=5] [end_hi] [end_lo] [qtd] [d0] [d1]    chk
```

- **byte 1 (`n`)** é o número de bytes que seguem antes do checksum: leitura `3` (endereço +
  quantidade), escrita `5` (endereço + quantidade + os dois bytes de dado);
- **`qtd`** é o número de bytes de dado;
- **checksum** é a mesma regra de todo o protocolo: `0xFF` menos a soma dos bytes anteriores.

Há também escrita de bloco maior — `0x4BC811` monta `09 11 …`, com `n = 0x11` (17 bytes
seguindo), ou seja um bloco de 14 bytes de dado num único quadro.

### Escrita de registradores

A rotina `0x4B7DCE` trata `Write PLC Vxxxx / Dxxxx / WCxxx / FILE Register`, montando quadros
`09` para cada família. Ela tem guardas próprias e reporta `TIME-OUT !`, `Communication Error`
e `CHECK SUM ERROR!` — mesma disciplina de resposta da leitura.

## Comando destrutivo: 0F 00 F0

`0x46F0F0` monta `0F 00 F0` (checksum `0xF0`, TX de 3 bytes) e o transmite. É o **Clear All
Memory**. Continua bloqueado no PG Lab e não há razão para o OpenLadder oferecê-lo.

## O que NÃO está mapeado

- **O laço de download do programa.** O menu `Write PLC Program...` (`0x4B6B08`) abre um
  diálogo (recurso `0xB7`); a rotina que percorre o programa montando quadros `09` em sequência
  **não foi isolada com certeza** nesta passada. A primitiva (`09`) está confirmada; a
  orquestração do download inteiro — handshake, travas, ordem dos blocos — não.
- **A confirmação de que a escrita de programa usa `09`** e não um comando dedicado. É a
  hipótese mais provável (o `09` é a única primitiva de escrita de memória encontrada), mas não
  está provada.

Um log de bancada de um `Write PLC Program...` real fecharia os dois pontos de uma vez — pelo
mesmo método com que o Upload fecharia os pontos abertos da leitura.

## Como reproduzir

```bash
python3 scripts/emulate_pc12_readprog.py --so-geometria
```

A seção "Comandos de escrita (mapa estático)" confere cada afirmação deste documento contra os
bytes do `pc12.exe` e falha com código 1 se divergir. Como todo o resto da ferramenta, é
estática: não transmite nada.

## Segurança

Mapear não é habilitar. Este documento existe para que a decisão sobre escrita seja tomada com
conhecimento do que ela envolve — não para aproximá-la. Enquanto a política READ-ONLY estiver
em vigor, o mapa é referência, não roteiro.
