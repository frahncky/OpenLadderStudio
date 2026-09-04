# OpenLadder Studio

O **OpenLadder Studio** é um ambiente moderno de programação Ladder, monitoramento e ferramentas de engenharia para PLCs.

O **WEG TP02** é o primeiro controlador com suporte específico do projeto. A arquitetura agora é **multi-fabricante**, com perfis de dispositivos, drivers desacoplados e uma representação Ladder intermediária independente do fabricante. Também foi adicionada comunicação genérica **Modbus RTU** e **Modbus TCP** em modo de leitura.

O projeto preserva a compatibilidade com o **PC12 Design Center 2.1** e com seus arquivos legados, mas o nome do novo software é **OpenLadder Studio**. O PC12 é tratado apenas como software legado, fonte de compatibilidade e referência para a evolução do driver TP02.

## Interface principal

A interface principal é compilada como `OpenLadderStudio.exe` e reúne no mesmo ambiente:

- Editor Ladder moderno;
- seleção de controlador e perfil de dispositivo;
- monitor Modbus RTU/TCP genérico;
- comunicação e diagnóstico específicos do TP02;
- TP02 Bridge Lab;
- leitor da memória de programa por `RBP`;
- decodificador `RBP -> Boolean/IL`;
- laboratório de calibração automática de opcodes;
- conversão IL -> Ladder;
- verificação de atualizações;
- acesso às ferramentas de compatibilidade com o PC12 original.

O arquivo `PC12_v2.1_Windows7_v3_portatil/INICIAR_PC12.bat` recompila as interfaces e abre prioritariamente o **OpenLadder Studio**.

A interface principal usa tema escuro, barra de menus, barra de ferramentas, área central de edição, painéis laterais e barra de status, seguindo uma organização semelhante a softwares industriais atuais.

## Arquitetura multi-fabricante

A nova camada usa a seguinte separação:

`Editor Ladder -> Modelo Ladder universal -> Driver/Compilador do fabricante -> PLC`

Arquivos principais dessa arquitetura:

- `PLCPlatform.cs` — interfaces, capacidades, perfis, registro de drivers e modelo Ladder universal;
- `PLCDeviceManager.cs` — catálogo visual de controladores e escolha do perfil padrão;
- `ModbusCore.cs` — implementação de Modbus RTU e TCP;
- `ModbusMonitor.cs` — monitor de coils, entradas e registradores;
- `PrepareStudioBuild.ps1` — integra o seletor de controlador e o monitor Modbus ao shell principal;
- `docs/PLC_DRIVER_ARCHITECTURE.md` — documentação técnica detalhada.

O perfil selecionado é salvo em `%APPDATA%\OpenLadder Studio\device.profile`.

### Situação dos drivers

| Perfil | Comunicação | Monitoramento | Programação Ladder | Situação |
|---|---:|---:|---:|---|
| WEG TP02-60MR | Sim | Sim | Em desenvolvimento | Implementado em leitura segura |
| Modbus RTU genérico | Sim | FC 01/02/03/04 | Não | Experimental funcional |
| Modbus TCP genérico | Sim | FC 01/02/03/04 | Não | Experimental funcional |
| Schneider Modicon M221 | Perfil cadastrado | Via Modbus quando aplicável | Não | Planejado |
| Delta DVP | Perfil cadastrado | Via Modbus quando aplicável | Não | Planejado |
| Siemens S7-1200 | Perfil cadastrado | Não | Não | Planejado |
| Mitsubishi FX5U | Perfil cadastrado | Não | Não | Planejado |
| Omron CP1L | Perfil cadastrado | Não | Não | Planejado |
| Allen-Bradley Micro850 | Perfil cadastrado | Não | Não | Planejado |

Perfis marcados como **Planejado** não são apresentados como suporte operacional. Transferência de programa e escrita permanecem desabilitadas enquanto não houver implementação e validação real no hardware.

## Monitor Modbus RTU/TCP

O OpenLadder Studio já possui comunicação genérica de leitura para equipamentos que exponham mapa Modbus.

Funções implementadas:

- `01` — Read Coils;
- `02` — Read Discrete Inputs;
- `03` — Read Holding Registers;
- `04` — Read Input Registers.

No **Modbus RTU** podem ser configurados porta COM, baud rate, data bits, paridade, stop bits, Unit ID, endereço inicial, quantidade e timeout. O cliente calcula e valida CRC-16 Modbus.

No **Modbus TCP** podem ser configurados host/IP, porta, Unit ID, endereço inicial, quantidade e timeout. O cliente valida o cabeçalho MBAP, Transaction ID e Protocol ID.

O monitor pode ser aberto pelo menu **PLC** do OpenLadder Studio ou separadamente por:

`PC12_v2.1_Windows7_v3_portatil/INICIAR_MODBUS.bat`

## Gerenciador de controladores

O menu **PLC > Selecionar controlador...** abre o catálogo multi-fabricante. Também pode ser iniciado por:

`PC12_v2.1_Windows7_v3_portatil/INICIAR_CONTROLADORES.bat`

O shell principal passa a mostrar o controlador escolhido no painel de propriedades e na barra de status.

## Editor Ladder

O editor Ladder próprio usa atualmente a nomenclatura real do TP02 e já possui:

- contatos normalmente abertos e fechados;
- pontos `X0001–X0384`, `Y0001–Y0384`, `C0001–C2048` e `SC001–SC128`;
- bobina `OUT` para `Y` e `C`;
- ramificações paralelas;
- `TMR` e `CNT` com identificadores `V0001–V0256`;
- presets diretos ou por `D0001–D2048`;
- `SET` F-23 e `RESET` F-24;
- detectores de borda F-05/F-06;
- bloco genérico `FUN`;
- `END` F-00;
- múltiplos rungs, desfazer, edição, salvar/abrir e validação estrutural;
- formato `.pladder` versão 2 com leitura da versão anterior.

A próxima evolução do editor é converter esse modelo específico para o **modelo Ladder universal**, permitindo compiladores diferentes por família de PLC.

## TP02 Bridge Lab

O Bridge mantém as ferramentas específicas de compatibilidade com o WEG TP02 e o PC12.

Um projeto salvo pelo PC12 é formado por um conjunto de arquivos com o mesmo nome-base:

- `.PLC` — programa do usuário;
- `.sys1` — memória de sistema `WSxxx`;
- `.sys2` — marcadores especiais `SCxxx`;
- `.cnt` — posição/final do programa Ladder;
- `.reg1` — registradores `Vxxxx`;
- `.reg2` — registradores `Dxxxx`;
- `.reg3` — registradores `WCxxx`;
- `.sym` — símbolos/rótulos;
- `.file` — registradores de texto;
- `.cmt` — comentários;
- `.typ` — tipo do módulo básico.

O Bridge permite localizar arquivos auxiliares, gerar SHA-256/hexdump, extrair strings, comparar arquivos byte a byte e salvar relatórios de engenharia reversa.

### Comunicação TP02 em modo seguro

A moldura ASCII do protocolo TP02 oferece atualmente:

- `PSR` — ler estado do PLC;
- `MCR` — ler entradas, saídas, relés auxiliares e especiais;
- `MRV` — ler registradores `V`, `D`, `WS`, `WC` e `F`;
- `RBP` — ler memória de programa.

A configuração inicial é 19200 bps, 7 bits, paridade EVEN, 2 stop bits, estação 01 e tempo de resposta configurável.

## Leitor e decodificador RBP

O `TP02ProgramReader.cs` implementa o comando `RBP` em modo somente leitura, com leitura de passos da memória, validação de checksum, agrupamento em 3 bytes / 6 caracteres hexadecimais e salvamento de dumps `.rbpdump`.

O `TP02MachineDecoder.cs`, `TP02AutoDecoder.cs`, `TP02OpcodeCalibration.cs` e `TP02CalibrationCampaign.cs` formam a camada de pesquisa para reconstrução do programa Ladder do TP02.

A metodologia está documentada em:

- `docs/TP02_OPCODE_RESEARCH.md`;
- `docs/TP02_CALIBRATION_CAMPAIGN.md`.

## Como iniciar

### OpenLadder Studio — recomendado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_PC12.bat`

### Gerenciador de controladores

`PC12_v2.1_Windows7_v3_portatil/INICIAR_CONTROLADORES.bat`

### Monitor Modbus

`PC12_v2.1_Windows7_v3_portatil/INICIAR_MODBUS.bat`

### Editor Ladder separado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_EDITOR_LADDER.bat`

### TP02 Bridge Lab separado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_BRIDGE_TP02.bat`

### Leitor RBP separado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_LEITOR_RBP.bat`

### Decodificador RBP separado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_DECODIFICADOR_RBP.bat`

### Calibração automática separada

`PC12_v2.1_Windows7_v3_portatil/INICIAR_CALIBRACAO_OPCODE.bat`

### PC12 clássico — compatibilidade

`PC12_v2.1_Windows7_v3_portatil/INICIAR_PC12_CLASSICO.bat`

## Build e instalador

`BUILD_INTERFACE_MODERNA.bat` gera:

- `OpenLadderStudio.exe`;
- `OpenLadderEditor.exe`;
- `OpenLadderDeviceManager.exe`;
- `OpenLadderModbus.exe`;
- `OpenLadderUpdater.exe`.

O instalador inclui atalhos para o OpenLadder Studio, gerenciador de controladores, monitor Modbus e atualizador.

## Compatibilidade

As interfaces usam **Windows Forms + .NET Framework**, sem bibliotecas externas, mantendo **Windows 7 SP1 como base mínima** e visando também Windows 8.1, 10 e 11.

## Próximas etapas

1. mapeamento de memória configurável por modelo;
2. monitor online geral baseado em `IPlcDriver`;
3. migração do editor para o modelo Ladder universal;
4. compiladores por família de PLC;
5. drivers específicos para outros fabricantes;
6. escrita Modbus após validação;
7. transferência de programa apenas para drivers e compiladores validados em hardware.

## Identidade do projeto

**Nome do software:** OpenLadder Studio  
**Primeiro driver específico:** WEG TP02  
**Protocolos genéricos atuais:** Modbus RTU e Modbus TCP em leitura  
**Software legado compatível:** PC12 Design Center 2.1  
**Versão atual:** 0.11
