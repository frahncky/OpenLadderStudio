# OpenLadder Studio

O **OpenLadder Studio** é um ambiente moderno de programação Ladder e ferramentas de engenharia para PLCs, com arquitetura preparada para múltiplos fabricantes e protocolos.

O projeto começou pela compatibilidade com o **WEG TP02** e com o **PC12 Design Center 2.1**, mas o núcleo do OpenLadder Studio não é mais acoplado a um único fabricante. O TP02 permanece como o primeiro driver específico em desenvolvimento, enquanto a plataforma usa perfis de dispositivo, drivers, configurações por controlador, mapas de memória e um modelo Ladder universal.

## Versão atual — 0.17

A versão 0.17 adiciona **monitoramento online periódico** ao monitor Modbus. O usuário pode iniciar a atualização automática de entradas, saídas e registradores com intervalos configuráveis, sem precisar clicar repetidamente em **LER DISPOSITIVO**.

Principais recursos:

- shell principal universal multi-PLC;
- Editor Ladder moderno;
- modelo intermediário Ladder independente do fabricante;
- seleção de controlador por fabricante, família e modelo;
- criação, edição e exclusão de controladores personalizados;
- perfis personalizados Modbus RTU e Modbus TCP;
- persistência dos perfis personalizados em `%APPDATA%`;
- configurações de conexão e mapa de memória independentes para cada modelo cadastrado;
- comunicação WEG TP02 em modo seguro de leitura;
- leitura do programa TP02 por `RBP`;
- monitor **Modbus RTU** genérico;
- monitor **Modbus TCP** genérico;
- funções Modbus 01, 02, 03 e 04 em leitura;
- CRC-16 para Modbus RTU;
- validação de MBAP e Transaction ID para Modbus TCP;
- mapa de memória configurável por controlador;
- seleção de área do mapa diretamente no monitor Modbus;
- preenchimento automático da função, endereço inicial e quantidade;
- leitura automática de áreas maiores que o limite de uma requisição Modbus;
- divisão automática em blocos de até 2000 bits para FC01/FC02 e 125 registradores para FC03/FC04;
- consolidação dos blocos em uma única tabela de resultados;
- resposta bruta organizada por bloco, endereço e quantidade;
- **monitoramento online com intervalos de 250 ms, 500 ms, 1 s, 2 s e 5 s**;
- **proteção contra leituras concorrentes durante atualização online**;
- **intervalo de monitoramento salvo separadamente para cada perfil de PLC**;
- indicação de ciclo e horário da última atualização;
- parada automática do temporizador ao fechar a janela ou editar o mapa de memória;
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
| Perfil personalizado Modbus RTU | Sim | Sim | Não | Não | Experimental |
| Perfil personalizado Modbus TCP | Sim | Sim | Não | Não | Experimental |
| Schneider Modicon M221 | Não | Não | Não | Não | Planejado |
| Delta DVP | Não | Não | Não | Não | Planejado |
| Siemens S7-1200 | Não | Não | Não | Não | Planejado |
| Mitsubishi FX5U | Não | Não | Não | Não | Planejado |
| Omron CP1L | Não | Não | Não | Não | Planejado |
| Allen-Bradley Micro850 | Não | Não | Não | Não | Planejado |

Perfis planejados aparecem no catálogo, mas não são apresentados como drivers funcionais. Escrita e transferência de programa só serão habilitadas após implementação e validação real no hardware.

## Perfis personalizados

Na tela **Controladores e drivers**, o usuário pode criar um novo perfil informando:

- fabricante;
- família;
- modelo;
- protocolo/driver: **Modbus RTU** ou **Modbus TCP**;
- observações.

Os perfis são armazenados em:

`%APPDATA%\OpenLadder Studio\profiles\custom.profiles`

Cada perfil recebe um identificador próprio. Isso faz com que o OpenLadder Studio mantenha separadamente, para cada modelo cadastrado:

- configuração serial ou TCP;
- Unit ID;
- timeout;
- mapa de memória;
- áreas monitoradas;
- preferências de leitura;
- intervalo do monitoramento online.

Um perfil personalizado pode ser editado ou excluído. Perfis nativos do OpenLadder Studio permanecem somente leitura. Se o perfil personalizado ativo for excluído, o software retorna ao controlador padrão em vez de manter uma referência inválida.

A criação de um perfil personalizado **não cria um compilador Ladder proprietário**. Ela permite usar imediatamente as funções já suportadas pelo driver escolhido, principalmente monitoramento Modbus em leitura.

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
- quantidade de pontos por requisição;
- intervalo do monitoramento online.

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

O editor impede que uma área ultrapasse o endereço `65535`.

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

## Monitoramento online — v0.17

O monitor Modbus possui agora os controles **INICIAR ONLINE** e **PARAR ONLINE**. O intervalo pode ser escolhido entre:

- 250 ms;
- 500 ms;
- 1 s;
- 2 s;
- 5 s.

Ao iniciar o modo online, a leitura é executada imediatamente e depois repetida no intervalo escolhido. A janela informa o número do ciclo e o horário da última tentativa de atualização.

O temporizador é interrompido enquanto uma leitura está em andamento, evitando sobreposição de requisições. Isso também é aplicado às leituras em múltiplos blocos. O intervalo selecionado é salvo no perfil de conexão do PLC, mas o modo online **não é reativado automaticamente ao abrir o programa**, evitando comunicação inesperada com o equipamento.

A versão atual continua somente em leitura para Modbus e para as ferramentas modernas do TP02. Nenhuma escrita, alteração de saída ou comando de RUN/STOP é executado pelo monitor online.

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
- monitoramento online periódico;
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
- `OpenLadderDeviceManager.exe` — catálogo, criação e seleção de controladores;
- `OpenLadderModbus.exe` — monitor Modbus RTU/TCP, mapa de memória e atualização online;
- `OpenLadderMemoryMap.exe` — editor de mapa de memória por controlador;
- `OpenLadderUpdater.exe` — atualizador;
- `INICIAR_PC12_CLASSICO.bat` — PC12 legado para compatibilidade.

## Arquivos principais

- `UniversalStudioShell.cs` — shell principal multi-PLC;
- `UniversalLadderAdapter.cs` — conversão do editor para o modelo Ladder universal;
- `PLCPlatform.cs` — contratos, perfis, drivers e modelo universal;
- `PLCCustomProfiles.cs` — persistência dos perfis personalizados;
- `PLCDeviceManagerV16.cs` — catálogo e editor de controladores personalizados;
- `PreparePLCPlatformV16.ps1` — resolução dos perfis personalizados pelo núcleo;
- `PLCConnectionSettings.cs` — persistência de parâmetros de comunicação e intervalo online por perfil;
- `PLCMemoryMapV15.cs` — mapa de memória com suporte a áreas extensas;
- `ModbusCore.cs` — protocolo Modbus RTU/TCP em leitura;
- `ModbusBulkReader.cs` — divisão, execução e consolidação de leituras em múltiplos blocos;
- `ModbusMonitorV14.cs` — base visual do monitor integrada ao mapa de memória;
- `PrepareModbusMonitorV15.ps1` — preparação da lógica de leitura em blocos;
- `PrepareModbusMonitorV17.ps1` — monitoramento online periódico e proteção contra reentrada;
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

1. permitir importação/exportação de perfis e mapas de memória;
2. adicionar histórico e gráfico temporal das variáveis monitoradas;
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
**Versão atual:** 0.17
