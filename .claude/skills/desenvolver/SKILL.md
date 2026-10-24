---
name: desenvolver
description: Orquestra o desenvolvimento de uma melhoria ou funcionalidade do OpenLadder Studio (interface, editor Ladder, simulação, drivers de PLC, build) de ponta a ponta — planejamento, delegação aos agentes especialistas, verificação independente pelo agente revisor, commits, PR e CI. Use quando o usuário pedir uma mudança no produto que envolva mais de um arquivo ou mais de uma área. Argumento: a descrição da mudança.
---

# Orquestração do desenvolvimento

Você, a sessão principal, coordena. Os especialistas implementam e o `revisor` verifica. Uma etapa só começa depois que a anterior foi validada.

## 1. Entender e planejar

1. Se o pedido for amplo ("melhorar a interface"), pergunte a prioridade e o escopo com `AskUserQuestion` antes de mexer. Quando o pedido for específico, siga direto.
2. Divida em etapas pequenas, cada uma com um especialista responsável:

| Área | Agente |
|---|---|
| Cor, tema, ícone, layout, DPI, telas | `interface` |
| Editor Ladder, varredura, simulação, plantas | `ladder-plc` |
| Conectar, ler, transferir, monitorar, modelo de PLC novo | `driver-plc` |
| Protocolo ainda desconhecido | skill `engenharia-reversa` |
| `Prepare*.ps1`, `Build.bat`, erro de build | `build-chain` |
| Texto, acento, encoding | `portugues` |
| Versão, notas, instalador, CI | `release` |

3. Crie um branch `feat/…`, `fix/…` ou `ui/…` a partir do `main` atualizado. Se o working tree tiver alterações do usuário, use `git worktree` e não toque nelas.

## 2. Implementar

Para cada etapa, chame o especialista com: o objetivo, os arquivos prováveis, as regras do CLAUDE.md e "não faça commit". Peça que ele valide com `Build.bat` e `ValidateProject.ps1`. Quando várias etapas não mexem nos mesmos arquivos, elas podem rodar em paralelo.

**Lição deste projeto:** o shell que o usuário vê é o `UniversalStudioShell.build.cs`, gerado por mais de 40 `Prepare*.ps1`. Uma mudança na interface do shell entra como script novo no fim da cadeia (padrão `PrepareGenericInterfaceV85`, `V86`, `V87`), e não reescrevendo âncoras dos scripts anteriores. O script tolera CRLF e aborta se a âncora sumir.

## 3. Verificar (obrigatório)

Chame o `revisor` sobre o resultado.
- Se ele der **BLOQUEADO**, devolva os achados ao especialista e chame o `revisor` de novo, no máximo três rodadas. Depois disso, pare e relate ao usuário.
- Se a mudança toca a interface, olhe você mesmo pelo menos uma das imagens do `scripts/RenderUi.ps1` antes de seguir.

## 4. Entregar

1. Faça os commits por etapa, com o trailer pedido pelo ambiente. Não incluem `.exe`, `.png`, `*.build.cs`, `artifacts/` nem `version.txt`.
2. Faça push do branch, abra o PR contra o `main` e espere `build` e `validate` ficarem verdes. Se o CI falhar, leia o log (`gh run view <id> --log-failed`), corrija e repita.
3. **Não faça merge sem o usuário pedir.** Um merge no `main` publica uma release, e a mudança só chega aos usuários com aumento de versão (agente `release`).
4. No relatório ao usuário, diga:
   - o que mudou e onde;
   - o que ficou de fora e por quê;
   - o que não foi testado (hardware, tema, DPI);
   - o link do PR e o status do CI.
