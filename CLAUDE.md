# OpenLadder Studio

Ambiente de engenharia para programação Ladder, configuração e monitoramento de PLCs. C# / WinForms / .NET Framework, compilado direto pelo `csc.exe`. Produto e código em **português do Brasil**.

## Especialistas

Há agentes com o conhecimento detalhado de cada área em `.claude/agents/`. Delegue quando a tarefa cair claramente em uma delas:

| Agente | Área |
|---|---|
| `build-chain` | scripts `Prepare*.ps1`, `*.build.cs`, compilação |
| `interface` | cor, tema, ícone, layout, DPI |
| `portugues` | acentuação, encoding, texto de interface |
| `ladder-plc` | varredura, endereçamento, plantas simuladas |
| `release` | versão, notas, CI, instalador, atualizador |

## Quatro coisas que quebram o build ou o produto

Valem para qualquer alteração, mesmo pequena.

**1. Âncoras textuais.** Os ~45 `Prepare*.ps1` localizam o ponto de alteração por trecho literal do código, **inclusive comentários em português**. Antes de reescrever um comentário ou declaração em `PC12_v2.1_Windows7_v3_portatil`, procure o texto:

```bash
grep -n "trecho exato" PC12_v2.1_Windows7_v3_portatil/Prepare*.ps1
```

**2. BOM em `.ps1` com acento.** O build usa Windows PowerShell 5.1, que lê arquivo sem BOM como Windows-1252. Sem BOM, o texto injetado chega corrompido à tela — silenciosamente, porque compila e passa nos testes.

**3. Nenhuma tela declara cor própria.** A fonte única é `OpenLadderPalette`, em `AppBranding.cs`. Um `Color.FromArgb(...)` novo em código de tela é defeito.

**4. Push para `main` publica uma release pública.** Trabalhe em branch, abra PR, espere o job `build` ficar verde. Correção sem bump de `version.txt` não chega a nenhum usuário.

## Validar

```powershell
powershell -ExecutionPolicy Bypass -File scripts/ValidateProject.ps1
cd PC12_v2.1_Windows7_v3_portatil; cmd /c ".\BUILD_INTERFACE_MODERNA.bat"
```

O critério é saída 0 e os dois autotestes (`OpenLadderSimTest`, `OpenLadderCoreTest`) com "Todas as verificações passaram". Os `*.build.cs` são temporários e nunca versionados.

## Documentação

`docs/UI_GUIDELINES.md` (interface) · `docs/PROCESS_SIMULATION.md` (PLC virtual e plantas) · `docs/DEVELOPMENT_GUIDE.md` (build e arquitetura) · `docs/SOFTWARE_ARCHITECTURE.md` · `CONTRIBUTING.md`
