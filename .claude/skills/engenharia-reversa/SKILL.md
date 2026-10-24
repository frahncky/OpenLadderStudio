---
name: engenharia-reversa
description: Orquestra o processo completo de engenharia reversa de protocolo de PLC no OpenLadder Studio (análise estática → emulação offline → roteiro de bancada → integração como driver), delegando cada etapa ao agente especialista e fazendo os commits e PRs. Use quando o usuário pedir para continuar, terminar ou retomar a engenharia reversa do TP02, ou começar a de um modelo de PLC novo. Argumento opcional: o modelo ou a pergunta a responder.
---

# Orquestração da engenharia reversa

Você, a sessão principal, coordena. Cada etapa vai para um agente especialista, porque um subagente não consegue chamar outro. Rode as etapas em sequência: cada uma consome a saída da anterior.

## 0. Ponto de partida

1. Leia a seção "Estado de evidência" de `docs/TP02_PG_LATEST.md` e o fim de `docs/pc12-v2.1-static-inventory.md`. Para um modelo novo, comece do zero em `docs/<modelo>-static-inventory.md`.
2. Monte a **lista de perguntas abertas**. Classifique cada uma como *estática* (o software do fabricante responde) ou *de firmware* (só o PLC responde).
3. Crie um branch `re/<tema>-<data>` a partir do `main` atualizado. Se o working tree tiver alterações do usuário, use `git worktree` em vez de `switch`, para não tocar nelas.

## 1. Análise estática: agente `re-estatico`

Passe as perguntas estáticas. Ele devolve as descobertas com evidência REA e o nível de confiança de cada uma, além das **perguntas para a bancada**. Se o REA ou o Ghidra faltarem em `artifacts/`, pare e peça ao usuário.

## 2. Emulação e regressão: agente `re-emulacao`

Passe as descobertas classificadas como RESOLVIDO ou EVIDÊNCIA FORTE. Ele cria as regras de emulador e os testes, e roda a matriz offline, os golden vectors e os três builds de laboratório. **Gate:** só avance se tudo sair com código 0. Se algo falhar, volte ao passo 1 com o caso que falhou, no máximo duas vezes, e depois pare e relate.

## 3. Bancada: agente `re-bancada` (gate humano obrigatório)

Se houver perguntas de firmware, ele prepara o roteiro e o confere no emulador. **A execução no PLC é do usuário.** Mostre o roteiro, pare e espere a captura. Não existe modo autônomo para esta etapa, nem se o usuário pediu "tudo automático": nesse caso, explique a trava em uma linha e siga com o que não depende do PLC. Quando o usuário trouxer a captura, chame `re-bancada` de novo para analisá-la e depois `re-emulacao` para ajustar regras e testes.

## 4. Integração no produto: agente `driver-plc`

Só para o que estiver CONFIRMADO EM BANCADA, ou RESOLVIDO com trava de transmissão. Ele integra pelo `IPlcDriverUi`, sem nome de modelo no shell. Uma tela nova passa também pelo agente `interface`, e o texto pelo `portugues`.

## 5. Fechamento (você)

1. **Validação completa:**
   - `powershell -ExecutionPolicy Bypass -File scripts/ValidateProject.ps1`;
   - `Build.bat`;
   - `BuildTp02Emulator.bat`, `BuildTp02Lab.bat` e `BuildTp02FullCapture.bat`;
   - `RunTp02OfflineLab.bat`.
2. **Commits** por etapa (docs, emulador/testes, driver), sem `.exe`, `.png`, `*.build.cs` nem `version.txt`.
3. **Push** do branch e PR contra o `main`.
4. **CI:** espere `build` e `validate` ficarem verdes.
5. **Merge e release** só com autorização explícita do usuário. Um push no `main` publica uma release; se ela for pedida, use o agente `release`.
6. **Relatório ao usuário:** o que foi resolvido em cada nível, o que ficou pendente de bancada, os links e o status do CI.

## Nunca

- Abrir porta COM física ou transmitir ao PLC, em nenhuma etapa.
- Promover uma hipótese a regra, a asserção de teste ou a byte transmitido.
- Afrouxar os gates de segurança (ACK exato, transmissão única, recusa em RUN, bloqueio de `0F 00 F0`).
- Mergear sem CI verde, ou sem o usuário pedir.
