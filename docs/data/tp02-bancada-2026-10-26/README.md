# Programas de teste da bancada de firmware TP02 (PB, PC, PD, PE)

Programas da fase P0.3 do roteiro `docs/tp02-bancada-firmware-2026-10-24.md`.
Foram gerados offline por `src/OpenLadderStudio.Desktop/GenerateTp02BancadaPrograms.ps1`,
sem abrir nenhuma porta COM. **Nenhum deles usa saída física Y.**

## Formato: por que `.pladder` e não o projeto nativo do PC12

O PC12 abre um projeto pelo arquivo principal `.PLC`. Os auxiliares são
`.sys1/.sys2/.cnt/.reg1-3/.sym/.file/.cmt/.typ` (`docs/pc12-ajuda-oficial.md`,
"Arquivos de um projeto PC12"). O layout binário do `.PLC` **não foi reconstruído**.
O repositório não tem leitor nem gravador desse formato: o `TP02BridgeLab` só
procura os arquivos, e o pacote portátil não traz nenhum `.PLC` de exemplo.
Por isso não dá para gerar o arquivo nativo com segurança.

O que este diretório entrega:

| Arquivo | Uso |
|---|---|
| `*.pladder` | projeto OpenLadder (Editor Ladder do OpenLadder Studio) |
| `*.tp02.hex` | palavras de máquina esperadas, `HHLLEE` por linha (saída de `OpenLadderTP02ProjectExport.exe`) |
| `PD-323-passos.expected.txt` | expectativa original do `GenerateTp02Pg34BoundaryProject.ps1` |

Para carregar no PLC pelo **PC12 original** (exigência do roteiro), o operador
digita a listagem abaixo no editor do PC12 (Ladder ou Boolean) e salva um projeto
`.PLC` próprio. Depois de digitar, ele confere com **Read PLC Program** em ponte
(`BuildTp02FullCapture.bat`): as palavras lidas precisam ser iguais ao `*.tp02.hex`.
Se o PC12 gerar outra palavra (ex.: `EXTERNAL` diferente de `00`), vale o que o
PC12 gravou. Registre a divergência; ela não invalida o teste.

O arquivo `.PLC` salvo pelo PC12 pode ser versionado aqui depois da bancada. Ele
seria o primeiro exemplo real do formato nativo e passaria ao `re-estatico`.

## Resumo das previsões

`N` = palavras até o END inclusive. 38 = `00 02 HI LO CHK`, com `HI LO = 2*(N-1)`
(hipótese "total") ou `2*(min(N,80)-1)` = `00 9E` (hipótese "limitado à página";
é o padrão do emulador, e `--pg38=total16` ativa a outra).

| Programa | Palavras | END | 38 "total" (esperado) | 38 "limitado" (alternativa) | STARTs que o PC12 lê |
|---|---|---|---|---|---|
| PA (canônico) | 23 | 0x0016 | `00 02 00 2C D1` | igual | 0000 |
| PB | 2 | 0x0001 | `00 02 00 02 FB` | igual | 0000 |
| PC | 91 | 0x005A | `00 02 00 B4 49` | `00 02 00 9E 5F` | 0000, 0050 |
| PD | 323 | 0x0142 | `00 02 02 84 77` | `00 02 00 9E 5F` | 0000, 0050, 00A0, 00F0, 0140 |
| PE | 82 | 0x0051 | `00 02 00 A2 5B` | `00 02 00 9E 5F` | 0000, **004F** |

Páginas 34 previstas pelo modelo por palavra: 80 pares a partir do START,
BRAW = soma dos nibbles & 0F e zeros depois do END. A coluna CHK é o checksum
do quadro de resposta de 243 bytes:

| Programa | START | Palavras ativas | CHK previsto |
|---|---|---|---|
| PB | 0000 | 2 | `87` |
| PC | 0000 | 80 | `CF` (igual a `docs/tp02-pg-pagination-34-v119.md`) |
| PC | 0050 | 11 | `56` (igual a v119) |
| PD | 0000, 0050, 00A0, 00F0 | 80 cada | `27` |
| PD | 0140 | 3 (END no par local 2) | `FF` |
| PE | 0000 | 80 | `15` |
| PE | 0050 | 2 (`D7 84`, END) | `3D` |
| PE | 004F | 3 (`17 71`, `D7 84`, END) | `B5` |

## PB — 2 passos

```text
0000  00 10 00   STR X0001
0001  00 70 00   F-00 END
```

Ladder: um rung com contato `X0001` ligado direto ao `END`. Se o diagnóstico
do PC12 recusar o STR sem saída ("Stack Over"), troque por `STR X0001 / OUT C0001 / END`
(3 passos). Nesse caso o 38 esperado passa a `00 02 00 04 F9`, valor já arquivado
fisicamente em 2026-09-14.

## PC — 91 passos (18 rungs F-13w ADD)

```text
rung n (n = 1..18):  STR X00nn  /  F-13w ADD D0002, D0001, 10
                     (Xnn) 0D 77 / F0 01 / F0 00 / 80 0A
0090  00 70 00   F-00 END
```

Os passos 00..79 são os rungs 1..16 completos. O passo 80 é `STR X0017` (`02 10`),
e nenhuma instrução cruza a página. O PC12 lê `0000` e `0050`.

## PD — 323 passos

Gerado por `GenerateTp02Pg34BoundaryProject.ps1`: 161 rungs `STR X0001 / OUT C0001`
e o END no passo 322 (0x0142). O detalhe está em `PD-323-passos.expected.txt`.
**Cuidado:** são 161 `OUT C0001`, a mesma bobina. Se o PC12 acusar "Double OUT",
use a variante do emulador `--seed=semantic323` (`OUT C0001..C0161`), que gera as
mesmas 323 palavras com coils distintos e o mesmo 38.

## PE — 82 passos, instrução de 2 palavras nos slots 79/80

```text
0000..0077  39 x (STR X0001 / OUT C0001..C0039)
0078  00 10 00   STR X0001
0079  17 71 00   F-23 SET C0040      <- 1ª palavra no slot 79 (última da página 0)
0080  D7 84 00     operando C0040    <- 2ª palavra no slot 80
0081  00 70 00   F-00 END
```

Pela regra de paginação do PC12 (`docs/pc12-v2.1-static-inventory.md`, item 5),
a instrução de 2 palavras que começa no slot 79 não cabe na página `0000`. O PC12
termina a página em 79 palavras e lê a próxima em `34 03 00 4F A0 D9`. Por isso o
**Read PLC Program do PC12 em ponte já exercita a B2**, sem que o validador
precise enviar o `004F`.

O operando bit `C0040` vem da fórmula do PC12 (`docs/tp02-machine-code-reverse-engineering.md`,
"base C = D0"), ainda não observada fisicamente para C. Se o readback mostrar
outro par, ele também é dado de bancada. O exemplo do roteiro (F-13w começando
no passo 77) leva ao START `004D`, que não está na allowlist do validador. Aqui
a escolha foi o SET de 2 palavras para cair exatamente em `004F`.

## Reprodução

```powershell
cd src/OpenLadderStudio.Desktop
cmd /c BuildTp02WbpWriter.bat          # gera OpenLadderTP02ProjectExport.exe
powershell -ExecutionPolicy Bypass -File .\GenerateTp02BancadaPrograms.ps1 -ExportExe .\OpenLadderTP02ProjectExport.exe
cmd /c RunTp02BancadaEnsaio.bat        # ensaio offline A1/A2/A3/B1/B2 + analisador
```
