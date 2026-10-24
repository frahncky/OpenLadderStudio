# TP02 — roteiro de bancada das pendências de firmware (2026-10-24)

Roteiro preparado pelo agente `re-bancada`. **Quem executa é o operador
humano**, com as ferramentas do laboratório. Nenhum quadro deste documento
foi transmitido ao PLC na preparação.

Fonte das perguntas: `docs/TP02_PG_LATEST.md` ("Estado de evidência",
"Ainda pendente fisicamente") e `docs/pc12-v2.1-static-inventory.md`
("Fechamento estático de pendências"). O lado PC12 está resolvido
estaticamente; o que segue mede **o firmware**.

## 0. Regras que valem para todo o roteiro

```text
PLC: WEG TP02-60MR V2.2.4K, desacoplado de máquina e cargas
Serial: 19200 8O1, perfil DTR/RTS que confirmar HELLO STOP e F0 na mesma COM aberta
Checksum: sum(quadro) mod 256 = FF
Proibido em qualquer fase: 0F 00 F0 (Clear All), 01/02 (STOP/RUN remoto), 37 (BIOS),
  04, 11, 12, 13, 09, 35, firmware, apagamento de EEPROM
Escrita (33, 03): seletor físico em STOP, backup 34 antes, TX única por quadro,
  ACK aceito somente = 00 00 FF, readback 34 depois de cada escrita,
  restauração com readback idêntico ao backup byte a byte
Recusa em RUN: HELLO = C0 01 09 35 bloqueia qualquer escrita
Sem retransmissão cega: um quadro de escrita sem resposta clara encerra a fase;
  o próximo passo é só leitura 34
```

Ordem de prioridade: **fases A e B só leem** e respondem a maior parte das
pendências. A fase C escreve e só deve rodar depois de A e B arquivadas.

Legenda de evidência: as respostas "esperadas" vêm do PC12 (estático) e do
emulador; nenhuma delas é fato de firmware até haver captura arquivada.

### Quadros conferidos (offline, 2026-10-24)

```text
HELLO              43 4F 4E 2D 49 43 42 0D        (ASCII "CON-ICB\r")
F0                 F0 00 0F                        soma FF
38                 38 00 C7                        soma FF
34 @0000           34 03 00 00 A0 28               soma FF
34 @0050           34 03 00 50 A0 D8               soma FF
34 @004F           34 03 00 4F A0 D9               soma FF
34 @00A0           34 03 00 A0 A0 88               soma FF
34 @00F0           34 03 00 F0 A0 38               soma FF
34 @0140           34 03 01 40 A0 E7               soma FF
PG33 sentinela     33 0D 00 00 00 06 00 10 20 40 00 70 00 00 00 D9   soma FF
                   (STR X0001 / OUT Y0001 / END; LEN 0D = 3*3+4; 2W = 06)
PG33 CHK errado    33 0D 00 00 00 06 00 10 20 40 00 70 00 00 00 DA   soma 00 (proposital)
Clear Program      03 00 FC                        soma FF
```

## Fase 0 — antes de ir à bancada (sem PLC)

**P0.1 — a pendência 1 pode já estar respondida.** No ciclo de 2026-09-11 a
PG Lab 1.22 gravou a sentinela de 3 palavras **sem** `03 00 FC` sobre o
programa de 23 passos, e leu `34 @0000` depois. Se o RX bruto desse readback
existir (log da sessão, pasta da PG Lab), os pares 3..22 da Região A e os BRAW
3..22 da Região B respondem à pergunta sem nova escrita:

```text
pares 3..22 = backup original     -> firmware NÃO limpa; palavras antigas sobram após o END
pares 3..22 = 00 00 (ou FF FF)    -> firmware limpa/zera além do END (ou o 33 apaga a página)
```

Ação do operador: localizar e copiar o log de 2026-09-11 para `docs/data/`.
Se existir, a fase C1 é dispensada.

**P0.2 — ajustes de ferramenta: RESOLVIDO (driver-plc, 2026-10-24).** A
allowlist READ-ONLY de `TP02PhysicalValidator.cs` aceita `34` somente nos
quadros exatos de uma lista fechada: START `0000`, `0050` e, para B2/B3,
`34 03 00 4F A0 D9`, `34 03 00 A0 A0 88`, `34 03 00 F0 A0 38`,
`34 03 01 40 A0 E7`. Qualquer outro START (ex.: `004E`, `0051`) é recusado.
Depois de HELLO RUN (`C0 01 09 35`) o validador transmite somente `F0 00 0F`
(fase A3) e encerra a campanha; `33`, `03`, `14`, `34`, `0A`, `38` e
`0F 00 F0` ficam bloqueados em RUN. ACK exato, TX única e o bloqueio de
`0F 00 F0` não mudaram. Coberto por `OpenLadderTP02PhysicalValidatorSafetyTest`.

**P0.3 — programas de teste** carregados pelo **PC12 original** (Write PLC
Program, que já envia `03 00 FC` + `33`), não pelo OpenLadder:

```text
PA  programa canônico de 23 passos / END=0022 (backup de referência)
PB  2 passos:  STR X0001 / END                      -> 38 esperado em A2
PC  91 passos: 18 rungs F-13w ADD (docs/tp02-pg-pagination-34-v119.md)
PD  323 passos: GenerateTp02Pg34BoundaryProject.ps1 (só X0001 e C0001, sem Y)
PE  programa com instrução de 2..4 palavras cruzando o slot 79/80
    (ex.: 77 palavras simples + F-13w ADD de 4 palavras começando no passo 77)
```

Cada carga pelo PC12 deve ser capturada em ponte com
`BuildTp02FullCapture.bat` (isso arquiva, de graça, `03 00 FC`, os blocos `33`
e os ACK físicos do firmware — pendências "ACK 33" e "handshake no fio").

## Fase A — identificação e metadados (só leitura)

### A1. Sessão base

- Pergunta: perfil serial e F0 continuam iguais; referência para o resto.
- Pré-condição: seletor em STOP, programa PA.
- TX: `HELLO`, depois `F0 00 0F`.
- Esperado: `80 01 09 75`; `00 02 10 22 CB`.
- Interpretação: igual ao arquivado = sessão válida.
- Parada: HELLO `C0...` (RUN) ou F0 diferente → encerrar e registrar.

### A2. Generalidade de `38 = 2*(N-1)` (pendência 3)

- Pré-condição: A1 aprovado; repetir para PA, PB, PC, PD, PE.
- TX: `38 00 C7`.
- Esperado pela hipótese (`00 02 HI LO CHK`):

```text
PA 23 passos  -> 00 02 00 2C D1
PB 2 passos   -> 00 02 00 02 FB
PC 91 passos  -> 00 02 00 B4 49   (2*90 = B4)   ou 00 02 00 9E 5F se limitado a 80
PD 323 passos -> 00 02 02 84 77   (2*322 = 284) ou 00 02 00 9E 5F se limitado a 80
```

- Interpretação: o PC12 usa `HI<<8|LO` como limite do laço de leitura em
  palavras. Para PC/PD, o valor distingue "total do programa" de "limitado à
  página"; o emulador hoje modela o limitado (`--pg38=total16` é a alternativa).
- Parada: resposta sem soma FF ou LEN != 02 → registrar e seguir só com 34.

### A3. F0 com bit6 (RUN) — pendência 5, parte RUN

- Pré-condição: P0.2 aplicado; programa PB ou PD (sem saída Y); cargas
  desligadas. O operador muda o **seletor físico** para RUN. Nenhum RUN remoto.
- TX: `HELLO`, `F0 00 0F`. **Nada mais** nesta fase.
- Esperado: HELLO `C0 01 09 35`; F0 `40 02 10 22 8B` (bit6 em S).
- Alternativas: `00 02 10 22 CB` (bit6 não reflete RUN no F0), silêncio ou NAK
  (S com bit7).
- Parada: qualquer resposta registrada encerra a fase; seletor de volta a STOP
  e conferir HELLO `80 01 09 75` antes de seguir.

### A4. F0 com bit5 (senha) — pendência 5, parte senha — OPCIONAL

Ver "Itens inseguros". Só com senha definida e removida pelo **PC12**, senha
anotada em papel. TX `F0 00 0F`; esperado `20 02 10 22 AB`.

## Fase B — paginação do 34 (só leitura)

### B1. Paginação >80 passos (pendências 6 e 7)

- Pré-condição: A1/A2 aprovados com PC, depois PD.
- TX em sequência, uma vez cada: `34 @0000`, `34 @0050`, `34 @00A0`,
  `34 @00F0`, `34 @0140` (as três últimas só com PD e P0.2 aplicado).
- Esperado: `00 F0 <160 bytes HIGH/LOW> <80 BRAW> CHK`, cada página = 80
  palavras a partir do START; END `00 70` em 0x5A (PC) / 0x142 (PD); BRAW
  = soma dos nibbles & 0F.
- Interpretação: comparar com "Read PLC Program" do PC12 da mesma carga (em
  ponte). Igual = paginação por palavras confirmada.
- Parada: página sem soma FF, LEN != F0 ou silêncio → não repetir; parar B1.

### B2. START fora de múltiplo de 0x50 (pendência 4)

- Pré-condição: PE carregado; B1 aprovado; P0.2 aplicado.
- TX: `34 03 00 4F A0 D9`, uma vez.
- Esperado (modelo por palavra): página começando na palavra 0x4F; par 0 =
  par 79 da página `@0000`, pares 1..79 = pares 0..78 da página `@0050`.
- Alternativas e leitura:

```text
página alinhada a 0x50 ou a 0x00   -> firmware arredonda o START
NAK (S bit7, código em rx[2])       -> firmware exige múltiplo de 80; anotar o código
silêncio                            -> START inválido descartado
```

- Parada: qualquer uma das alternativas encerra a fase (sem retry).

## Fase C — escrita controlada (só depois de A e B)

Modelo único (`TP02_PG_LATEST.md`, "Protocolo do próximo teste físico"):
HELLO STOP → F0 → 38 → backup `34 @0000` (salvo e validado) → quadro de teste
(TX única) → ACK exato `00 00 FF` → readback `34` → restauração (TX única) →
ACK `00 00 FF` → readback = backup byte a byte. Programa base: PA.

Restauração: o PG33 de 23 palavras gerado pela PG Lab 1.22 a partir do
backup, já validado em 2026-09-11 (ACK `00 00 FF`). Plano B: PC12 "Write PLC
Program" com o projeto PA.

### C1. Firmware exige `03 00 FC`? (pendência 1) — só se P0.1 não resolveu

- Pré-condição: PA carregado, STOP, backup salvo.
- TX única: `33 0D 00 00 00 06 00 10 20 40 00 70 00 00 00 D9` (sem 03 antes).
- Esperado: `00 00 FF`.
- Readback `34 @0000`: pares 0..2 = `00 10 20 40 00 70`; **olhar pares 3..22**.
  Interpretação idêntica a P0.1. `38` depois do readback também é informativo
  (se responder `00 2C`, o firmware mede o END antigo).
- Restaurar e conferir readback = backup.
- Parada: ACK diferente de `00 00 FF` ou silêncio → não restaurar às cegas;
  ler `34 @0000` e decidir com o operador.

### C2. NAK do 33 por checksum inválido (pendência 2, variante segura)

- Pré-condição: PA carregado, STOP, backup salvo.
- TX única: `33 0D 00 00 00 06 00 10 20 40 00 70 00 00 00 DA` (soma != FF).
- Esperado: silêncio ou NAK `8x ..` com código em rx[2] (candidatos 0D/10/11/19).
- Interpretação: o conteúdo é a sentinela já validada; se o firmware ignorar o
  checksum e gravar, o readback mostra a sentinela e a restauração padrão
  resolve. Readback `34 @0000` obrigatório nos dois casos.
- Parada: ACK `00 00 FF` → firmware não valida soma; restaurar e encerrar a
  fase C.

### C3. `03 00 FC` + 33 (comportamento do PC12) — OPCIONAL

Só se C1 mostrar resíduo e houver interesse em replicar o PC12 no OpenLadder.
Exige liberação de `03` na PG Lab (`driver-plc`). Sequência: backup → `03 00 FC`
(TX única, ACK `00 00 FF`) → readback `34` (esperado programa vazio) →
restauração de 23 palavras → readback = backup. Risco maior: entre o `03` e a
restauração o PLC fica sem programa. A captura em ponte do PC12 em P0.3 já
mostra o ACK físico do `03` sem este passo.

## Itens inseguros ou não recomendados

```text
NAK 12 "Can not program while PLC is running": exige mandar 33 com o PLC em RUN,
  o que viola a regra de recusa em RUN. NÃO testar.
NAK 1A "Password incorrect" e F0 bit5 (A4): exigem gravar senha no PLC; risco de
  bloqueio se a senha se perder. Só pelo PC12, senha anotada, removida no fim.
NAK por START fora da memória (33 em 0FA0+) ou LEN inconsistente: o firmware
  pode gravar fora da área de programa ou interpretar parcialmente. NÃO testar.
C3 (03 00 FC): janela sem programa; preferir a captura em ponte do PC12.
A3 (F0 em RUN): o programa roda de fato; só com saídas desconectadas e
  programa sem Y (PB/PD).
```

## Checklist do operador

```text
[ ] P0.1 log de 2026-09-11 localizado e copiado para docs/data/ (ou ausente)
[ ] P0.2 allowlist de laboratório ajustada pelo driver-plc (34 004F/00A0/00F0/0140; F0 em RUN)
[ ] P0.3 cargas PB, PC, PD, PE feitas pelo PC12 com captura em ponte arquivada
[ ] PLC desacoplado; seletor em STOP; cabo TP-232PG validado; COM exclusiva
[ ] A1 HELLO 80 01 09 75 / F0 00 02 10 22 CB
[ ] A2 38 registrado para PA, PB, PC, PD, PE
[ ] A3 F0 em RUN registrado; seletor de volta a STOP; HELLO STOP reconfirmado
[ ] B1 páginas 34 de PC e PD arquivadas e comparadas com o PC12
[ ] B2 34 @004F registrado (página / NAK / silêncio)
[ ] C1 (se necessário) backup, ACK, readback, restauração, readback = backup
[ ] C2 backup, resposta ao CHK errado, readback, restauração se preciso
[ ] PLC final em STOP com programa PA confirmado no PC12
[ ] todos os RX brutos e logs copiados para docs/data/
```

## Conferência offline feita na preparação

- `RunTp02OfflineLab.bat` (compila o emulador e roda `--self-test-all`, sem
  COM): saída 0, `TP02 emulator full matrix: PASS`, `checks=144946`,
  `hardware=NÃO UTILIZADO / COM=NÃO ABERTA`.
- Script de verificação local (PowerShell, fora do repositório): todos os
  quadros acima com soma FF, exceto o de C2, propositalmente errado; nenhum
  opcode proibido; PG33 com LEN/2W coerentes. A allowlist READ-ONLY atual
  aceita F0, 38, 34 @0000 e @0050; recusa @004F/@00A0/@00F0/@0140, o 33 e o 03
  (daí o P0.2).
- Os quadros não passaram pelo emulador via COM virtual (isso exige um par
  com0com). O emulador grava o 33 sem limpar as palavras seguintes e lê o 34
  por palavra a partir de qualquer START. Essas são as hipóteses que C1 e B2
  vão confirmar ou derrubar.

## Programas, ensaio offline e analisador de captura (re-emulacao, 2026-10-24)

### Programas PB, PC, PD e PE

Ficam em `docs/data/tp02-bancada-2026-10-26/`, em `.pladder` e com as palavras
esperadas em `.tp02.hex`. O `README.md` traz a listagem para digitar no PC12,
o END, o 38 previsto ("total" e "limitado") e o checksum de cada página 34.
O formato nativo `.PLC` do PC12 não foi reconstruído e por isso não foi gerado.
Duas diferenças em relação ao P0.3: o PE tem 82 passos e usa `F-23 SET C0040`
de 2 palavras nos slots 79/80. Com isso o PC12 lê `34 @004F`; o F-13w no passo
77 levaria a `004D`, fora da allowlist. O 38 do PE vale `00 02 00 A2 5B` (total)
ou `00 02 00 9E 5F` (limitado).

### Ensaio offline (sem COM)

```text
cd src\OpenLadderStudio.Desktop
RunTp02BancadaEnsaio.bat [pasta-de-saída]
```

Para cada fase A1, A2 (PB/PC/PD/PE, 38 "página" e "total16"), A3 (F0 em RUN
bit6/stop/silent/nak), B1 (PC e PD) e B2 (PE, `004F` nos modos
word/align50/align00/nak/silent), o ensaio passa cada quadro pela allowlist
real do validador (`TP02PhysicalValidationSafety.IsAllowed`). O que ela libera
é respondido em processo pelo emulador. Em A3, o ensaio tenta ainda 38, 34,
HELLO, 33, `03`, `0F 00 F0` e `02` com o PLC em RUN: o validador barra os sete
e só HELLO e F0 saem. Em B2, `34 @004E` também é barrado. Ao final o
analisador roda sobre os logs. Isto **não** substitui o ensaio por COM virtual
(ver pendências).

### Analisador de captura

```text
powershell -ExecutionPolicy Bypass -File AnalyzeTp02BancadaFirmware.ps1 -Path <log ou pasta> -Programa PC -Out relatorio.txt
powershell -ExecutionPolicy Bypass -File AnalyzeTp02BancadaFirmware.ps1 -SelfTest [-EnsaioDir <pasta do ensaio>]
```

Lê os logs do validador físico (`session.log`), da captura completa ou em ponte
(`session.log` e `protocol-report.txt`), da PG Lab, do emulador TP02 e os
arquivos chave=valor da sessão física (`*_TX=` / `*_RX=`). Cada resposta é
classificada como `ESPERADA`, `ALTERNATIVA PREVISTA` (com a alternativa) ou
`INESPERADA`, e o relatório sai agrupado por pendência:

```text
A1         HELLO 80 01 09 75 e F0 00 02 10 22 CB
Pend. 3    38 = 2*(N-1)                      alternativa: limitado à página (00 9E)
Pend. 5    F0 em RUN = 40 02 10 22 8B         alternativas: 00 02 10 22 CB, silêncio, NAK
Pend. 6/7  páginas 34 iguais ao modelo        alternativa: resíduo depois do END; BRAW errado = inesperada
Pend. 4    34 @004F por palavra               alternativas: arredonda p/ 0050 ou 0000, NAK, silêncio
Pend. 1/2  C1 ACK 00 00 FF; C2 silêncio/NAK   alternativa C2: ACK (firmware não valida a soma)
Segurança  qualquer quadro além de F0 depois de HELLO RUN = INESPERADA (violação)
```

`-Programa` diz qual programa estava carregado (PA..PE); sem ele, o
analisador tenta o nome do arquivo. Sem programa, o B2 usa as páginas `@0000`
e `@0050` do mesmo log como referência. Um TX sem linha de RX no log aparece
como "RX bruto ausente", que é diferente de silêncio. A saída é 3 quando há
alguma `INESPERADA`.

## Atualização: campanha B1/B2 no validador (2026-10-24)

O validador físico ganhou duas caixas na tela, desmarcadas por padrão. Sem marcá-las, a campanha é a mesma de antes.

- **B1:** `34 @00A0`, `@00F0` e `@0140`. Cada página só é pedida se a anterior (a partir de `@0050`) veio válida e sem END. Silêncio ou página inválida encerra a fase. O gatilho não usa o `38`, porque a regra `2*(N-1)` ainda está em aberto.
- **B2:** `34 @004F` uma vez, só se `@0000` e `@0050` vieram válidas.

Rótulos no `session.log`: `PG34-00A0`, `PG34-00F0`, `PG34-0140` e `PG34-004F`. As leituras alinhadas aparecem como `PG34-P0` e `PG34-P80`. Todos estão no formato que o `AnalyzeTp02BancadaFirmware.ps1` lê. Em RUN continua saindo só o `F0`.
