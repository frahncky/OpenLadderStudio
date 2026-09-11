# Emulador PG do WEG TP02

Ferramenta de laboratório para reproduzir o protocolo PG do TP02 e usar o PC12 original como gerador de tráfego, sem transmitir comandos experimentais ao PLC físico.

## Estado atual

Perfil serial observado: `19200 8O1`, DTR ligado e RTS desligado. Nos quadros binários confirmados, a soma módulo 256 do frame completo resulta em `FF`.

O comando **`0x33` está confirmado como Write PLC Program / Write Program Data**. A confirmação veio de duas fontes independentes dentro do projeto:

- análise estática da rotina `Write PLC Program` do `pc12.exe` original;
- emulação Unicorn offline do construtor original em `0x004B7958`, com a rotina TX interceptada antes de qualquer I/O.

## Ligação

Use um par null-modem de portas COM virtuais, por exemplo:

- PC12 original: `COM10`;
- `OpenLadderTP02Emulator.exe`: `COM11`.

Não use no emulador a COM física ligada ao TP02.

## Compilar

```bat
BuildTp02Emulator.bat
```

O build aplica `TP02PgEmulatorRules.txt` por meio de `PrepareTp02EmulatorRules.ps1` e cria:

```text
OpenLadderTP02Emulator.exe
```

## Executar

```bat
StartTp02Emulator.bat COM11
```

Opções principais:

```text
--auto-ack       responde 00 00 FF a comandos ainda desconhecidos
--no-auto-ack    apenas captura comandos desconhecidos
--pg33-ack       usa 00 00 FF como ACK SINTETICO do PG33 (padrão)
--no-pg33-ack    captura/decodifica 0x33 sem responder
--hello=80       responde 80 01 09 75 ao CON-ICB<CR> (padrão)
--hello=c0       responde C0 01 09 35 ao CON-ICB<CR>
--fast           remove atrasos aproximados do TP02 real
```

## Quadros nativos

```text
CON-ICB<CR> -> 80 01 09 75   (ou C0 01 09 35)
F0 00 0F    -> 00 02 10 22 CB
38 00 C7    -> 00 02 00 0A F3
34 ...       -> 00 F0 + 240 bytes + checksum
33 ...       -> Write Program Data; decodificado e armazenado pelo emulador
0A ...       -> 00 LEN + dados de memória + checksum
14 00 EB     -> 00 00 FF
```

`14` continua classificado somente como consulta auxiliar; não presumir semântica de senha.

## Estrutura confirmada do PG33

Para `W` palavras de máquina:

```text
33 | LEN | 00 | START_H | START_L | 2*W | HIGH/LOW... | EXTERNAL... | CHK
```

Regras confirmadas:

```text
LEN = 3*W + 4
1 <= W <= 80
START = endereço inicial em passos/palavras de máquina
HIGH/LOW = 2 bytes por word
EXTERNAL = 1 byte por word
soma(frame) mod 256 = FF
```

Exemplos produzidos pelo construtor original do PC12 e confirmados por emulação offline:

```text
W=1, start=0000
33 07 00 00 00 02 00 10 01 B2

W=2, start=0050
33 0A 00 00 50 04 00 10 20 41 01 07 F5
```

O construtor bruto aceita até 80 words. O orquestrador normal do PC12 fecha um bloco ao atingir **20 instruções lógicas**; cada instrução pode ocupar de 1 a 4 words, portanto o máximo teórico de 20 x 4 = 80 words é coerente com o limite do frame.

Depois de um quadro aceito, o próximo bloco começa no **cursor real de passos** calculado pelo encoder. Quando o cursor atinge o tamanho do programa, o PC12 entra no caminho de conclusão.

## ACK do PG33

O PC12 usa um validador genérico de resposta. Emulação offline mostrou que um quadro `00 00 FF` deixa zeradas as flags de timeout, checksum e erro, portanto faz o chamador PG33 avançar no ambiente emulado.

Isso **não prova** que `00 00 FF` seja o ACK físico real do TP02 para `0x33`. Por esse motivo o emulador registra a resposta como:

```text
ACK SINTETICO PG33 (nao confirmado no PLC fisico)
```

O PC12 tenta um quadro PG33 no máximo três vezes quando ocorre timeout, erro de checksum ou erro de status.

## Armazenamento do programa escrito

Ao receber um PG33 válido, o emulador:

1. valida opcode, LEN, `2*W`, endereço, planos e checksum;
2. reconstrói cada word como `(HIGH, LOW, EXTERNAL)`;
3. grava os words por endereço de passo;
4. salva um dump em:

```text
tp02-emulator-captures\TP02-Emulator-AAAAMMDD-HHMMSS-pg33-program.bin
```

Cada word ocupa três bytes no dump: `HIGH LOW EXTERNAL`.

O `34` ainda usa um fixture de leitura capturado e **não** é sintetizado automaticamente a partir desse banco PG33. Fechar essa conversão de ida e volta é uma etapa separada.

## Captura e análise

O fluxo bruto PC12 -> emulador é salvo em:

```text
tp02-emulator-captures\TP02-Emulator-AAAAMMDD-HHMMSS-raw.bin
```

Analise a captura com:

```bat
AnalyzeLatestTp02Capture.bat
```

ou:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\AnalyzeTp02Capture.ps1 -Path captura-raw.bin
```

O analisador agora reconhece `0x33` nativamente, mostra `startStep`, quantidade de words e reconstrói os planos `HIGH/LOW + EXTERNAL`. Ele também identifica o programa mínimo W1A depois da reconstrução dos words, em vez de procurar uma sequência de nove bytes contíguos.

Para comparar duas capturas:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\AnalyzeTp02Capture.ps1 -Path captura-A-raw.bin -Compare captura-B-raw.bin
```

## Programa mínimo W1A

A assinatura reversa usada como controle é:

```text
STR X001  -> 00 10 00
OUT Y001  -> 20 40 00
END       -> 00 70 00
```

No PG33 esses bytes ficam distribuídos em dois planos:

```text
HIGH/LOW: 00 10 20 40 00 70
EXTERNAL: 00 00 00
```

Logo, a forma correta de detectar W1A é reconstruir os words, não procurar `00 10 00 20 40 00 00 70 00` diretamente dentro do frame.

## Regras externas

Comandos ainda desconhecidos podem receber respostas adicionais por `TP02PgEmulatorRules.txt`:

```text
CMD_HEX|DELAY_MS|RESPONSE_HEX|LABEL
```

Os comandos nativos protegidos são:

```text
F0 38 34 33 0A 14
```

Uma regra externa não pode sobrescrevê-los.

## Próximas lacunas

Para considerar o Write Program totalmente fechado no equipamento real ainda faltam principalmente:

- capturar o ACK físico real do TP02 para `0x33`;
- confirmar a sequência completa de início/finalização em uma sessão real de Write;
- relacionar o banco de words PG33 com a representação devolvida por `34` para readback de ida e volta;
- depois repetir a engenharia para `System/WS`, `V`, `D`, `WC`, `FL`, EEPROM e RUN/STOP.

Até essas validações, use o emulador e as rotinas offline para desenvolvimento e reserve o PLC físico para confirmação controlada.
