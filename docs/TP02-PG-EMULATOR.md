# Emulador PG do WEG TP02

Ferramenta para fechar o protocolo de programação do TP02 usando o PC12 original como gerador de tráfego.

## Objetivo

O emulador responde aos quadros PG já confirmados e registra integralmente qualquer comando ainda desconhecido enviado pelo PC12. Isso permite descobrir a sequência de gravação sem transmitir comandos experimentais ao PLC físico.

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

Será criado:

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

O checksum mantém a soma módulo 256 do frame em `FF`.

## Descoberta da escrita

1. Abra o emulador na segunda COM virtual.
2. Configure o PC12 original na primeira COM virtual.
3. Abra um projeto mínimo no PC12.
4. Faça primeiro uma leitura para validar o diálogo.
5. Mande o PC12 gravar o programa no PLC emulado.
6. Todo comando não conhecido será salvo em `tp02-emulator-captures` como `.bin` e também aparecerá no log.
7. Repita alterando apenas uma variável por vez: uma instrução, um endereço, um valor ou um rung.
8. Compare os frames desconhecidos para identificar comando, endereço, tamanho, payload e confirmação.

O ACK genérico existe apenas para permitir que o PC12 avance enquanto a resposta real do novo comando ainda não foi identificada. Se ele mascarar o comportamento, execute com `--no-auto-ack`.
