# Campanha de validação da escrita PG do WEG TP02

Objetivo: validar no PC12 e, por último, no TP02 físico a sequência de gravação já reconstruída offline, e descobrir separadamente as demais classes de escrita.

## Estado já confirmado offline

`Write Program Data` usa o comando **`0x33`**. O construtor original do PC12 foi executado dentro do Unicorn com a rotina TX interceptada, produzindo frames idênticos ao modelo independente.

Estrutura:

```text
33 | LEN | 00 | START_H | START_L | 2*W | HIGH/LOW... | EXTERNAL... | CHK
LEN = 3*W + 4
1 <= W <= 80
soma(frame) mod 256 = FF
```

O PC12 fecha um bloco normal ao atingir 20 instruções lógicas. Cada instrução ocupa 1..4 machine words, portanto um bloco pode chegar a 80 words.

Depois de sucesso, o próximo bloco começa no cursor real de passos. O PC12 tenta cada frame até três vezes em caso de timeout, erro de checksum ou erro de status.

O quadro `00 00 FF` é aceito pelo validador genérico do PC12 e é usado pelo emulador como **ACK sintético de laboratório**. Isso não prova que seja o ACK físico real do TP02 para `0x33`.

## Regras da campanha

- PC12 original em uma ponta do par COM virtual.
- `OpenLadderTP02Emulator.exe` na outra ponta.
- Não conectar o emulador à COM física do PLC.
- Reiniciar o emulador antes de cada caso para gerar captura RAW independente.
- Marcar somente uma opção de `PLC > Write` por caso.
- Alterar apenas uma variável entre A e B.
- Rodar `AnalyzeLatestTp02Capture.bat` após cada sessão.
- Preservar `*-raw.bin`, `*.frames.csv`, `*-pg33-program.bin` e o log textual.

## Classes de escrita expostas pelo PC12

1. `Write Program Data` — **PG33 já identificado**.
2. `Write System Data` — memória `WSxxx`.
3. `Write Vxxx Data`.
4. `Write Dxxx Data`.
5. `Write WCxxx Data`.
6. `Write FLxxx Data`.

## Matriz atualizada

| ID | Operação | Variação | Objetivo atual |
|---|---|---|---|
| R0 | Read Program Data | nenhuma | controle da sessão de leitura |
| W1A | Write Program Data | programa mínimo | confirmar `0x33` via PC12 real contra emulador |
| W1B | Write Program Data | mudar só o endereço X | validar bytes do machine word |
| W1C | Write Program Data | mudar só o tipo X/Y | validar classe de dispositivo |
| W1D | Write Program Data | >20 instruções | confirmar divisão em múltiplos PG33 |
| W1E | Write Program Data | `--no-pg33-ack` | observar retries/timeout do PC12 |
| W2A/B | Write System Data | 1 WS controlado | descobrir opcode e formato WS |
| W3A/B | Write Vxxx Data | V001=1/2 | opcode/endereço/endianess V |
| W4A/B | Write Dxxx Data | D001=1/2 | opcode/endereço/endianess D |
| W5A/B | Write WCxxx Data | WC001=1/2 | opcode/endereço/endianess WC |
| W6A/B | Write FLxxx Data | FL001=A/B | opcode/formato FL |

## W1A - controle do PG33

Use o programa mínimo cuja codificação reversa já é conhecida:

```text
STR X001  -> 00 10 00
OUT Y001  -> 20 40 00
END       -> 00 70 00
```

No PG33 os planos ficam:

```text
HIGH/LOW: 00 10 20 40 00 70
EXTERNAL: 00 00 00
```

Para `start=0000`, a geometria esperada é:

```text
33 0D 00 00 00 06
00 10 20 40 00 70
00 00 00
CHK
```

O checksum exato depende dos bytes anteriores e deve fechar a soma em `FF`.

O analisador reconstruirá os três machine words e marcará `W1A=True`.

## W1D - paginação

Faça um programa com 21 instruções lógicas simples. O comportamento previsto pelo PC12 reconstruído é:

- primeiro frame: 20 instruções lógicas;
- segundo frame: restante;
- `START` do segundo frame = cursor real de passos após as palavras do primeiro bloco, não simplesmente `START + 20`.

Essa diferença importa quando uma instrução ocupa 2, 3 ou 4 machine words.

## W1E - retry

Execute contra o emulador com:

```bat
StartTp02Emulator.bat COM11 --no-pg33-ack
```

A expectativa reconstruída é até três transmissões do mesmo frame antes da falha. Depois repita com o ACK sintético habilitado; o PC12 deve avançar após a primeira resposta aceita.

## Captura e comparação

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\AnalyzeTp02Capture.ps1 `
  -Path .\tp02-emulator-captures\W1A-raw.bin `
  -Compare .\tp02-emulator-captures\W1B-raw.bin
```

Para frames `0x33`, o analisador mostra diretamente:

- `START`;
- quantidade de machine words;
- words reconstruídos `(HIGH LOW EXTERNAL)`;
- reconhecimento de W1A.

## Estado STOP

A transferência de programa deve ocorrer com o controlador em STOP. No emulador, o handshake padrão continua:

```text
CON-ICB<CR>
<- 80 01 09 75
```

`C0 01 09 35` fica reservado para testes específicos de estado RUN.

## Critério para fechar Write Program Data

Já temos: opcode `0x33`, geometria, endereço inicial, quantidade de words, planos HIGH/LOW e EXTERNAL, checksum, limite de bloco, regra de paginação e retry.

Restam para considerar o caminho físico fechado:

1. capturar a resposta real do TP02 ao `0x33`;
2. confirmar a sequência completa de abertura/conclusão em uma gravação real controlada;
3. confirmar erros/NAK físicos;
4. obter readback/compare consistente entre o programa escrito por PG33 e o lido por `34`.

Somente depois dessa validação o envio PG33 deve ser habilitado contra um TP02 físico pelo OpenLadder.

## Fases posteriores

Depois de `Write Program Data`, estudar contra o emulador as classes WS, V, D, WC e FL e, separadamente, RUN, STOP, Password, EEPROM e operações Clear. Comandos de limpeza permanecem proibidos contra o PLC físico durante a engenharia reversa.
