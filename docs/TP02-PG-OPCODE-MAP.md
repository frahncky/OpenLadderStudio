# TP02 PG - mapa de opcodes do PC12 2.1

Este documento registra somente o que foi confirmado por captura ou por analise estatica do `pc12.exe` original. O protocolo PG proprietario nao deve ser confundido com o TP02 Host Protocol/Computer Link ASCII.

## Estado atual

| Requisicao PG | Estado | Semantica | Evidencia atual |
|---|---|---|---|
| `CON-ICB\r` | confirmada em bancada | HELLO/link PG | captura real |
| `F0 00 0F` | confirmada em bancada | preflight/status | captura real |
| `38 00 C7` | confirmada em bancada | preambulo de leitura do programa | captura real + readback |
| `34 ...` | confirmada em bancada | leitura de pagina do programa | captura real + analise PC12 |
| `33 ...` | confirmada offline | escrita de programa | analise estatica + emulacao offline do construtor PC12 |
| `0A ...` | confirmada em bancada | leitura de memoria | captura real |
| `14 00 EB` | observada/implementada | comando auxiliar | captura/fluxo PC12; semantica fina pendente |
| `01 00 FE` | **confirmada estaticamente** | **STOP / Program Mode** | PC12 monta o quadro e, apos sucesso, altera o estado para `PLC Mode: Program` |
| `02 00 FD` | **confirmada estaticamente** | **RUN** | PC12 monta o quadro e, apos sucesso, altera o estado para `PLC Mode: Running` |
| `03 00 FC` | **confirmada estaticamente** | **Clear Program** | menu PLC `Clear Program` (ID `0x141`) aponta para rotina que monta `03 00 FC` |
| `04 00 FB` | **confirmada estaticamente** | **Clear System** | menu PLC `Clear System` (ID `0x135`) aponta para rotina que monta `04 00 FB` |
| `0F 00 F0` | **confirmada estaticamente** | **Clear All Memory** | menu PLC `Clear All Memory` (ID `0x142`) aponta para rotina que monta `0F 00 F0` |
| `11 00 EE` | **confirmada estaticamente** | **Clear Data** | menu PLC `Clear Data` (ID `0x136`) aponta para rotina que monta `11 00 EE` |

Todos os seis quadros curtos (`01`, `02`, `03`, `04`, `0F`, `11`) fecham soma modulo 256 em `FF`.

## Evidencia estatica dos quatro comandos de limpeza

A tabela de menu Win32 embutida no executavel original do PC12 foi cruzada com os handlers de cada item e com os construtores de telegrama PG:

| Item do menu PLC | ID do menu | Handler observado | Construtor PG | Quadro |
|---|---:|---:|---:|---|
| `Clear System` | `0x135` | `0x004AE346` | `0x0046F1DC` | `04 00 FB` |
| `Clear Data` | `0x136` | `0x004AE491` | `0x0046F166` | `11 00 EE` |
| `Clear Program` | `0x141` | `0x004AEA95` | `0x0046F07A` | `03 00 FC` |
| `Clear All Memory` | `0x142` | `0x004AEB65` | `0x0046F0F0` | `0F 00 F0` |

Isso fecha a **semantica da requisicao**. Nao e mais correto tratar `03`, `04`, `0F` ou `11` como candidatos sem funcao conhecida.

## Resposta esperada pelo PC12

As quatro rotinas de limpeza usam o mesmo caminho generico de recepcao do PC12. O software verifica:

1. timeout;
2. soma modulo 256 igual a `FF`;
3. bits de status/erro do primeiro byte da resposta.

Nenhuma dessas quatro rotinas consome payload especifico apos uma resposta de sucesso. Por isso, na bancada virtual, o quadro minimo de sucesso compativel com o parser e:

```text
00 00 FF
```

Esse valor continua classificado como **ACK sintetico/estrutural**. A analise estatica mostra que ele e suficiente para representar uma resposta vazia de sucesso ao PC12, mas ainda nao prova que o TP02 fisico devolve exatamente esses tres bytes para cada comando.

## Descoberta automatizada

Use `src/OpenLadderStudio.Desktop/DiscoverTp02PgCommands.bat` ou:

```powershell
.\DiscoverTp02PgCommands.ps1 -Rebuild
```

A rotina e destinada exclusivamente a:

- PC12 original;
- par de portas COM virtuais;
- `OpenLadderTP02Emulator.exe` na outra ponta;
- nenhuma conexao com PLC fisico.

Para cada acao do PC12, a rotina captura todos os novos `*-unknown-*.bin`, remove quadros duplicados, valida checksum, identifica os comandos estaticamente confirmados e gera um TXT e um CSV em `tp02-emulator-captures`.

### Modos de ACK

`GENERIC` e o padrao. O emulador devolve `00 00 FF` apenas para permitir que o PC12 avance na sequencia e exponha quadros posteriores. Esse ACK e **sintetico** e nao prova qual e a resposta real do TP02.

`SILENT` nao responde a opcode desconhecido. E o modo adequado quando se quer isolar somente o primeiro comando de uma acao.

Exemplo:

```powershell
.\DiscoverTp02PgCommands.ps1 -Port COM11 -Actions STOP,RUN -AckMode SILENT -VirtualOnlyConfirmed
```

Para os quatro comandos de limpeza em bancada exclusivamente virtual:

```powershell
.\DiscoverTp02PgCommands.ps1 -Port COM11 -Actions CLEAR_SYSTEM,CLEAR_DATA,CLEAR_PROGRAM,CLEAR_ALL_MEMORY -AckMode GENERIC -VirtualOnlyConfirmed
```

Para uma sequencia mais ampla:

```powershell
.\DiscoverTp02PgCommands.ps1 -Port COM11 -Actions STOP,RUN,MONITOR_START,MONITOR_STOP,READ_PROGRAM,WRITE_PROGRAM,CLEAR_SYSTEM,CLEAR_DATA,CLEAR_PROGRAM,CLEAR_ALL_MEMORY -AckMode GENERIC -VirtualOnlyConfirmed
```

## Autoteste offline

```powershell
.\DiscoverTp02PgCommands.ps1 -SelfTest
```

O autoteste valida os checksums e a classificacao dos seis quadros curtos sem abrir porta COM.

## Regra de promocao

Um opcode so deve virar regra nativa completa do emulador quando houver evidencia suficiente para separar tres coisas:

1. formato exato da requisicao;
2. semantica da acao;
3. resposta real esperada no hardware.

As requisicoes `01`, `02`, `03`, `04`, `0F` e `11` agora tem formato e semantica confirmados por engenharia reversa do PC12. A resposta vazia `00 00 FF` e compativel com o parser e adequada para simulacao virtual, mas permanece **sintetica** ate existir captura do TP02 real.

Os quatro comandos de limpeza sao destrutivos por definicao. Nao devem ser enviados ao PLC fisico durante descoberta de protocolo sem backup, bancada isolada e validacao especifica do efeito esperado.
