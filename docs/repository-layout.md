# Nomes e estrutura do repositório

## Convenções

- Módulos C# em `src/OpenLadderStudio.<Responsabilidade>/`; arquivos C# em PascalCase, com nomes ligados ao tipo ou à responsabilidade.
- Scripts PowerShell e lançadores Windows em PascalCase, com verbo e finalidade: `ValidateProject.ps1`, `Build.bat`, `StartStudio.bat`.
- Documentação em `docs/` com nomes em minúsculas e palavras separadas por hífen. `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md` e arquivos de configuração seguem os nomes reconhecidos pelas ferramentas.
- Scripts Python mantêm `snake_case`, seguindo a convenção da linguagem.
- Novos arquivos não recebem versões, sistema operacional ou qualificadores como “moderno” e “final” no nome. A versão do produto fica em `src/OpenLadderStudio.Desktop/version.txt`.
- Templates indicam sua natureza na extensão: `Tp02PgLab.cs.in` contém C# usado para gerar `TP02PgLab.build.cs`.

## Organização atual

`src/OpenLadderStudio.Core/` contém o núcleo independente da interface. `src/OpenLadderStudio.Desktop/` contém a aplicação Windows e sua cadeia de compilação atual, incluindo ferramentas e dependências do PC12 clássico. Essa mudança de diretório não extraiu as responsabilidades internas: a separação em Application, drivers e UI continua descrita no [guia de desenvolvimento](development-guide.md).

Os scripts `Prepare*VNN.ps1` existentes são etapas encadeadas de transformação dos fontes. Seus números distinguem etapas que ainda executam no build; removê-los sem substituir essa cadeia tornaria seus nomes ambíguos. A convenção sem versões vale para arquivos novos. Os executáveis e DLLs de terceiros mantêm seus nomes exigidos em tempo de execução.

## Principais mudanças

| Nome anterior | Nome atual |
|---|---|
| `PC12_v2.1_Windows7_v3_portatil/` (fontes e ferramentas) | `src/OpenLadderStudio.Desktop/` |
| `BUILD_INTERFACE_MODERNA.bat` | `Build.bat` |
| `BUILD_PG_LAB.bat` | `BuildTp02Lab.bat` |
| `INICIAR_PC12.bat` | `StartStudio.bat` |
| `INICIAR_PC12_CLASSICO.bat` | `StartClassicPc12.bat` |
| `INICIAR_EDITOR_LADDER.bat` | `StartLadderEditor.bat` |
| `INICIAR_CONTROLADORES.bat` | `StartDeviceManager.bat` |
| `INICIAR_MODBUS.bat` | `StartModbusMonitor.bat` |
| `INICIAR_CAPTURA_SERIAL.bat` | `StartSerialCapture.bat` |
| `INICIAR_SIMULADOR.bat` | `StartSimulator.bat` |
| `INICIAR_LINK_PG_V035/V037/V038.bat` | `StartTp02Link.bat` (um único lançador) |
| `RESETAR_ULTIMO_ARQUIVO.bat` | `ResetClassicPc12State.bat` |
| `LEIA-ME_WINDOWS_7.txt` | `legacy-pc12-notes.txt` |
| `TP02PgLabSource.txt` | `Tp02PgLab.cs.in` |
| `installer/PC12Studio.iss` | `installer/OpenLadderStudio.iss` |
| `docs/AUDITORIA_REPOSITORIO.md` | `docs/repository-audit.md` |
| `docs/DEVELOPMENT_GUIDE.md` | `docs/development-guide.md` |
| Demais documentos em `MAIUSCULAS_COM_SUBLINHADO.md` | `minusculas-com-hifen.md` |

## Compatibilidade com versões instaladas

Aplicações já distribuídas consultam URLs raw do GitHub dentro do diretório antigo. Por isso, `PC12_v2.1_Windows7_v3_portatil/` mantém somente `version.txt`, `TP02-PG-Tests.json` e uma explicação dessa compatibilidade. Os arquivos canônicos ficam no módulo Desktop; as cópias públicas devem ser idênticas.

Depois de alterar a versão ou o pacote de testes, execute na raiz:

```powershell
.\scripts\SyncCompatibilityMetadata.ps1
.\scripts\ValidateProject.ps1
```

O validador executado no CI rejeita cópias ausentes ou diferentes. Novos builds usam os caminhos novos; os antigos continuam encontrando os metadados nas URLs originais.

## Compilar

Na raiz do repositório, em Windows com .NET Framework:

```powershell
.\scripts\ValidateProject.ps1
cmd /c .\src\OpenLadderStudio.Desktop\Build.bat
cmd /c .\src\OpenLadderStudio.Desktop\BuildTp02Lab.bat
```

Os caminhos relativos do núcleo, autotestes, workflows, análise de protocolo e instalador acompanham o novo diretório. Os lançadores também podem ser executados a partir de outro diretório, pois usam sua própria localização como base.
