# Contribuindo com o OpenLadder Studio

## Fluxo recomendado

1. criar uma branch a partir de `main`;
2. fazer alterações pequenas e coerentes por responsabilidade;
3. executar `scripts/ValidateProject.ps1`;
4. executar o build principal;
5. abrir Pull Request para `main`;
6. integrar somente após o GitHub Actions concluir com sucesso.

## Regras de arquitetura

- UI não deve conter lógica de protocolo quando ela puder ficar em driver/serviço;
- núcleo e modelo Ladder não devem depender de WinForms;
- novos fabricantes devem entrar por perfis/contratos, não por condicionais espalhadas na interface;
- escrita em PLC deve permanecer separada de leitura e exigir validação específica;
- não criar novos arquivos com sufixo de versão quando um arquivo estável puder ser evoluído.

## Versionamento

`PC12_v2.1_Windows7_v3_portatil/version.txt` é a fonte principal da versão da aplicação.

Não inserir manualmente números de versão em novos arquivos. O shell e o instalador devem derivar a versão desse arquivo por scripts de build.

## Interface

Seguir `docs/UI_GUIDELINES.md`.

- preservar a identidade OpenLadder;
- usar cores semânticas de forma consistente;
- manter foco de teclado e contraste;
- evitar duplicação de informações na mesma tela.

## Commits

Mensagens devem explicar a intenção, por exemplo:

- `Melhorar navegação lateral`
- `Separar validação de perfil PLC`
- `Corrigir preparação do instalador`

Evitar mensagens genéricas como `update`, `fix` ou `alterações`.

## Arquivos gerados

Não versionar executáveis, arquivos `*.build.cs`, instalador temporário ou saída de build. O `.gitignore` deve cobrir esses artefatos.
