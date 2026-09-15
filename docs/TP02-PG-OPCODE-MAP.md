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
| `03 00 FC` | quadro confirmado estaticamente | controle `03`; semantica pendente | construtor dedicado no PC12; acao exige confirmacao |
| `04 00 FB` | quadro confirmado estaticamente | controle `04`; semantica pendente | construtor dedicado no PC12; acao exige PLC parado |
| `0F 00 F0` | quadro confirmado estaticamente | **candidato destrutivo/clear**; semantica exata pendente | construtor dedicado, PLC parado e confirmacao do usuario |
| `11 00 EE` | quadro confirmado estaticamente | controle `11`; semantica pendente | construtor dedicado no PC12; acao exige PLC parado |

Todos os seis quadros curtos (`01`, `02`, `03`, `04`, `0F`, `11`) fecham soma modulo 256 em `FF`.

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

Para cada acao do PC12, a rotina captura todos os novos `*-unknown-*.bin`, remove quadros duplicados, valida checksum, identifica os candidatos estaticos acima e gera um TXT e um CSV em `tp02-emulator-captures`.

### Modos de ACK

`GENERIC` e o padrao. O emulador devolve `00 00 FF` apenas para permitir que o PC12 avance na sequencia e exponha quadros posteriores. Esse ACK e **sintetico** e nao prova qual e a resposta real do TP02.

`SILENT` nao responde a opcode desconhecido. E o modo adequado quando se quer isolar somente o primeiro comando de uma acao.

Exemplo:

```powershell
.\DiscoverTp02PgCommands.ps1 -Port COM11 -Actions STOP,RUN -AckMode SILENT -VirtualOnlyConfirmed
```

Para uma sequencia mais ampla:

```powershell
.\DiscoverTp02PgCommands.ps1 -Port COM11 -Actions STOP,RUN,MONITOR_START,MONITOR_STOP,READ_PROGRAM,WRITE_PROGRAM,CLEAR_PROGRAM -AckMode GENERIC -VirtualOnlyConfirmed
```

## Autoteste offline

```powershell
.\DiscoverTp02PgCommands.ps1 -SelfTest
```

O autoteste valida os checksums e a classificacao dos seis quadros curtos sem abrir porta COM.

## Regra de promocao

Um opcode so deve virar regra nativa do emulador quando houver evidencia suficiente para separar tres coisas:

1. formato exato da requisicao;
2. semantica da acao;
3. resposta esperada pelo PC12.

Por isso `01` e `02` ja podem ser tratados como requisicoes RUN/STOP confirmadas, mas a resposta real do PLC ainda nao deve ser declarada como conhecida. Para `03`, `04`, `0F` e `11`, a semantica continua em investigacao.

`0F` permanece marcado como potencialmente destrutivo e nao deve ser enviado ao PLC fisico ate confirmacao independente.
