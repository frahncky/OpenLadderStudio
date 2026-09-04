# Arquitetura de software do OpenLadder Studio

## Objetivo

O OpenLadder Studio deve evoluir como uma plataforma de engenharia para PLCs, e não como um conjunto de telas acopladas a um fabricante específico. A arquitetura adota separação entre interface, regras de aplicação, modelo de domínio e infraestrutura de comunicação.

## Camadas

### 1. Apresentação

Responsável por WinForms, shell, editor Ladder, navegação, abas, diálogos, monitoramento visual e feedback ao usuário.

Arquivos atuais mais próximos desta camada:

- `UniversalStudioShell.cs`;
- `StudioUi.cs`;
- `LadderEditor.cs`;
- `PLCDeviceManagerV16.cs`;
- `PLCMemoryMapManager.cs`;
- `ModbusMonitorV14.cs` e etapas de preparação associadas.

A camada de apresentação pode consumir serviços e contratos do núcleo, mas o núcleo não deve depender de WinForms.

### 2. Aplicação

Coordena casos de uso, como selecionar controlador, abrir projeto, validar portabilidade, iniciar leitura e aplicar configurações.

Meta de evolução: retirar do código das Forms as decisões que não sejam estritamente de interface e concentrá-las em serviços de aplicação testáveis.

### 3. Domínio / núcleo

Contém os conceitos estáveis do produto:

- modelo Ladder universal;
- perfis de PLC;
- capacidades do controlador;
- contratos de driver;
- endereçamento e áreas de memória;
- validações independentes da interface.

Arquivos atuais:

- `PLCPlatform.cs`;
- `UniversalLadderAdapter.cs`;
- estruturas de mapa de memória que não dependem de UI.

Regra principal: o domínio não deve referenciar WinForms, arquivos de tela ou detalhes de comunicação serial/TCP.

### 4. Infraestrutura

Implementa comunicação, persistência e integração com equipamentos:

- `ModbusCore.cs`;
- `ModbusBulkReader.cs`;
- `TP02BridgeLab.cs`;
- `TP02ProgramReader.cs`;
- persistência de perfis, conexões e mapas de memória.

Drivers devem implementar contratos definidos pelo núcleo.

### 5. Build, instalação e release

Responsável por gerar os executáveis, ícones, instalador, hashes e release.

A partir da v0.22:

- `version.txt` é a fonte principal da versão;
- `scripts/PrepareInstaller.ps1` gera o instalador versionado;
- `scripts/ValidateProject.ps1` bloqueia inconsistências básicas;
- GitHub Actions compila, empacota e publica a release.

## Regra de dependência

Fluxo desejado:

`UI -> Aplicação -> Domínio <- Infraestrutura`

A infraestrutura conhece os contratos do domínio. O domínio não conhece a infraestrutura. A UI não deve acessar diretamente detalhes de protocolo quando houver um serviço ou driver apropriado.

## Dívida técnica conhecida

O projeto nasceu dentro do diretório legado `PC12_v2.1_Windows7_v3_portatil`, que ainda mistura fontes modernas, ferramentas de pesquisa e binários de compatibilidade. Também existem arquivos com sufixos de versão e scripts que transformam código durante o build.

Esses pontos não devem ser ampliados.

## Plano de reorganização gradual

### Fase 1 — v0.22

- centralizar versionamento;
- formalizar arquitetura e padrões;
- melhorar identidade visual e pipeline do ícone;
- fortalecer CI;
- separar arquivos gerados dos fontes.

### Fase 2

Criar estrutura de fontes sem quebrar a compatibilidade:

```text
src/
  OpenLadderStudio.App/
  OpenLadderStudio.Core/
  OpenLadderStudio.Drivers.Modbus/
  OpenLadderStudio.Drivers.TP02/
  OpenLadderStudio.UI/
```

O diretório legado permanece temporariamente apenas como área de compatibilidade.

### Fase 3

- eliminar scripts de patch de código fonte no build;
- remover sufixos de versão dos nomes de classes/arquivos ativos;
- introduzir testes automatizados para domínio, drivers e parsers;
- empacotar componentes por responsabilidade.

## Critérios para novas funcionalidades

Antes de adicionar uma funcionalidade, decidir:

1. ela pertence à UI, aplicação, domínio ou infraestrutura?
2. existe contrato adequado para evitar acoplamento a um fabricante?
3. há risco de escrita/comando em PLC e esse risco está explicitamente controlado?
4. a funcionalidade exige persistência? Se sim, a persistência está separada da UI?
5. o build e a release continuam reproduzíveis?

## Segurança operacional

Recursos de escrita, RUN/STOP, limpeza de memória ou download de programa devem permanecer desabilitados até validação específica por família de PLC e hardware real. A arquitetura deve manter capacidades de leitura e escrita explicitamente separadas.
