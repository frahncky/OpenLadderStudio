# TP02 PG — varredura completa em uma execução

Data: 2026-09-09 · Pacote `2026.09.09.7` · OpenLadder Studio v0.95

Este documento reúne, em um só lugar, tudo que hoje é necessário para caracterizar o
protocolo PG do WEG TP02 e descreve a **bateria única** que o Teste Único passa a
executar. O objetivo é fechar a fase de leitura numa só sessão em vez de acrescentar uma
consulta por versão.

## Como rodar

1. Conecte o TP02 pelo mesmo cabo do PC12.
2. Abra **OpenLadderTP02PgLab.exe**.
3. Escolha a porta COM e pressione **EXECUTAR TESTE ÚNICO TP02**.
4. Ao final, o laboratório grava **um** relatório TXT e **um** JSON com toda a sessão.

Não é preciso rodar F0, 38, leitura de programa e leitura de sistema em execuções
separadas: a bateria inteira roda na mesma porta aberta.

## Camada física confirmada em hardware

- 19200 bit/s, 8 bits, paridade ímpar, 1 stop bit (`19200 8O1`);
- perfil que respondeu em bancada: DTR = ligado, RTS = ligado;
- o TP02 costuma ignorar várias tentativas seguidas antes de responder;
- regra de checksum: soma módulo 256 de todo o quadro fecha em `0xFF`;
- formato do quadro: `CMD LEN payload[LEN] CHECKSUM`, comprimento total `LEN + 3`.

## Sequência de sessão que a bateria reproduz

```text
CON-ICB<CR>            handshake protegido (varre DTR/RTS e tenta 6x por perfil)
    ↓
80 01 09 75            HELLO-STOP  (ou C0 01 09 35 em RUN; 0D 01 09 E8 não classificado)
    ↓
F0 00 0F               matriz pós-handshake: 6 variantes de temporização/RTS na mesma porta
    ↓
00 02 10 22 CB         resposta física confirmada ao F0 (5 bytes, soma = FF)
    ↓
38 00 C7  × 5          só sai depois do F0 validado na mesma sessão
34 03 00 00 A0 28 × 5  Read PLC Program (bloco)
0A 03 60 00 AC E6 × 5  Read PLC System — parte 1
0A 03 60 AC AC 3A × 5  Read PLC System — parte 2
14 00 EB  × 3          ramo de senha observado no PC12
    ↓
escutas passivas       intercaladas entre os blocos e ao final (quadros espontâneos)
```

### Por que repetir cada consulta

Os testes anteriores enviaram 38, 34 e 0A **uma única vez** após o HELLO e receberam
`RX []`. Como o TP02 comprovadamente ignora tentativas seguidas, uma única amostra não
distingue **silêncio real** de **tentativa ignorada**. A bateria repete cada leitura na
mesma sessão (38 ×5, 34 ×5, cada 0A ×5, 14 ×3). Com a taxa de resposta observada, cinco
tentativas dão mais de 90 % de chance de capturar ao menos uma resposta, se ela existir.

### Por que a matriz do F0

A validação do F0 é o que libera o 38. Em uma sessão a bancada achou o HELLO com
`RTS=off`, mas o F0 ficou mudo; em outra, o F0 respondeu com `RTS=on`. A matriz reenvia
**somente** `F0 00 0F` — já `READ_ONLY_VERIFIED` — variando temporização e RTS, e preserva
o estado vencedor para o resto da bateria. Nenhum opcode novo é introduzido.

## Mapa de opcodes recuperado do PC12

Do binário `pc12.exe`, por análise estática e por emulação, com os endereços auditáveis em
[`tp02-pg-frame-inventory.md`](tp02-pg-frame-inventory.md):

| Quadro | Origem no PC12 | Classe no laboratório | Transmite? |
|---|---|---|---|
| `43 4F 4E 2D 49 43 42 0D` | handshake `CON-ICB<CR>` | HANDSHAKE (protegido) | sim |
| `F0 00 0F` | status/preflight da conexão | READ_ONLY_VERIFIED | sim (matriz) |
| `38 00 C7` | preâmbulo de Read/Compare PLC Program | READ_ONLY_CANDIDATE | sim, após F0 |
| `34 03 00 00 A0 28` | Read PLC Program | READ_ONLY_PROBE | sim |
| `0A 03 60 00 AC E6` | Read PLC System | READ_ONLY_PROBE | sim |
| `0A 03 60 AC AC 3A` | Read PLC System | READ_ONLY_PROBE | sim |
| `14 00 EB` | ramo condicional de senha | READ_ONLY_PROBE | sim |
| `0F 00 F0` | **Clear All Memory** | BLOCKED | nunca |
| `37 02 FF FF C8` | associado a `BIOS Refresh` (firmware) | fora da allowlist | nunca |
| `09 05 …`, `09 11 …` | carregam endereço + dado (candidatos a escrita) | fora da allowlist | nunca |
| `01 00 FE`, `02 00 FD`, `03 00 FC`, `04 00 FB`, `11 00 EE`, `13 00 EC` | efeito ainda desconhecido | fora da allowlist | nunca |

A bateria transmite exatamente a coluna "sim". O motor recusa qualquer outro quadro por
dupla trava: a lista interna compilada no executável **e** a `readOnlyAllowlist` do pacote.

## O que a bateria fecha e o que fica em aberto

**Fecha:** se, depois de estabelecido o enlace e validado o F0, as leituras 38/34/0A/14
continuarem em `RX []` mesmo após todas as repetições e escutas passivas, isso passa a ser
**evidência forte de que o problema não é temporização nem tentativa ignorada** — é a
sequência/estado da sessão. A caracterização de "é retentativa ou é sequência?" deixa de
depender de novos ensaios manuais.

**Fica em aberto** (fora desta bateria de propósito):
- o encadeamento em que os bytes de metadado da resposta do 38 parametrizam o 34 — isso
  exige lógica adaptativa, papel do modo **Pesquisa Contínua IA**, não de uma bateria fixa;
- a varredura de endereço/página dos comandos de leitura (`34 …`, `0A 03 60 …`), que
  geraria quadros novos e por isso **não** é disparada automaticamente contra hardware vivo;
- a semântica byte a byte do payload `10 22` da resposta ao F0.

## Segurança

- nenhuma escrita, RUN/STOP remoto, download, apagamento ou firmware é transmitido;
- `0F 00 F0` (Clear All Memory) permanece bloqueado no motor e no pacote;
- opcodes de efeito desconhecido ficam registrados apenas como documentação, nunca como TX;
- toda transmissão continua passando pela mesma trava do Laboratório PG.
