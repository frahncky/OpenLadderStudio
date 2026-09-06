# Guia de desenvolvimento

## Limites atuais

O produto ainda é compilado diretamente pelo `csc.exe` do .NET Framework a partir de `PC12_v2.1_Windows7_v3_portatil/BUILD_INTERFACE_MODERNA.bat`. Esse script lista cada arquivo de fonte de forma explícita e produz vários executáveis Windows Forms.

Por isso, a pasta portátil é uma fronteira de compatibilidade: fontes existentes não devem ser movidas ou renomeadas sem atualizar e validar todas as invocações do compilador. Arquivos `*.build.cs` são temporários e gerados durante o build.

## Estrutura de destino

O catálogo em `.github/architecture/modules.json` é a fonte de verdade para ownership e dependências permitidas. A evolução deve convergir para:

```text
src/
  OpenLadderStudio.Core/
  OpenLadderStudio.Application/
  OpenLadderStudio.Drivers.Modbus/
  OpenLadderStudio.Drivers.TP02/
  OpenLadderStudio.UI/
```

O primeiro código extraído, o codec do formato `.pladder`, está em `src/OpenLadderStudio.Core/LadderProject.cs`. Ele não depende de WinForms e permanece compatível com .NET Framework. A UI consome casos de uso; drivers implementam contratos do Core; o Core não conhece UI ou protocolos concretos.

## Fluxo de mudança

1. Classifique a mudança no catálogo de módulos antes de criar o arquivo.
2. Mantenha a UI limitada a eventos, exibição e composição de dependências.
3. Preserve leitura e escrita de PLC como capacidades separadas e explicitamente controladas.
4. Execute `powershell -ExecutionPolicy Bypass -File scripts/ValidateProject.ps1`.
5. Execute `OpenLadderCoreTest.exe` quando a mudança tocar o formato `.pladder` e `OpenLadderSimTest.exe` quando tocar o motor de varredura ou as plantas simuladas. O build já executa os dois.
6. Execute `PC12_v2.1_Windows7_v3_portatil/BUILD_INTERFACE_MODERNA.bat` em uma máquina Windows com .NET Framework antes de publicar executáveis.

## Estratégia de migração

1. Continue a extração de contratos e modelos puros para `OpenLadderStudio.Core`; o codec `.pladder` já foi migrado.
2. Extraia casos de uso que hoje vivem em Forms para `OpenLadderStudio.Application`.
3. Mova implementações Modbus e TP02 para drivers separados.
4. Migre o build para projetos SDK ou .NET Framework antes de retirar a pasta portátil.

Cada etapa deve manter os launchers existentes funcionais e preservar a capacidade de fallback para o executável legado.
