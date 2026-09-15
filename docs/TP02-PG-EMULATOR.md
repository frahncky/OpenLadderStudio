# Emulador PG do WEG TP02

Ferramenta de laboratório para reproduzir o protocolo PG do TP02 sem transmitir comandos experimentais ao PLC físico.

O emulador deve ser usado **somente em um par de portas COM virtuais**. Nunca aponte o emulador para a COM física conectada ao TP02.

## Estado atual

Perfil serial observado: `19200 8O1`. Os quadros binários confirmados fecham soma módulo 256 em `FF`.

Comandos implementados no banco virtual:

```text
CON-ICB<CR> -> 80 01 09 75   (ou C0 01 09 35)
F0 00 0F    -> 00 02 10 22 CB
38 00 C7    -> resposta de estado/tamanho coerente com o programa virtual
34 ...       -> 00 F0 + 240 bytes + checksum, derivados do banco virtual
33 ...       -> Write Program Data; decodificado e gravado por endereço
0A ...       -> leitura de memória auxiliar
14 00 EB     -> 00 00 FF
```

O `0x33` está confirmado como **Write PLC Program / Write Program Data**. O emulador valida formato, quantidade de words, endereço e checksum antes de atualizar o banco virtual.

O ACK `00 00 FF` produzido pelo emulador continua sendo chamado de **ACK emulado**: o valor é o usado no modelo, mas a resposta é gerada por software e não substitui a confirmação final no hardware.

## PG33

Estrutura usada:

```text
33 | LEN | 00 | START_H | START_L | 2*W | HIGH/LOW... | EXTERNAL... | CHK
```

Regras:

```text
LEN = 3*W + 4
1 <= W <= 80
START = endereço inicial em passos/words
soma(frame) mod 256 = FF
```

Cada word recebido é armazenado como:

```text
HIGH LOW EXTERNAL
```

O dump bruto fica em:

```text
tp02-emulator-captures\TP02-Emulator-AAAAMMDD-HHMMSS-pg33-program.bin
```

## Readback PG38/PG34

O build do emulador integra `TP02PgReadback.cs`. Quando existe um programa no banco virtual, as respostas de `38` e `34` são sintetizadas a partir desse banco.

O PG34 usa a geometria fisicamente observada:

```text
240 bytes de payload
160 bytes = 80 pares HIGH/LOW
80 bytes  = BRAW
```

O BRAW é calculado pela regra observada em bancada, a partir de HIGH/LOW. O campo EXTERNAL recebido no PG33 é preservado no banco, mas não é copiado diretamente para o BRAW.

A leitura lógica termina no primeiro `F-00 END` (`00 70`). Words antigos que ainda existam fisicamente depois do END podem permanecer no banco bruto; o leitor canônico deve ignorá-los. Isso reproduz o comportamento já observado no TP02 real após uma gravação curta sobre uma memória que possuía programa maior.

## Cenário automático da v1.56

A v1.56 ganhou um cenário específico para reproduzir o caso real que motivou a correção do enlace de programação.

Estado inicial virtual:

```text
323 words
0000,0002,...,0320 = STR X0001
0001,0003,...,0321 = OUT C0001
0322 = F-00 END
```

A leitura desse programa exige exatamente:

```text
PG34 start=0000
PG34 start=0080
PG34 start=0160
PG34 start=0240
PG34 start=0320
```

Resultado esperado:

```text
5 páginas
323 words
END = 0322
```

Depois, o cenário aplica o PG33 mínimo:

```text
STR X0001
OUT C0001
F-00 END
```

O readback canônico esperado passa a ser:

```text
1 página
3 words
END = 0002
```

O teste também confirma que o backup original de 323 words permanece preservado e que o resíduo bruto posterior ao novo END não é artificialmente apagado pelo emulador.

### Executar o self-test sem nenhuma COM

Depois de compilar:

```bat
OpenLadderTP02Emulator.exe --self-test-v156
```

Saída esperada:

```text
TP02 v1.56 emulator roundtrip: PASS
backup=323 words / 5 paginas / END=0322
write=PG33 3 words / STR X0001 / OUT C0001 / END
readback=3 words / 1 pagina / END=0002 / stale tail preservado
```

Para rodar todos os testes internos:

```bat
OpenLadderTP02Emulator.exe --self-test-all
```

Esses testes não abrem porta serial e não enviam nada ao PLC físico.

## Cenário v1.56 com COM virtual

Para testar o OpenLadder de ponta a ponta, crie um par null-modem virtual, por exemplo:

```text
OpenLadder Studio          -> COM10
OpenLadderTP02Emulator.exe -> COM11
```

Compile e inicie com:

```bat
BuildTp02Emulator.bat
RunTp02EmulatorV156.bat COM11
```

O atalho `--scenario=v156` configura automaticamente:

```text
seed inicial = boundary323
HELLO        = silêncio nas 4 primeiras tentativas; responde na 5ª
F0           = silêncio nas 3 primeiras tentativas; responde na 4ª
PG33 ACK     = 00 00 FF emulado
ACK genérico = OFF
```

Assim é possível exercitar a persistência de HELLO/F0 da v1.56 sem usar o PLC real.

No OpenLadder conectado à outra ponta do par virtual, o teste alvo é:

```text
CONECTAR
-> STOP
-> ESCREVER projeto de 3 words
-> backup PG34 do programa de 323 words
-> PG33 único
-> ACK 00 00 FF emulado
-> readback PG34
-> 3 words / END 0002 / projeto idêntico
```

## Opções úteis

```text
--scenario=v156       cenário completo descrito acima
--seed=boundary323    somente carrega o programa inicial de 323 words
--hello-on=N          responde HELLO a partir da tentativa N, 1..8
--f0-on=N             responde F0 a partir da tentativa N, 1..8
--self-test-v156      roundtrip 323 -> 3 sem COM
--self-test-all       todos os testes sem COM
--pg33-ack            habilita ACK emulado 00 00 FF para PG33
--no-pg33-ack         recebe PG33 sem responder
--auto-ack            responde genericamente a comandos desconhecidos
--no-auto-ack         apenas captura comandos desconhecidos
--hello=80            estado STOP
--hello=c0            estado RUN
--fast                remove atrasos aproximados
```

No cenário v1.56, `--no-auto-ack` é aplicado por segurança mesmo que não seja informado explicitamente.

## Compilar

```bat
BuildTp02Emulator.bat
```

O build gera:

```text
OpenLadderTP02Emulator.exe
```

A integração do readback e do cenário de laboratório é feita por `PrepareTp02EmulatorRules.ps1` durante o build.

## Limites do emulador

O emulador permite validar estrutura de quadros, paginação, checksum, estado lógico da memória e comportamento do software diante de respostas tardias. Ele **não prova** sozinho:

- temporização elétrica real do TP-232PG;
- comportamento do driver USB/serial real;
- aceitação final de um comando pelo hardware TP02;
- efeitos físicos em entradas e saídas.

Por isso, depois de o cenário virtual passar, o PLC real deve ser usado apenas para uma confirmação controlada e sem retransmissões cegas.
