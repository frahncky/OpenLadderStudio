# Arquitetura multi-fabricante do OpenLadder Studio

O OpenLadder Studio separa a interface, o modelo Ladder, os perfis de dispositivos e os drivers de comunicação. O objetivo é permitir evolução para vários fabricantes sem acoplar o núcleo do software ao WEG TP02.

## Camadas

1. **Interface OpenLadder Studio** — editor, projetos, monitoramento e ferramentas.
2. **Modelo Ladder universal** — representação intermediária independente do fabricante.
3. **Perfil de dispositivo** — fabricante, família, modelo, protocolo, transporte e nível de suporte.
4. **Driver de PLC** — conexão e monitoramento específicos do protocolo/fabricante.
5. **Compilador de destino** — futura conversão do Ladder universal para o formato executável de cada família.

Fluxo previsto:

`Editor Ladder -> Modelo universal -> Driver/Compilador do fabricante -> PLC`

## Arquivos

- `PLCPlatform.cs` — contratos, perfis, registro de drivers, capacidades, modelo Ladder universal e driver do PLC virtual.
- `LadderSimulation.cs` e `ProcessSimulation.cs` — motor de varredura e plantas simuladas do PLC virtual.
- `PLCDeviceManagerV16.cs` — catálogo visual de controladores e seleção do perfil padrão.
- `ModbusCore.cs` — implementação genérica Modbus RTU e Modbus TCP.
- `ModbusMonitorV14.cs` — monitor de coils, entradas e registradores; o build aplica sobre ele as etapas de preparação V15, V17 e V18.
- `PrepareUniversalStudioV20.ps1` — integra o seletor de controlador, o mapa de memória e o monitor Modbus ao shell principal durante o build.
- `INICIAR_CONTROLADORES.bat` — inicializa o gerenciador de controladores.
- `INICIAR_MODBUS.bat` — inicializa o monitor Modbus.

O perfil escolhido é salvo em `%APPDATA%\OpenLadder Studio\device.profile`. O shell principal carrega esse perfil e passa a exibir o controlador selecionado no painel de propriedades e na barra de status.

## Integração na interface principal

O menu **PLC** do OpenLadder Studio passa a oferecer:

- **Selecionar controlador...** — abre o catálogo multi-fabricante;
- **Monitor Modbus RTU/TCP...** — abre o monitor genérico;
- **Comunicação TP02** — mantém as ferramentas específicas já existentes para o WEG TP02;
- leitura e decodificação de programa TP02 continuam separadas enquanto dependem do protocolo RBP.

A barra de ferramentas também recebe acesso direto ao monitor Modbus.

## Situação atual

| Fabricante / perfil | Comunicação | Monitoramento | Leitura de programa | Download Ladder | Situação |
|---|---:|---:|---:|---:|---|
| PLC virtual OpenLadder | Execução local | Sim | Sim | Sim, para o simulador | Implementado, sem hardware |
| WEG TP02-60MR | Sim | Sim | Sim, via RBP | Não | Implementado em leitura segura |
| Modbus RTU genérico | Sim | FC 01/02/03/04 | Não | Não | Experimental funcional |
| Modbus TCP genérico | Sim | FC 01/02/03/04 | Não | Não | Experimental funcional |
| WEG TPW-03 | Sim, com o cartão opcional RS-485/RS-232 | FC 01/02/03/04 | Não | Não | Perfil sobre o driver Modbus RTU genérico, sem validação em hardware |
| TECO SG2-20VR-D / 20VT-D e variantes 12D | Sim, RS-485 Modbus-RTU integrado | FC 01/02/03/04 | Não | Não | Perfil sobre o driver Modbus RTU genérico, sem validação em hardware |
| TECO SG2-10HR-A | Não | Não | Não | Não | Hardware sem porta Modbus; só a porta de programação proprietária |
| Schneider Modicon M221 | Não como driver específico | Pode usar Modbus quando o mapa do equipamento permitir | Não | Não | Perfil planejado |
| Delta DVP | Não como driver específico | Pode usar Modbus quando o modelo permitir | Não | Não | Perfil planejado |
| Siemens S7-1200 | Não | Não | Não | Não | Perfil planejado |
| Mitsubishi FX5U | Não | Não | Não | Não | Perfil planejado |
| Omron CP1L | Não | Não | Não | Não | Perfil planejado |
| Allen-Bradley Micro850 | Não | Não | Não | Não | Perfil planejado |

## Modbus genérico implementado

O monitor genérico executa somente funções de leitura nesta etapa:

- `01` — Read Coils;
- `02` — Read Discrete Inputs;
- `03` — Read Holding Registers;
- `04` — Read Input Registers.

No RTU são configuráveis porta COM, baud rate, data bits, paridade, stop bits, Unit ID e timeout. O quadro RTU usa CRC-16 Modbus e valida a resposta recebida.

No TCP são configuráveis endereço IP/host, porta, Unit ID e timeout. O cliente valida Transaction ID, Protocol ID e comprimento do quadro MBAP.

Escrita de coils/registradores e transferência de programa continuam desabilitadas até validação específica.

## WEG TPW-03 e TECO SG2

Os dois entram pela camada Modbus genérica, não por driver próprio: quando o equipamento
responde Modbus-RTU como escravo, o driver existente já cobre a leitura e não há
comportamento específico de fabricante a isolar.

O que muda entre eles é o hardware:

- **WEG TPW-03** — o Modbus-RTU depende do cartão opcional de comunicação RS-485 ou RS-232.
  Sem esse cartão não há porta de rede para o Studio conversar.
- **TECO SG2-20VR-D, SG2-20VT-D e variantes 12D** — trazem RS-485 Modbus-RTU integrado.
- **TECO SG2-10HR-A** — não possui porta Modbus. A folha de dados do modelo marca a
  comunicação como N/A; o RS-485 integrado aparece apenas na linha SG2-20V. A única porta
  do equipamento é a de programação usada pelo SG2 Client, com protocolo proprietário.

Por isso o SG2-10HR-A está cadastrado como **Planejado**: ele aparece no catálogo com a
explicação, mas não pode ser selecionado como driver operacional. Habilitá-lo exigiria
o mesmo tipo de trabalho já feito no TP02: levantar o protocolo da porta de programação
e validá-lo em hardware real.

Nenhum dos perfis novos foi validado em equipamento físico. Baud rate, paridade e mapa de
endereços precisam ser conferidos no manual de cada equipamento e ajustados no mapa de
memória por PLC — o catálogo não traz mapa de endereços presumido para esses modelos.

## PLC virtual

O perfil `openladder.simulator.plc` usa o driver `openladder.simulator`, que executa o modelo Ladder universal no motor de varredura interno em vez de falar com um equipamento. É o único driver com escrita e transferência de programa habilitadas, porque nenhuma saída física existe do outro lado. Está documentado em [`PROCESS_SIMULATION.md`](PROCESS_SIMULATION.md).

## Regra de segurança técnica

O catálogo não deve confundir perfil cadastrado com suporte efetivo. Perfis marcados como **Planejado** aparecem na arquitetura, mas não podem ser selecionados como driver operacional. Recursos de escrita e transferência de programa só devem ser habilitados depois de implementação e validação real no hardware.

## Próximas etapas

1. criar mapeamento de memória configurável por dispositivo;
2. adaptar o monitor online geral para usar `IPlcDriver` em vez de classes TP02 diretamente;
3. converter o editor atual para o modelo Ladder universal;
4. criar compiladores por família de PLC;
5. habilitar escrita Modbus somente com confirmação e validação;
6. habilitar download de programa apenas nos drivers cuja compilação e transferência tenham sido validadas em hardware.
