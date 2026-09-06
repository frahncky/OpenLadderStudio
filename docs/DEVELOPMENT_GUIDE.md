# Guia de desenvolvimento

## Limites atuais

O produto ainda é compilado diretamente pelo `csc.exe` do .NET Framework a partir de `PC12_v2.1_Windows7_v3_portatil/BUILD_INTERFACE_MODERNA.bat`. Esse script lista cada arquivo de fonte de forma explícita e produz vários executáveis Windows Forms.

Por isso, a pasta portátil é uma fronteira de compatibilidade: fontes existentes não devem ser movidas ou renomeadas sem atualizar e validar todas as invocações do compilador. Arquivos `*.build.cs` são temporários e gerados durante o build.

## Scripts `Prepare*.ps1`

Antes de compilar, o build executa uma sequência de scripts que reescrevem os fontes em `*.build.cs`. Duas regras não negociáveis:

**Âncoras textuais.** Cada script localiza o ponto de alteração por um trecho literal do código — inclusive comentários em português. Alterar um desses trechos no fonte quebra o build com `... não encontrado`. Antes de reescrever um comentário ou uma declaração na pasta portátil, procure o texto em `Prepare*.ps1`.

**Codificação com BOM.** Um script `.ps1` que contenha qualquer caractere fora do ASCII **precisa** ser salvo em UTF-8 **com BOM**. O build o invoca pelo Windows PowerShell 5.1, que lê arquivo sem BOM como Windows-1252: sem o BOM, `•` vira `â€¢` e `—` vira `â€"` dentro das strings geradas. O dano é silencioso — compila e só aparece na tela do usuário — e já produziu um defeito funcional, uma busca por `IndexOf("  •")` que nunca casava.

Para conferir os dois pontos antes de publicar:

```powershell
Get-ChildItem PC12_v2.1_Windows7_v3_portatil -Filter *.ps1 | Where-Object {
    $b = [System.IO.File]::ReadAllBytes($_.FullName)
    ($b | Where-Object { $_ -gt 127 }) -and -not ($b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF)
}
```

A saída deve ser vazia.

## Cores

Nenhuma tela declara cor própria. A paleta única do produto é `OpenLadderPalette`, em `AppBranding.cs`, que entra em todos os executáveis. Regras e valores dos temas estão em [`UI_GUIDELINES.md`](UI_GUIDELINES.md).

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
