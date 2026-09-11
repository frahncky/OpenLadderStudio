# Emulador PG do WEG TP02

Ferramenta para fechar o protocolo de programação do TP02 usando o PC12 original como gerador de tráfego.

## Objetivo

O emulador responde aos quadros PG já confirmados e registra integralmente qualquer comando ainda desconhecido enviado pelo PC12. Isso permite descobrir a sequência de gravação sem transmitir comandos experimentais ao PLC físico.

O protocolo PG observado até agora usa `19200 8O1`, DTR ligado e RTS desligado. Os quadros binários confirmados seguem a regra de checksum em que a soma módulo 256 do frame completo resulta em `FF`.

## Ligação

Use um par de portas COM virtuais, por exemplo:

- PC12 original: `COM10`
- `OpenLadderTP02Emulator.exe`: `COM11`

As duas portas devem formar um par null-modem virtual. Não use no emulador a COM física ligada ao TP02.

## Compilar

Execute:

```bat
BuildTp02Emulator.bat
```

O build aplica automaticamente as regras externas de `TP02PgEmulatorRules.txt` e cria:

```text
OpenLadderTP02Emulator.exe
```

## Executar

Modo interativo:

```bat
StartTp02Emulator.bat
```

Ou informando a porta:

```bat
StartTp02Emulator.bat COM11
```

Opções:

```text
--auto-ack       responde 00 00 FF a comandos desconhecidos (padrão)
--no-auto-ack    apenas captura comandos desconhecidos, sem responder
--hello=80       responde 80 01 09 75 ao CON-ICB<CR> (padrão)
--hello=c0       responde C0 01 09 35 ao CON-ICB<CR>
--fast           remove os atrasos aproximados do TP02 real
```

## Quadros já emulados

```text
CON-ICB<CR> -> 80 01 09 75   (ou C0 01 09 35)
F0 00 0F    -> 00 02 10 22 CB
38 00 C7    -> 00 02 00 0A F3
34 ...       -> 00 F0 + 240 bytes + checksum
0A ...       -> 00 LEN + dados de memória + checksum
14 00 EB     -> 00 00 FF
```

`14` continua classificado apenas como consulta auxiliar; sua semântica ainda não deve ser presumida.

## Regras externas adaptativas

Depois que um novo opcode for descoberto, não é necessário editar `TP02PgEmulator.cs`.

As respostas adicionais ficam em:

```text
TP02PgEmulatorRules.txt
```

Formato:

```text
CMD_HEX|DELAY_MS|RESPONSE_HEX|LABEL
```

Exemplo hipotético, somente depois de `A1` aparecer em captura real:

```text
A1|150|00 00 FF|ACK observado para A1
```

Também é possível adicionar uma regra validada pelo helper:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\AddTp02EmulatorRule.ps1 -Cmd A1 -Response "00 00 FF" -DelayMs 150 -Label "ACK observado"
```

O helper exige que a resposta feche a soma módulo 256 em `FF`. Depois execute novamente:

```bat
BuildTp02Emulator.bat
```

`PrepareTp02EmulatorRules.ps1` injeta as regras no fonte temporário de build, mantendo o fonte principal intacto. Os comandos nativos `F0`, `38`, `34`, `0A` e `14` não podem ser sobrescritos pelo arquivo de regras.

## Captura bruta

Além do log textual, o emulador grava o fluxo bruto recebido do PC12 em:

```text
tp02-emulator-captures\TP02-Emulator-AAAAMMDD-HHMMSS-raw.bin
```

A captura RAW é importante porque preserva bytes mesmo se um futuro comando de escrita usar estrutura diferente da atualmente conhecida.

## Analisador automático

Depois de uma sessão, execute:

```bat
AnalyzeLatestTp02Capture.bat
```

Ou diretamente:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\AnalyzeTp02Capture.ps1 -Path .\tp02-emulator-captures\captura-raw.bin
```

O analisador:

- detecta `CON-ICB<CR>`;
- procura frames no formato `[CMD][LEN][PAYLOAD][CHECKSUM]`;
- aceita somente candidatos cuja soma módulo 256 seja `FF`;
- identifica `F0`, `38`, `34`, `0A` e `14`;
- destaca opcodes desconhecidos;
- gera um arquivo `.frames.csv` ao lado da captura.

Para comparar duas gravações:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\AnalyzeTp02Capture.ps1 -Path captura-A-raw.bin -Compare captura-B-raw.bin
```

Isso mostra exatamente quais frames mudaram entre duas operações.

## Campanha para descobrir a escrita

Use sempre um projeto mínimo e altere apenas uma variável por teste. A matriz completa está em `docs/TP02-PG-WRITE-CAMPAIGN.md`.

### Teste 0 - controle

1. Inicie o emulador em `COM11`.
2. Configure o PC12 em `COM10`.
3. Faça `Read` no PC12.
4. Confirme no log a sequência conhecida, tipicamente `CON-ICB -> F0 -> 38 -> 34/0A`.

Se a leitura não passar pelo emulador, não avance para a escrita: primeiro corrija COM virtual, parâmetros ou estado do handshake.

### Teste 1 - primeira escrita

1. Reinicie o emulador para gerar uma captura limpa.
2. Abra no PC12 um programa mínimo.
3. Marque somente `Write Program Data`.
4. Execute `Write` para o PLC emulado.
5. Rode `AnalyzeLatestTp02Capture.bat`.
6. Anote o primeiro `CMD=0xXX` classificado como `DESCONHECIDO`.

Esse opcode passa a ser o principal candidato ao início do download. Se o ACK genérico não fizer o PC12 avançar, capture o comportamento com `--no-auto-ack`, determine a resposta necessária e registre-a em `TP02PgEmulatorRules.txt`.

### Teste 2 - endereço de operando

Faça duas gravações alterando somente um operando, por exemplo:

```text
A: contato X000
B: contato X001
```

Compare as duas capturas. Os bytes que mudarem no mesmo frame são candidatos à codificação de endereço.

### Teste 3 - tipo de dispositivo

Repita mantendo a estrutura e trocando somente o tipo:

```text
X000
Y000
M000
C000
T000
D000
```

Isso ajuda a separar opcode da instrução, classe de dispositivo e endereço.

### Teste 4 - constante

Compare duas escritas alterando apenas um valor numérico, por exemplo `K1` para `K2`, depois `K255` para `K256`. Isso permite determinar largura, endianess e possíveis campos BCD/binários.

### Teste 5 - tamanho do programa

Grave projetos com 1, 2 e 3 rungs, mantendo instruções simples. Compare quantidade de frames, comprimentos e bytes de término. O objetivo é localizar tamanho total, paginação, END e confirmação final.

### Teste 6 - ACK real

O modo padrão responde `00 00 FF` para comandos desconhecidos somente dentro do emulador. Se o PC12 parar ou acusar erro, repita a mesma sessão com:

```bat
StartTp02Emulator.bat COM11 --no-auto-ack
```

Assim conseguimos distinguir entre:

- comando que não exige resposta;
- comando que exige ACK específico;
- comando cuja resposta depende de estado ou payload.

Quando a resposta correta for conhecida, registre-a pela camada de regras externas e repita a sessão. Isso permite avançar comando a comando sem alterar o motor principal.

## Operações adicionais do PC12

O PC12 possui operações separadas de leitura, escrita, RUN, STOP, EEPROM e limpeza de áreas de memória. No menu Write, as classes são separadas em Program Data, System/WS, V, D, WC e FL. Elas devem ser estudadas individualmente contra o emulador. Não use comandos de limpeza, gravação experimental ou RUN/STOP contra o PLC físico até que os respectivos frames e respostas estejam identificados.

`0F 00 F0` permanece tratado como candidato destrutivo associado a limpeza de memória e não deve ser enviado ao equipamento físico durante esta fase.

## Resultado esperado

A meta desta campanha é transformar cada operação do PC12 em uma sequência determinística documentada:

```text
handshake
preflight
inicio de download
paginas/blocos de programa
metadados
confirmacao/finalizacao
```

Depois que a sequência de `Write` estiver fechada no emulador, ela pode ser implementada no OpenLadder e somente então validada de forma controlada em um TP02 real.
