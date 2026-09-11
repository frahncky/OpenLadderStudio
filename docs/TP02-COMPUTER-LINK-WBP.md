# Escrita de programa TP02 via Computer Link (WBP)

Esta rota e separada do protocolo PG proprietario usado pelo PC12. O TP02 documenta no Host/Computer Link os comandos `WBP` (Write Boolean Program) e `RBP` (Read Boolean Program).

## O que esta implementado

- codec ASCII Computer Link com prefixo `::`;
- checksum por complemento de dois;
- `PSR` para consultar o estado do PLC;
- `WBP` em blocos de 1 a 100 passos;
- faixa de programa `0000..4000` para TP02-40MR/60MR;
- cada passo de programa = 3 bytes = 6 hex (`HIGH`, `LOW`, `EXT`);
- `RBP` automatico depois de cada `WBP` para verificacao byte a byte;
- escrita recusada se `PSR` nao confirmar `STOP`;
- compilacao direta de projeto `.pladder` para palavras TP02;
- modo padrao dry-run, sem abrir porta serial.

Arquivos:

```text
src/OpenLadderStudio.Core/Tp02ComputerLinkProgramCodec.cs
src/OpenLadderStudio.Desktop/TP02WbpWriter.cs
src/OpenLadderStudio.Desktop/TP02ProjectToWbpHex.cs
src/OpenLadderStudio.Desktop/TP02WriteProject.bat
src/OpenLadderStudio.Desktop/BuildTp02WbpWriter.bat
tests/OpenLadderStudio.Core.Tests/Tp02ComputerLinkProgramCodecSelfTest.cs
```

## Ligacao fisica / modo da MMI

O Computer Link nao usa o mesmo estado eletrico do modo PG. Na porta MMI, `PG/COM` precisa estar em LOW: pino 4 ligado ao pino 5. Em modo PG o pino 4 fica aberto.

Nao confundir esta rota com o protocolo PG proprietario que estamos fechando pelo emulador.

## Perfil serial inicial

O perfil padrao usado pela ferramenta e:

```text
19200 baud
7 data bits
paridade None
1 stop bit
station 01
response code 5
```

Todos podem ser alterados na linha de comando se a memoria de sistema do TP02 estiver configurada de outra forma.

## Compilar

```bat
BuildTp02WbpWriter.bat
```

O build primeiro compila e executa o autoteste do codec. Somente depois gera:

```text
OpenLadderTP02Wbp.exe
OpenLadderTP02ProjectExport.exe
```

## Fluxo direto a partir do projeto OpenLadder

Para compilar um `.pladder` e apenas visualizar os quadros WBP:

```bat
TP02WriteProject.bat meu-projeto.pladder
```

Fluxo:

```text
.pladder
  -> LadderProjectCodec
  -> Tp02LadderTargetCompiler
  -> Tp02MachineWord (3 bytes por passo)
  -> Tp02ComputerLinkProgramCodec
  -> WBP dry-run
```

Para solicitar explicitamente a escrita real:

```bat
TP02WriteProject.bat meu-projeto.pladder COM3 WRITE
```

Mesmo com `WRITE`, a ferramenta ainda executa `PSR` e recusa a transferencia se o TP02 nao estiver em `STOP`.

## Formato intermediario de programa

Tambem e possivel usar diretamente um arquivo de palavras de maquina TP02. Cada palavra tem 6 hex:

```text
001000
002100
204000
007000
```

Ha um exemplo em:

```text
TP02-WBP-SAMPLE.hex
```

O compilador TP02 do OpenLadder ja trabalha com a mesma estrutura `Tp02MachineWord` de 3 bytes.

## Exportar projeto sem transmitir

```bat
OpenLadderTP02ProjectExport.exe meu-projeto.pladder programa.tp02.hex
```

Esse comando apenas compila e grava o codigo de maquina em arquivo.

## Dry-run de arquivo HEX

```bat
OpenLadderTP02Wbp.exe TP02-WBP-SAMPLE.hex
```

Nesse modo nenhuma COM e aberta. Os quadros WBP sao apenas exibidos.

## Escrita real de arquivo HEX

```bat
OpenLadderTP02Wbp.exe --file=programa.hex --port=COM3 --write
```

Fluxo executado:

```text
1. abre Computer Link
2. PSR
3. exige STOP
4. WBP bloco 0 (ate 100 passos)
5. exige resposta WBP valida
6. RBP do mesmo bloco
7. compara todo o codigo de maquina
8. repete ate o fim
```

Se qualquer verificacao falhar, a ferramenta interrompe e retorna erro.

## Parametros opcionais

```text
--station=1
--response=5
--start=0
--baud=19200
--databits=7
--parity=None
--stopbits=1
--timeout=2500
```

`--response` e hexadecimal (`0` a `F`).

## Observacao importante sobre o codec antigo

`Tp02TargetCompiler.BuildHostFrame()` foi escrito anteriormente para dry-run e ainda usa um unico `:` no prefixo. A documentacao oficial do TP02 e o controle Computer Link existente no OpenLadder usam `::`. Por isso a nova rota WBP usa `Tp02ComputerLinkProgramCodec`, que implementa explicitamente o enquadramento documentado com dois pontos duplos.

A rota antiga deve ser migrada para esse codec antes de ser usada como referencia de transmissao Computer Link.

## Relacao com o protocolo PG

A estrategia agora fica em duas frentes:

```text
PG proprietario: leitura + engenharia reversa da escrita pelo PC12/emulador
Computer Link: WBP/RBP documentado para obter escrita funcional mais cedo
```

Uma rota nao prova a outra. A validacao do WBP nao encerra a engenharia reversa do PG; ela apenas fornece um caminho documentado adicional para transferir programas ao TP02.
