# Auditoria do repositório

Levantamento de código morto, duplicação e nomenclatura na base atual. Cada item traz como foi verificado, para que a conclusão possa ser refeita depois.

## Método

Um arquivo é considerado **morto** quando nada o alcança: nenhum `.bat`, nenhum `Prepare*.ps1`, nenhum `new Classe(...)` e nenhum passo do workflow de CI. As duas armadilhas deste repositório:

- o build está em **três** lugares — `BUILD_INTERFACE_MODERNA.bat`, os encadeamentos dentro de `PrepareUpdateNotification.ps1` e de `PreparePgLinkV39.ps1`, e o CI, que também chama `BUILD_PG_LAB.bat`. Olhar só o `.bat` principal produz falso positivo;
- um `.cs` pode não aparecer no `.bat` e ainda assim ser usado, porque um script o lê e grava outro nome (`ModbusMonitorV14.cs` → `ModbusMonitorV15.build.cs`).

## Código morto confirmado

| Item | Verificação |
|---|---|
| `UiStartupSelfTest.cs` | não citado em nenhum `.bat`, `.ps1` ou workflow |
| `PrepareLadderZoomV74.ps1` | não invocado por ninguém |
| `PrepareLadderZoomV74Fix.ps1` | invocado **apenas** pelo `V74.ps1`, que é morto — o par cai junto. A cadeia viva é `V74Fix3` → `V74Fix2` |
| `PrepareVisualStudioV68.ps1` | substituído por `PrepareVisualStudioLightV68.ps1`; nada o invoca |
| `PreparePgLinkV41.ps1` | só o `BUILD_LINK_PG_V041.bat`, que não está no CI nem é referenciado |
| `BUILD_LINK_PG_V035/V037/V038/V041.bat` | quatro `.bat` de build de gerações antigas, fora do CI |
| `TP02PgLinkV32Form`, `TP02PgLinkV33Form` | **compilados dentro do `OpenLadderStudio.exe`** e instanciados por ninguém: 1 035 linhas de formulário morto no binário entregue |

## Duplicação

**Cinco gerações do mesmo utilitário TP02 PG são compiladas no shell**: `TP02PgLinkV32/V33/V34/V35/V37.cs`, cerca de 2 700 linhas. Delas, V32 e V33 são inalcançáveis (acima); V35 e V37 têm `Main` próprio; V34 é aberto por um script de preparação. Cada geração é uma cópia com ajustes da anterior — corrigir um defeito de protocolo exige repetir a correção em até cinco arquivos.

**Três lançadores para o mesmo executável**: `INICIAR_LINK_PG_V035.bat`, `INICIAR_LINK_PG_V037.bat` e `INICIAR_LINK_PG_V038.bat` abrem todos o mesmo `OpenLadderTP02PgLink.exe`. Os números no nome sugerem versões diferentes que não existem.

## Peso do repositório

O legado do PC12 original ocupa **7,0 MB** em 11 arquivos versionados:

```
pc12.exe            2,5 MB     PC12HELP.HLP   1,6 MB
Tp022.hlp           1,6 MB     OWL52F.DLL     0,9 MB
cw3230.dll          0,3 MB     bwcc32.dll     0,2 MB
BDS52F.DLL           83 KB     HELPDLG.HLP    8,0 KB
pc12help.CNT        836 B      lastfile.cpu/.dir  20 B
```

São o software WEG original e o runtime Borland. **O instalador não os distribui**; o único acesso é `INICIAR_PC12_CLASSICO.bat`, que roda `pc12.exe` direto da pasta. `lastfile.cpu` e `lastfile.dir` são estado de execução daquele programa, não fonte — não deveriam estar versionados em nenhuma hipótese.

## Nomenclatura

O esquema atual identifica arquivos por **número de iteração**, não por responsabilidade: `PrepareUiAuditV51`, `PrepareUiAuditV52`, `PrepareUiFixV53`, `PrepareUiPolishV54`, `PrepareUiConsistencyV55`, `PrepareUiEfficiencyV56`. O nome diz *quando* a mudança entrou, e não *o que* o script faz — para saber, é preciso abrir. Com 50 scripts, encontrar onde uma regra vive passa a depender de `grep`.

Pior: `PrepareLadderZoomV74`, `...V74Fix`, `...V74Fix2`, `...V74Fix3` — quatro arquivos para um recurso, sendo dois mortos, e o nome não diz qual é o vivo.

O mesmo vale para `ModernPC12.cs` e `PC12Studio.cs`: dois shells legados cujos nomes não distinguem um do outro.

Não recomendo renomear agora: **os nomes são âncoras textuais** de outros scripts e do `.bat`, e uma renomeação em massa é exatamente o tipo de mudança que a cadeia não tolera. A recomendação é congelar o esquema (parar de criar `VNN`) e nomear por responsabilidade daqui em diante.

## Recomendações, por risco

**Sem risco — remover:**

1. os sete itens da tabela de código morto;
2. `lastfile.cpu` e `lastfile.dir` do controle de versão (acrescentar ao `.gitignore`);
3. `TP02PgLinkV32.cs` e `TP02PgLinkV33.cs` da linha de compilação do `OpenLadderStudio.exe`.

**Risco baixo — decidir:**

4. mover o legado PC12 (7 MB) para fora do repositório, ou para uma release de arquivo. Quem clona hoje baixa 7 MB de binário de 1995 que o produto não distribui;
5. reduzir os três `INICIAR_LINK_PG_*` a um único lançador.

**Risco médio — planejar:**

6. consolidar as cinco gerações do TP02 PG em uma, com o que sobrou de útil das anteriores;
7. congelar o esquema de nomes `VNN` e nomear por responsabilidade.
