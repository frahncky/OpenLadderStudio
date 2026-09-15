# TP02 PG - mapa completo de opcodes do PC12 2.1

Este documento registra o protocolo PG proprietario observado no `pc12.exe` original. Ele nao deve ser confundido com o TP02 Host Protocol/Computer Link ASCII.

## Inventario de opcodes usados pelo PC12

A varredura dos pontos em que o executavel escreve o primeiro byte do buffer de transmissao mostrou o conjunto explicito:

`01 02 03 04 09 0A 0F 11 12 13 14 33 34 35 37 38 F0`

Alem deles, o estabelecimento da sessao usa o texto ASCII `CON-ICB\r`.

| Opcode / quadro | Funcao | Evidencia | Observacao |
|---|---|---|---|
| `CON-ICB\r` | HELLO/link PG | captura real | abertura de sessao |
| `F0 00 0F` | preflight/status PG | captura real | resposta real observada `00 02 10 22 CB` |
| `01 00 FE` | STOP / Program Mode | estatica PC12 | muda UI para `PLC Mode: Program` |
| `02 00 FD` | RUN | estatica PC12 | muda UI para `PLC Mode: Running` |
| `03 00 FC` | Clear Program | estatica PC12 | destrutivo |
| `04 00 FB` | Clear System | estatica PC12 | destrutivo |
| `09 ...` | escrita de memoria/registrador/sistema | estatica PC12 | usado por Modify Register e Set RTC |
| `0A ...` | leitura de memoria/registrador/monitor | captura real + estatica | usado por monitor, RTC e Scan Time |
| `0F 00 F0` | Clear All Memory | estatica PC12 | destrutivo |
| `11 00 EE` | Clear Data | estatica PC12 | destrutivo |
| `12 00 ED` | EEPROM PACK -> PLC | estatica PC12 | grava PLC; potencialmente destrutivo |
| `13 00 EC` | PLC -> EEPROM PACK | estatica PC12 | grava pack |
| `14 00 EB` | password/security preflight | estatica PC12 | usado antes de operacoes privilegiadas |
| `33 ...` | Write Program Data | estatica + emulacao offline | escrita do ladder |
| `34 ...` | Read Program Page | captura real + estatica | leitura paginada |
| `35 ...` | Set/Reset I/O Coil | estatica PC12 | comando do menu Monitor |
| `37 ...` | BIOS Refresh/Update | estatica PC12 | **CRITICO**; atualizacao de firmware |
| `38 00 C7` | preambulo de leitura do programa | captura real | respostas reais observadas variam |
 
## Comandos de controle e limpeza

| Menu PLC | ID | Handler | Quadro |
|---|---:|---:|---|
| RUN | `0x12F` | `0x004AE5DC` | `02 00 FD` |
| STOP | `0x130` | `0x004AE836` | `01 00 FE` |
| Clear System | `0x135` | `0x004AE346` | `04 00 FB` |
| Clear Data | `0x136` | `0x004AE491` | `11 00 EE` |
| Clear Program | `0x141` | `0x004AEA95` | `03 00 FC` |
| Clear All Memory | `0x142` | `0x004AEB65` | `0F 00 F0` |

Os quadros curtos fecham soma modulo 256 em `FF`.

## Menu PLC principal

O mapa de eventos embutido no PC12 liga os itens principais aos handlers:

| ID | Acao |
|---:|---|
| `0x12D` | Write PC Program To PLC |
| `0x12E` | Read Program from PLC to PC |
| `0x12F` | Control PLC To Run |
| `0x130` | Control PLC To Stop |
| `0x131` | Set or Change PLC Password |
| `0x132` | EEPROM PACK / PLC transfer |
| `0x133` | Set Computer Link Port |
| `0x134` | Check PLC Program Logic |
| `0x135` | Clear PLC System |
| `0x136` | Clear PLC Register Data |
| `0x13F` | Set PLC RTC |
| `0x140` | Refresh PLC BIOS Program |
| `0x141` | Clear User Program |
| `0x142` | Clear System, Data and User Program |
| `0x143` | Compare PC12 & PLC Program |

## Monitor: mapa completo dos eventos

Os IDs `0x191` a `0x199` aparecem na mesma ordem dos recursos de menu do PC12:

| ID | Handler | Funcao |
|---:|---:|---|
| `0x191` | `0x004BED41` | Monitor PLC Program By Boolean Way |
| `0x192` | `0x004BEF0D` | Monitor PLC Program By Ladder Way |
| `0x193` | `0x004BF299` | Modify PLC Register Value In Monitor |
| `0x194` | `0x004BF6FE` | Monitor PLC Real Time Clock |
| `0x195` | `0x004BF78A` | Set PLC Time-Out Value |
| `0x196` | `0x004BF162` | Set or Reset I/O Coil |
| `0x197` | `0x004BF3D0` | Watch Now Scan Time |
| `0x198` | `0x004BF101` | Monitor Register or I/O Coils Data |
| `0x199` | `0x004AB5E0` | Abort Monitor Status |

### Como o monitor funciona

Nao foi encontrado um opcode isolado que signifique genericamente "entrar em monitor". O PC12 controla o estado de monitor na aplicacao e usa principalmente `0A` para buscar memoria/registradores. Quando necessario, o PLC deve estar em RUN.

Boolean, Ladder e Register/I/O Data usam leituras `0A` dinamicas conforme os enderecos/objetos exibidos.

`Set PLC Time-Out Value` aparece como configuracao do lado PC/cliente; o handler nao mostrou um telegrama PG dedicado.

`Abort Monitor Status` encerra o estado de monitor da aplicacao.

## RTC

### Monitor RTC

Quadro fixo confirmado estaticamente:

```text
0A 03 53 F9 06 A0
```

- endereco: `0x53F9`
- quantidade: 6 bytes
- uso: exibicao de RTC no monitor.

### Set RTC

Antes de abrir/aplicar a edicao, o PC12 le um bloco maior:

```text
0A 03 53 F9 0E 98
```

Depois grava pelo opcode `09`:

```text
09 11 53 F9 0E <14 bytes de dados> <checksum>
```

O dialogo apresenta Year, Month, Day, Hour, Minute e Second; o bloco interno possui sete pares de bytes e inclui um campo adicional de calendario/controle.

## Scan Time

O item `Watch Now Scan Time` faz:

```text
0A 03 60 00 06 8C
```

Le 6 bytes a partir de `0x6000`, usados pelo PC12 para mostrar:

- Now PLC Scan Time
- Now PLC Max. Scan Time
- Now PLC Min. Scan Time

Isto corresponde a tres valores de 16 bits.

## Modify Register

O item Monitor -> Modify Register usa o escritor generico `09`.

Formato observado para uma palavra:

```text
09 05 <addr-hi> <addr-lo> 02 <value-hi> <value-lo> <checksum>
```

O parser do PC12 aceita familias como:

- `V`: faixa mostrada 1..1024
- `D`: faixa mostrada 1..2048
- `WC`: faixa mostrada 1..912

Os codigos de endereco internos variam conforme a familia.

## Set/Reset I/O Coil

O comando dedicado e `35`.

Formato:

```text
35 03 <coil-addr-1> <coil-addr-2> <mode> <checksum>
```

O dialogo do PC12 mostra:

- `X,Y=1-->384 , C=1-->2048`
- `SET(ON)`
- `RESET(OFF)`

O byte de modo usa um bit alto observado no construtor. O help do PC12 diferencia este recurso de Force/Unforce persistente; portanto ele deve continuar nomeado **Set/Reset I/O Coil**, nao "force".

## EEPROM PACK

O dialogo possui duas direcoes e o retorno do dialogo seleciona diretamente o opcode:

```text
12 00 ED  = EEPROM PACK -> PLC
13 00 EC  = PLC -> EEPROM PACK
```

O fluxo exige PLC parado e passa pelas verificacoes de senha. A documentacao do PC12 associa o recurso a EEPROM PACK em modulos base de 40/60 pontos.

## Password / security preflight

`14 00 EB` aparece antes de operacoes privilegiadas quando o estado de senha/autorizacao ainda precisa ser validado, incluindo fluxos de:

- Read Program
- Write Program
- Compare Program
- Set/Change Password
- EEPROM

Por isso a classificacao anterior "comando auxiliar" foi substituida por **password/security preflight**. O significado fino do payload de resposta ainda depende de captura/analise adicional.

## BIOS Refresh

`37` pertence exclusivamente ao fluxo de BIOS Refresh/Update, nao ao monitor.

O PC12 exige PLC STOP, seleciona arquivo BIOS `*.BIN`, transmite blocos por `37` e encerra com:

```text
37 02 FF FF C8
```

Depois orienta reinicializar a alimentacao do PLC.

**Nunca usar `37` em PLC fisico durante descoberta casual. Um erro pode corromper firmware e inutilizar o controlador.**

## Leitura e escrita de programa

Fluxo conhecido:

- `F0`: preflight/status
- `14`: autenticacao/preflight quando necessario
- `38`: preambulo de leitura
- `34`: leitura das paginas
- `33`: escrita das palavras de programa

O emulador ja cobre leitura/escrita, limites PG33, fuzz, PG34 multipagina, END, memoria esparsa e cenario v1.56.

## Respostas reais x respostas sinteticas

A requisicao e a semantica de um opcode podem estar confirmadas sem que a resposta fisica exata esteja confirmada.

Respostas reais ja observadas incluem:

```text
F0 -> 00 02 10 22 CB
38 -> 00 02 00 02 FB
38 -> 00 02 00 0A F3
```

O quadro:

```text
00 00 FF
```

e aceito pelo caminho generico do parser como sucesso vazio em varias operacoes e e util na bancada virtual. Ele continua sendo **ACK sintetico/estrutural**, nao prova da resposta exata do TP02 real para RUN, STOP, clears, EEPROM, coil ou BIOS.

## Codigos de erro observados no PC12

O decodificador interno possui, entre outros:

| Codigo | Texto resumido |
|---:|---|
| `01` | System ROM Diagnostic Error |
| `02` | System RAM Diagnostic Error |
| `03` | Flash Memory/EEPROM Diagnostic Error |
| `04` | User Program address diagnostic error |
| `05` | System WS diagnostic error |
| `06` | I/O Bus diagnostic error |
| `07` | Remote I/O diagnostic error |
| `08` | Battery failure |
| `09` | Communication checksum error |
| `0A` | User Program Death Loop |
| `0B` | WatchDog Timer diagnostic error |
| `0C` | Power Down Error |
| `0D` | Controller in running mode |
| `0E` | Reference range overflow |
| `0F` | User ROM version incorrect |
| `10` | Cannot program while outputs enable |
| `11` | No enough space to insert |
| `12` | Cannot program while PLC running |
| `13` | Monitor program complete |
| `14` | Not PG Protocol Function |
| `16` | EEPROM PACK Data Error |
| `17` | C259 ON, can not force |
| `18` | Cannot use F34_SCLK |
| `19` | Must clear memory |
| `1A` | Password incorrect |
| `20` | F08 ENDS Error |
| `21` | Range Over |
| `22` | Stack Over |
| `23` | Stack Under |
| `24` | MCR Error |
| `25` | JCS Error |
| `26` | JCR Error |
| `27` | TMR/CNT Double Used |
| `28` | FOR/NEXT Error |
| `29` | Label Not Exist |
| `30` | Double OUT |

## Ferramentas do repositorio

- `AnalyzeTp02Capture.ps1`: reconhece o catalogo completo acima e especializa `09/0A/35/37`.
- `DiscoverTp02PgCommands.ps1`: captura opcodes ainda desconhecidos numa bancada PC12 + par COM virtual.
- `OpenLadderTP02Emulator.exe`: emulador da bancada virtual.

Autoteste do analisador:

```powershell
.\AnalyzeTp02Capture.ps1 -SelfTest
```

Descoberta virtual:

```powershell
.\DiscoverTp02PgCommands.ps1 -Rebuild
```

## Regra de promocao

Para declarar um recurso "completo" no emulador, separar:

1. formato exato da requisicao;
2. semantica da operacao;
3. resposta real do hardware;
4. efeito de estado/memoria.

Comandos destrutivos ou criticos (`03`, `04`, `0F`, `11`, `12`, `35` quando altera estado, e principalmente `37`) nao devem ser usados como sondas em um PLC fisico sem backup, bancada isolada e plano de recuperacao.
