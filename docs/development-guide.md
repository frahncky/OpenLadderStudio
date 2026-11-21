# Guia de desenvolvimento

## Solução moderna em migração

A trilha moderna é compilada pela solução `OpenLadderStudio.sln` e mantém as
responsabilidades separadas em projetos SDK:

```text
src/OpenLadderStudio.Core/
src/OpenLadderStudio.Application/
src/OpenLadderStudio.Drivers.Modbus/
src/OpenLadderStudio.Drivers.TP02/
src/OpenLadderStudio.UI/
```

No ambiente atual ela usa .NET 8, fixado em `global.json`, porque esse é o SDK
instalado e validado. A migração para .NET 10 consiste em atualizar o SDK e os
`TargetFramework`; não deve misturar responsabilidades novamente.

Para compilar e testar:

```powershell
dotnet build OpenLadderStudio.sln
dotnet run --project tests/OpenLadderStudio.Modernization.Tests/OpenLadderStudio.Modernization.Tests.csproj
```

Warnings são tratados como erros no código moderno. A nulabilidade permanece
temporariamente desabilitada apenas no projeto Core existente, que ainda é
compilado também pelo `csc.exe` do .NET Framework; código novo mantém
nulabilidade habilitada.

O `Build.bat` legado continua sendo o build oficial do produto distribuído até
que editor, simulador, drivers e instalador tenham sido migrados e validados.

## Limites atuais

O produto ainda é compilado diretamente pelo `csc.exe` do .NET Framework a partir de `src/OpenLadderStudio.Desktop/Build.bat`. Esse script lista cada arquivo de fonte de forma explícita e produz vários executáveis Windows Forms.

Por isso, o módulo Desktop é uma fronteira de compatibilidade: fontes existentes não devem ser movidas ou renomeadas sem atualizar e validar todas as invocações do compilador. Arquivos `*.build.cs` são temporários e gerados durante o build.

## Scripts `Prepare*.ps1`

Antes de compilar, o build executa uma sequência de scripts que reescrevem os fontes em `*.build.cs`. Duas regras não negociáveis:

**Âncoras textuais.** Cada script localiza o ponto de alteração por um trecho literal do código — inclusive comentários em português. Alterar um desses trechos no fonte quebra o build com `... não encontrado`. Antes de reescrever um comentário ou uma declaração no módulo Desktop, procure o texto em `Prepare*.ps1`.

**Codificação com BOM.** Um script `.ps1` que contenha qualquer caractere fora do ASCII **precisa** ser salvo em UTF-8 **com BOM**. O build o invoca pelo Windows PowerShell 5.1, que lê arquivo sem BOM como Windows-1252: sem o BOM, `•` vira `â€¢` e `—` vira `â€"` dentro das strings geradas. O dano é silencioso — compila e só aparece na tela do usuário — e já produziu um defeito funcional, uma busca por `IndexOf("  •")` que nunca casava.

Para conferir os dois pontos antes de publicar:

```powershell
Get-ChildItem src/OpenLadderStudio.Desktop -Filter *.ps1 | Where-Object {
    $b = [System.IO.File]::ReadAllBytes($_.FullName)
    ($b | Where-Object { $_ -gt 127 }) -and -not ($b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF)
}
```

A saída deve ser vazia.

## Cores

Nenhuma tela declara cor própria. A paleta única do produto é `OpenLadderPalette`, em `AppBranding.cs`, que entra em todos os executáveis. Regras e valores dos temas estão em [`ui-guidelines.md`](ui-guidelines.md).

Os nomes e os caminhos atuais estão documentados em [Nomes e estrutura do repositório](repository-layout.md).

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
6. Execute `src/OpenLadderStudio.Desktop/Build.bat` em uma máquina Windows com .NET Framework antes de publicar executáveis.

## Vetores de referência do PC12

`tests/OpenLadderStudio.Core.Tests/Data/pc12-golden-vectors.tsv` guarda os quadros que o `pc12.exe` original monta, extraídos pelos emuladores `scripts/emulate_pc12_*.py` (Unicorn, offline, nenhum TX). O autoteste `Tp02Pc12GoldenVectorsSelfTest`, executado pelo `Build.bat`, confere byte a byte os construtores do Core (PG35, PG09, leituras 0A, RTC, quadros curtos, 34 e PG33) contra esses vetores.

- Diferença intencional entre o OpenLadder e o PC12 é declarada na lista `Known` do autoteste, com o resultado exato do OpenLadder e o motivo. Qualquer outra diferença falha o build.
- Ao mudar um emulador ou o `pc12.exe`, regenere o arquivo com `python3 scripts/export_pc12_golden_vectors.py` (requer `unicorn==2.1.4`, `pefile==2024.8.26` e `capstone`). O workflow `validate-pc12-golden-vectors.yml` falha se o arquivo versionado estiver desatualizado.
- Os vetores provam igualdade com o PC12, não aceitação pelo TP02: efeito no equipamento continua exigindo teste de bancada.

## Estratégia de migração

1. Continue a extração de contratos e modelos puros para `OpenLadderStudio.Core`; o codec `.pladder` já foi migrado.
2. Extraia casos de uso que hoje vivem em Forms para `OpenLadderStudio.Application`.
3. Mova implementações Modbus e TP02 para drivers separados.
4. Migre o build para projetos SDK ou .NET Framework antes de retirar o módulo Desktop.

Cada etapa deve manter os launchers existentes funcionais e preservar a capacidade de fallback para o executável legado.
