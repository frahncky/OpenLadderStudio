# OpenLadder Studio

O **OpenLadder Studio** é um ambiente moderno de programação Ladder e ferramentas de engenharia para PLCs, com arquitetura preparada para múltiplos fabricantes e protocolos.

O projeto começou pela compatibilidade com o **WEG TP02** e com o **PC12 Design Center 2.1**, mas o núcleo do OpenLadder Studio não é mais acoplado a um único fabricante. O TP02 permanece como o primeiro driver específico em desenvolvimento, enquanto a plataforma usa perfis de dispositivo, drivers, configurações por controlador, mapas de memória e um modelo Ladder universal.

## Versão atual — 0.15

A versão 0.15 amplia a integração entre **mapa de memória e monitor Modbus** e adiciona **leitura automática de áreas extensas em múltiplos blocos**.

Principais recursos:

- shell principal universal multi-PLC;
- Editor Ladder moderno;
- modelo intermediário Ladder independente do fabricante;
- seleção de controlador por fabricante, família e modelo;
- painel de propriedades atualizado conforme o PLC selecionado;
- ativação e desativação automática de recursos conforme as capacidades do driver;
- comunicação WEG TP02 em modo seguro de leitura;
- leitura do programa TP02 por `RBP`;
- decodificação e calibração de opcodes TP02;
- reconstrução IL -> Ladder para a pesquisa TP02;
- monitor **Modbus RTU** genérico;
- monitor **Modbus TCP** genérico;
- funções Modbus 01, 02, 03 e 04 em leitura;
- CRC-16 para Modbus RTU;
- validação de MBAP e Transaction ID para Modbus TCP;
- configurações de conexão salvas separadamente para cada perfil de PLC;
- mapa de memória configurável por controlador;
- seleção de área do mapa diretamente no monitor Modbus;
- preenchimento automático da função, endereço inicial e quantidade;
- **leitura automática de áreas maiores que o limite de uma requisição Modbus**;
- **divisão automática em blocos de até 2000 bits para FC01/FC02 e 125 registradores para FC03/FC04**;
- consolidação dos blocos em uma única tabela de resultados;
- resposta bruta organizada por bloco, endereço e quantidade;
- exibição de endereços com o prefixo definido no mapa;
- acesso direto ao editor do mapa pelo monitor;
- verificação de portabilidade do projeto Ladder para o controlador selecionado;
- atualizador e instalador próprios.

## Arquitetura multi-fabricante

O fluxo principal é:

`Editor Ladder -> Modelo Ladder universal -> Driver/Compilador do fabricante -> PLC`

A separação é feita em cinco camadas:

1. **Interface OpenLadder Studio** — editor, projetos, monitoramento e ferramentas;
2. **Modelo Ladder universal** — representação independente do fabricante;
3. **Perfil de dispositivo** — fabricante, família, modelo, protocolo, transporte e nível de suporte;
4. **Driver de PLC** — comunicação e monitoramento específicos;
5. **Compilador de destino** — futura geração do programa executável de cada família.

A documentação técnica dessa arquitetura está em `docs/PLC_DRIVER_ARCHITECTURE.md`.

## Controladores e drivers

| Fabricante / perfil | Comunicação | Monitoramento | Leitura de programa | Download Ladder | Situação |
|---|---:|---:|---:|---:|---|
| WEG TP02-60MR | Sim | Sim | Sim, via RBP | Não | Implementado em leitura segura |
| Modbus RTU genérico | Sim | Sim | Não | Não | Experimental |
| Modbus TCP genérico | Sim | Sim | Não | Não | Experimental |
| Schneider Modicon M221 | Não | Não | Não | Não | Planejado |
| Delta DVP | Não | Não | Não | Não | Planejado |
| Siemens S7-1200 | Não | Não | Não | Não | Planejado |
| Mitsubishi FX5U | Não | Não | Não | Não | Planejado |
| Omron CP1L | Não | Não | Não | Não | Planejado |
| Allen-Bradley Micro850 | Não | Não | Não | Não | Planejado |

Perfis planejados aparecem no catálogo, mas não são apresentados como drivers funcionais. Escrita e transferência de programa só serão habilitadas após implementação e validação real no hardware.

## Configuração persistente por PLC

Cada perfil selecionado possui seu próprio arquivo de conexão em:

`%APPDATA%\OpenLadder Studio\connections\`

São persistidos, conforme aplicável:

- transporte RTU ou TCP;
- porta COM;
- baud rate;
- data bits;
- paridade;
- stop bits;
- host/IP;
- porta TCP;
- Unit ID;
- timeout;
- função Modbus preferida;
- endereço inicial;
- quantidade de pontos por requisição.

O monitor Modbus carrega automaticamente os valores do controlador ativo e salva a configuração ao executar uma leitura ou ao clicar em **SALVAR PERFIL**.

## Mapa de memória por controlador

Cada controlador pode manter áreas próprias em:

`%APPDATA%\OpenLadder Studio\memorymaps\`

Cada área contém:

- nome;
- tipo (`Coil`, `DiscreteInput`, `HoldingRegister`, `InputRegister` ou específico do fabricante);
- endereço inicial;
- tamanho;
- prefixo;
- observação.

A v0.15 amplia o tamanho permitido das áreas até o limite do espaço Modbus disponível a partir do endereço inicial. O editor impede que uma área ultrapasse o endereço `65535`.

### Integração com o monitor

Ao selecionar uma área:

- `Coil` seleciona automaticamente **FC01 - Read Coils**;
- `DiscreteInput` seleciona **FC02 - Read Discrete Inputs**;
- `HoldingRegister` seleciona **FC03 - Read Holding Registers**;
- `InputRegister` seleciona **FC04 - Read Input Registers**;
- o endereço inicial é preenchido pelo mapa;
- o prefixo da área é usado na coluna de endereço do resultado;
- áreas específicas do fabricante ficam em modo manual quando não houver função Modbus genérica associada.

Se a área for maior que uma única requisição, o OpenLadder Studio calcula automaticamente a quantidade de blocos e executa as leituras sequencialmente. Os dados são reunidos em uma única tabela. Em caso de falha intermediária, os dados já recebidos permanecem visíveis e o monitor informa o bloco, endereço e quantidade em que ocorreu o erro.

Limites por requisição usados pelo monitor:

- FC01 / FC02: até **2000 bits** por bloco;
- FC03 / FC04: até **125 registradores** por bloco.

## WEG TP02

O TP02 continua sendo o primeiro driver específico do projeto. Atualmente o OpenLadder Studio possui:

- comunicação serial TP02;
- leitura de estado e memória em modo seguro;
- comandos de leitura `PSR`, `MCR` e `MRV`;
- leitura da memória de programa por `RBP`;
- análise de dumps `.rbpdump`;
- laboratório de decodificação RBP -> Boolean/IL;
- campanha de calibração de opcodes;
- reconstrução IL -> Ladder;
- ferramentas de compatibilidade e análise dos arquivos do PC12.

Nenhum comando de escrita, RUN, STOP, limpeza de memória ou download de programa é liberado sem validação específica.

## Modbus RTU/TCP

A camada Modbus genérica permite monitoramento de equipamentos de diferentes fabricantes que exponham um mapa Modbus.

Recursos atuais:

- `01 - Read Coils`;
- `02 - Read Discrete Inputs`;
- `03 - Read Holding Registers`;
- `04 - Read Input Registers`;
- configuração de endereço inicial e quantidade;
- seleção rápida por área do mapa de memória;
- leitura sequencial automática em blocos;
- Unit ID configurável;
- serial: COM, baud rate, data bits, paridade e stop bits;
- TCP: host/IP e porta;
- timeout configurável;
- visualização decimal, hexadecimal e resposta bruta por bloco;
- persistência da configuração por perfil de PLC.

O uso de Modbus genérico não implica capacidade de compilar ou transferir Ladder para o PLC. Essa função depende do compilador e do protocolo de programação específicos de cada fabricante.

## Editor Ladder universal

O editor atual possui contatos normalmente abertos e fechados, bobinas, ramificações, temporizadores, contadores, SET/RESET, bordas, funções e END.

O arquivo `UniversalLadderAdapter.cs` converte a estrutura atual do editor para o modelo universal definido em `PLCPlatform.cs`. A interface possui a função **Verificar portabilidade do Ladder**, que informa a situação do projeto em relação ao controlador selecionado e deixa explícito quando ainda não existe compilador para o destino.

## Como iniciar

### OpenLadder Studio

`PC12_v2.1_Windows7_v3_portatil/INICIAR_PC12.bat`

O inicializador compila e abre `OpenLadderStudio.exe`.

### Ferramentas separadas

- `OpenLadderEditor.exe` — editor Ladder;
- `OpenLadderDeviceManager.exe` — catálogo e seleção de controladores;
- `OpenLadderModbus.exe` — monitor Modbus RTU/TCP integrado ao mapa de memória;
- `OpenLadderMemoryMap.exe` — editor de mapa de memória por controlador;
- `OpenLadderUpdater.exe` — atualizador;
- `INICIAR_PC12_CLASSICO.bat` — PC12 legado para compatibilidade.

## Arquivos principais

- `UniversalStudioShell.cs` — shell principal multi-PLC;
- `UniversalLadderAdapter.cs` — conversão do editor para o modelo Ladder universal;
- `PLCPlatform.cs` — contratos, perfis, drivers e modelo universal;
- `PLCDeviceManager.cs` — catálogo e seleção de controladores;
- `PLCConnectionSettings.cs` — persistência de parâmetros de comunicação por perfil;
- `PLCMemoryMapV15.cs` — mapa de memória com suporte a áreas extensas;
- `PLCMemoryMapManager.cs` — editor visual do mapa de memória;
- `ModbusCore.cs` — protocolo Modbus RTU/TCP em leitura;
- `ModbusBulkReader.cs` — divisão, execução e consolidação de leituras em múltiplos blocos;
- `ModbusMonitorV14.cs` — base visual do monitor integrada ao mapa de memória;
- `PrepareModbusMonitorV15.ps1` — preparação da lógica de leitura em blocos para a v0.15;
- `LadderEditor.cs` — editor Ladder;
- `TP02BridgeLab.cs` — comunicação e análise do TP02/PC12;
- `TP02ProgramReader.cs` — leitura RBP;
- `TP02MachineDecoder.cs` — análise da linguagem de máquina TP02;
- `TP02OpcodeCalibration.cs` e `TP02CalibrationCampaign.cs` — pesquisa de opcodes;
- `TP02AutoDecoder.cs` — decodificação automática experimental;
- `TP02IlToLadder.cs` — reconstrução IL -> Ladder;
- `BUILD_INTERFACE_MODERNA.bat` — compilação local;
- `.github/workflows/validate-modern-ui.yml` — validação automática em Windows.

## Compatibilidade

A aplicação usa **Windows Forms + .NET Framework**, sem bibliotecas externas obrigatórias, mantendo **Windows 7 SP1 como base mínima** e visando também Windows 8.1, 10 e 11.

## Próximas etapas

1. criar perfis personalizados de fabricante/modelo pelo próprio usuário;
2. adicionar monitoramento periódico configurável para áreas do mapa;
3. desacoplar completamente o monitor TP02 das classes de interface antigas;
4. adicionar drivers específicos para novos fabricantes;
5. criar compiladores de destino por família de PLC;
6. validar geração e transferência de programas em hardware real;
7. ampliar o modelo Ladder universal para instruções avançadas e blocos de função.

## Identidade do projeto

**Nome do software:** OpenLadder Studio  
**Arquitetura:** multi-fabricante / multi-protocolo  
**Primeiro driver específico:** WEG TP02  
**Protocolos genéricos atuais:** Modbus RTU e Modbus TCP (leitura)  
**Software legado compatível:** PC12 Design Center 2.1  
**Versão atual:** 0.15
