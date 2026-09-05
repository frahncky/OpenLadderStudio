# Guia de desenvolvimento

## Limites atuais

O produto ainda e compilado diretamente pelo `csc.exe` do .NET Framework a partir de `PC12_v2.1_Windows7_v3_portatil/BUILD_INTERFACE_MODERNA.bat`. Esse script lista cada arquivo de fonte de forma explicita e produz varios executaveis Windows Forms.

Por isso, a pasta portatil e uma fronteira de compatibilidade: fontes existentes nao devem ser movidas ou renomeadas sem atualizar e validar todas as invocacoes do compilador. Arquivos `*.build.cs` sao temporarios e gerados durante o build.

## Estrutura de destino

O catalogo em `.github/architecture/modules.json` e a fonte de verdade para ownership e dependencias permitidas. A evolucao deve convergir para:

```text
src/
  OpenLadderStudio.Core/
  OpenLadderStudio.Application/
  OpenLadderStudio.Drivers.Modbus/
  OpenLadderStudio.Drivers.TP02/
  OpenLadderStudio.UI/
```

O primeiro codigo extraido deve ser sem dependencia de WinForms e mantido compativel com .NET Framework. A UI consome casos de uso; drivers implementam contratos do Core; o Core nao conhece UI ou protocolos concretos.

## Fluxo de mudanca

1. Classifique a mudanca no catalogo de modulos antes de criar o arquivo.
2. Mantenha a UI limitada a eventos, exibicao e composicao de dependencias.
3. Preserve leitura e escrita de PLC como capacidades separadas e explicitamente controladas.
4. Execute `powershell -ExecutionPolicy Bypass -File scripts/ValidateProject.ps1`.
5. Execute `OpenLadderSimTest.exe` quando a mudanca tocar o motor de varredura ou as plantas simuladas. O build ja faz isso, mas rodar antes evita um ciclo de CI.
6. Execute `PC12_v2.1_Windows7_v3_portatil/BUILD_INTERFACE_MODERNA.bat` em uma maquina Windows com .NET Framework antes de publicar executaveis.

## Estrategia de migracao

1. Extraia contratos e modelos puros para `OpenLadderStudio.Core`.
2. Extraia casos de uso que hoje vivem em Forms para `OpenLadderStudio.Application`.
3. Mova implementacoes Modbus e TP02 para drivers separados.
4. Migre o build para projetos SDK ou .NET Framework antes de retirar a pasta portatil.

Cada etapa deve manter os launchers existentes funcionais e preservar a capacidade de fallback para o executavel legado.
