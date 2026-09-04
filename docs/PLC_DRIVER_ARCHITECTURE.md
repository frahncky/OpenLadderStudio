# Arquitetura multi-fabricante do OpenLadder Studio

O OpenLadder Studio passa a separar a interface, o modelo Ladder, os perfis de dispositivos e os drivers de comunicação. O objetivo é permitir evolução para vários fabricantes sem acoplar o núcleo do software ao WEG TP02.

## Camadas

1. **Interface OpenLadder Studio** — editor, projetos, monitoramento e ferramentas.
2. **Modelo Ladder universal** — representação intermediária independente do fabricante.
3. **Perfil de dispositivo** — fabricante, família, modelo, protocolo, transporte e nível de suporte.
4. **Driver de PLC** — conexão e monitoramento específicos do protocolo/fabricante.
5. **Compilador de destino** — futura conversão do Ladder universal para o formato executável de cada família.

Fluxo previsto:

`Editor Ladder -> Modelo universal -> Driver/Compilador do fabricante -> PLC`

## Arquivos

- `PLCPlatform.cs` — contratos, perfis, registro de drivers, capacidades e modelo Ladder universal.
- `PLCDeviceManager.cs` — catálogo visual de controladores e seleção do perfil padrão.
- `INICIAR_CONTROLADORES.bat` — inicializa o gerenciador de controladores.

O perfil escolhido é salvo em `%APPDATA%\OpenLadder Studio\device.profile`.

## Situação atual

| Fabricante / perfil | Comunicação | Monitoramento | Leitura de programa | Download Ladder | Situação |
|---|---:|---:|---:|---:|---|
| WEG TP02-60MR | Sim | Sim | Sim, via RBP | Não | Implementado em leitura segura |
| Modbus RTU genérico | Base de driver | Base de driver | Não | Não | Experimental |
| Modbus TCP genérico | Base de driver | Base de driver | Não | Não | Experimental |
| Schneider Modicon M221 | Não | Não | Não | Não | Perfil planejado |
| Delta DVP | Não | Não | Não | Não | Perfil planejado |
| Siemens S7-1200 | Não | Não | Não | Não | Perfil planejado |
| Mitsubishi FX5U | Não | Não | Não | Não | Perfil planejado |
| Omron CP1L | Não | Não | Não | Não | Perfil planejado |
| Allen-Bradley Micro850 | Não | Não | Não | Não | Perfil planejado |

## Regra de segurança técnica

O catálogo não deve confundir perfil cadastrado com suporte efetivo. Perfis marcados como **Planejado** aparecem na arquitetura, mas não podem ser selecionados como driver operacional. Recursos de escrita e transferência de programa só devem ser habilitados depois de implementação e validação real no hardware.

## Próximas etapas

1. concluir cliente Modbus RTU genérico;
2. concluir cliente Modbus TCP genérico;
3. criar mapeamento de memória configurável por dispositivo;
4. adaptar o monitor online para usar `IPlcDriver` em vez de classes TP02 diretamente;
5. converter o editor atual para o modelo Ladder universal;
6. criar compiladores por família de PLC;
7. habilitar download somente nos drivers cuja compilação e transferência tenham sido validadas.
