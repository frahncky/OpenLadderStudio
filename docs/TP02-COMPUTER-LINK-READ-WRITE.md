# TP02 - leitura e escrita de programa via Computer Link

Este caminho usa o Host Protocol documentado do WEG TP02 e nao depende do protocolo binario PG proprietario do PC12.

## Estado atual

O OpenLadder possui:

- `OpenLadderTP02Rbp.exe`: leitura de programa por `RBP`;
- `OpenLadderTP02Wbp.exe`: escrita de programa por `WBP`;
- `OpenLadderTP02ProjectExport.exe`: compila `.pladder` para words TP02 de 3 bytes;
- `OpenLadderTP02HostEmulator.exe`: emulador Computer Link para teste sem PLC;
- `TP02ReadProgram.bat`: atalho de leitura;
- `TP02WriteProject.bat`: atalho de compilacao + escrita;
- `BuildTp02WbpWriter.bat`: compila todas as ferramentas e executa o autoteste do codec.

## Protocolo

O quadro Computer Link usa o formato ASCII documentado:

```text
::AD?RCCC...SS<CR>
```

onde:

- `::` = dois caracteres `:` de inicio;
- `AD` = estacao decimal, `01..99`;
- `?` = comando;
- `R` = tempo de resposta, hexadecimal `0..F`;
- `CCC` = comando de tres letras (`PSR`, `RBP`, `WBP`, etc.);
- `SS` = checksum ASCII de complemento de dois;
- `<CR>` = `0D`.

Exemplo oficial de leitura de tres passos a partir de 0000:

```text
::01?5RBP00000324<CR>
```

O `RBP` retorna words de 3 bytes / 6 hex por passo. O `WBP` grava de 1 a 100 passos por quadro. O TP02 nao aceita `WBP` em RUN.

## Hardware / modo da MMI

Na porta MMI, Computer Link e PG sao modos diferentes.

Para Computer Link na MMI:

```text
PG/COM = LOW
pino 4 ligado ao pino 5
```

O cabo usado pelo PC12 em PG pode deixar o pino 4 aberto; nesse caso o Host Protocol nao responde.

Tambem e possivel usar a porta de comunicacao RS-485 quando ela estiver configurada para Computer Link.

## Configuracao serial

Padrao das ferramentas:

```text
19200 7N1
station=1
response=5
```

Os parametros podem ser alterados por linha de comando para coincidir com a configuracao real do TP02.

## Compilar

```bat
BuildTp02WbpWriter.bat
```

O build executa primeiro o autoteste do codec. Se algum vetor conhecido falhar, os executaveis nao sao produzidos.

## Teste fim a fim sem PLC

Crie um par null-modem virtual, por exemplo:

```text
COM10 <-> COM11
```

Na primeira janela:

```bat
StartTp02HostEmulator.bat COM11
```

Na segunda janela, leia o programa inicial do emulador:

```bat
TP02ReadProgram.bat COM10 antes.hex
```

O programa inicial esperado contem:

```text
001000
204000
007000
```

Depois grave um projeto OpenLadder no emulador:

```bat
TP02WriteProject.bat meu_projeto.pladder COM10 WRITE
```

O writer executa a seguinte sequencia:

```text
PSR -> exige STOP
RBP -> backup previo da faixa
WBP -> grava cada bloco
RBP -> verifica byte a byte cada bloco
```

Leia novamente:

```bat
TP02ReadProgram.bat COM10 depois.hex
```

A memoria do emulador e persistida em `tp02-host-emulator-memory.hex`.

## Leitura no TP02 real

Com o hardware em Computer Link:

```bat
TP02ReadProgram.bat COM3 programa_lido.hex
```

Sem `--count`, o leitor avanca em blocos de ate 100 passos ate encontrar `F-00 END = 007000` ou o limite do passo 4000.

## Escrita no TP02 real

Primeiro gere apenas o dry-run:

```bat
TP02WriteProject.bat meu_projeto.pladder
```

Para escrita real:

```bat
TP02WriteProject.bat meu_projeto.pladder COM3 WRITE
```

A escrita real somente inicia depois de:

1. resposta `PSR` valida;
2. estado confirmado como `STOP`;
3. backup `RBP` completo da faixa que sera sobrescrita.

Depois de cada `WBP`, a mesma faixa e relida por `RBP` e comparada byte a byte. Qualquer diferenca interrompe o processo.

Os backups sao preservados em:

```text
tp02-wbp-backups\
```

## Observacao sobre PG

O protocolo PG binario continua sendo estudado para compatibilidade direta com o PC12 e para uso do cabo em modo PG. Ele nao e necessario para disponibilizar leitura/escrita de programa no OpenLadder, pois `RBP/WBP` acessam a memoria de programa oficialmente pelo Computer Link.
