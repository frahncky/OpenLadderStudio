# OpenLadder Studio — Auditoria de UI/UX v0.52

## Objetivo

Consolidar o OpenLadder Studio como um IDE Ladder moderno, intuitivo e coerente, sem duplicar comandos entre toolbar e painel lateral e mantendo compatibilidade com Windows 7/WinForms.

## Problemas identificados na v0.51

- toolbar de 48 px recebia botões de 54 px, causando risco de clipping visual;
- referências antigas a `v0.12` ainda apareciam em Sobre/status;
- `Configurações` abria o seletor de controlador, o que era semanticamente incorreto;
- o painel lateral único era uma lista longa: era necessário rolar todos os Elementos para chegar em Propriedades;
- Elementos não possuía busca/filtro;
- Propriedades mostrava a seleção, mas não oferecia uma ação direta para editar o elemento;
- o console de saída iniciava ocupando espaço do editor mesmo sem haver uma necessidade imediata;
- o símbolo interno da marca não reutilizava o ícone real do executável;
- o menu ainda usava o nome antigo `Painel de navegação`;
- a ajuda do editor standalone não citava o novo atalho `Ctrl+Y`.

## Correções implementadas

### Toolbar

- altura e botões compatibilizados para eliminar clipping;
- somente comandos globais permanecem no topo;
- fluxo final: Novo, Abrir, Salvar, Desfazer, Refazer, Validar, Conectar, Monitor, Ler PLC, Controlador e Atualizar;
- `Adicionar linha` e `Remover linha` permanecem exclusivamente em Elementos;
- tooltips e nomes acessíveis adicionados aos botões;
- `Configurações` foi substituído por `Controlador`, refletindo a função real.

### Painel lateral único

- Projeto fixo no topo;
- Propriedades fixas na parte inferior;
- Elementos ocupa somente a região central e é a única área que rola;
- campo `Buscar elemento...` filtra a biblioteca Ladder em tempo real;
- largura balanceada para preservar área útil do editor;
- a antiga noção de painel lateral direito continua removida.

### Propriedades

- seleção atual permanece sempre visível;
- ferramenta ativa permanece sempre visível;
- novo botão `Editar elemento selecionado` reutiliza o editor nativo de parâmetros do Ladder.

### Identidade visual

- o cabeçalho lateral usa o ícone real associado ao executável, mantendo consistência com o ícone do aplicativo/instalador;
- versão exibida é derivada de `version.txt`, removendo referências antigas;
- terminologia de menu atualizada para `Painel lateral`.

### Área de trabalho

- console de saída inicia recolhido para priorizar o canvas Ladder;
- continua acessível em `Exibir`;
- editor standalone atualiza a ajuda para `Ctrl+Z`, `Ctrl+Y` e `Del`.

## Princípios mantidos

- nenhuma função de edição de linha fica na toolbar superior;
- painel lateral não repete as ações globais do topo;
- bobina permanece com símbolo IEC corrigido;
- recursos específicos do TP02 continuam disponíveis pelos menus/ferramentas correspondentes;
- compatibilidade com a base WinForms e Windows 7 é preservada.

## Próximas melhorias recomendadas

- inspector editável em linha para endereço/preset sem abrir diálogo;
- atalhos configuráveis;
- abas com overflow/scroll quando muitos documentos estiverem abertos;
- escala de densidade compacta/confortável;
- tema claro/escuro configurável mantendo a paleta industrial;
- painel de diagnóstico contextual para PLC conectado;
- estados de conexão globais sincronizados entre comunicação, monitor e status bar.
