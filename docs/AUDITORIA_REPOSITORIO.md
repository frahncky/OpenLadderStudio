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

Renomear um script **é** seguro. As âncoras da cadeia são trechos de código C# *dentro* dos scripts, não os nomes dos arquivos; o nome só aparece como referência no `.bat`, nos encadeamentos e no workflow, e o build acusa qualquer referência esquecida.

O que **não** dá para consertar por renomeação: os scripts `V51`–`V56` não têm uma responsabilidade. Cada um faz quatro ou cinco coisas sem relação entre si — o `V51`, por exemplo, mexe em toolbar, painel lateral, desenho da bobina, Refazer e lista de elementos. São *changesets* de uma iteração, não módulos. Chamá-los de "Audit", "Fix", "Polish", "Consistency" ou "Efficiency" atribuía a eles uma responsabilidade inexistente: o nome passava informação falsa.

### Padrão adotado

```text
Prepare<Área>V<NN>[a|b].ps1
```

Área (`Ui`, `Ladder`, `Modbus`, `Tp02`, `Workspace`, `Theme`…) mais o número da iteração em que a mudança entrou, sem qualificadores de qualidade. Quando uma iteração precisa de mais de um script, sufixo de letra na ordem de execução — `Fix2`/`Fix3` não diziam qual era o vivo.

Já renomeados: `PrepareUiAuditV51` → `PrepareUiV51`, e o mesmo para `V52`–`V56`; `PrepareLadderZoomV74Fix2` → `PrepareLadderZoomV74a`; `...Fix3` → `...V74b`.

### O que continua ruim

`ModernPC12.cs` e `PC12Studio.cs` são dois shells legados cujos nomes não distinguem um do outro. Renomear mexe na linha de compilação e nos scripts que os leem pelo nome — vale fazer junto com a decisão de qual dos dois sobrevive, não antes.

A pasta `PC12_v2.1_Windows7_v3_portatil` carrega versão, sistema operacional e o adjetivo "portátil" de um produto que já não é o produto. É o pior nome do repositório. Renomeá-la atinge o workflow de CI, os dois arquivos do instalador, `scripts/`, o `.gitignore` e boa parte da documentação: mecânico, mas com o pipeline de release no caminho, então merece ser uma mudança isolada.

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
