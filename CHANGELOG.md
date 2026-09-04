# Changelog

Todas as mudanças relevantes do OpenLadder Studio são registradas neste arquivo.

## [0.22] - 2026-09-04

### Interface
- nova identidade do ícone do aplicativo, com desenho Ladder/PLC mais limpo;
- geração de ICO multirresolução em 16, 24, 32, 48, 64, 128 e 256 px;
- paleta semântica de cores para ícones da barra superior, navegação e abas;
- melhor contraste do tema escuro;
- correção do posicionamento dos rótulos na barra compacta;
- foco visual e acionamento por teclado nos botões principais.

### Engenharia de software
- `version.txt` passa a ser a fonte principal da versão exibida pelo shell;
- instalador transformado em template e preparado automaticamente a partir de `version.txt`;
- validação automática de metadados e estrutura antes do build;
- release passa a usar este changelog como fonte das notas;
- inclusão de `.editorconfig`, documentação de arquitetura e guia de interface;
- arquivos gerados foram separados de forma mais clara no `.gitignore`.

### Instalação e atualização
- preservado o comportamento que fecha o OpenLadder Studio durante a atualização;
- se o aplicativo estava aberto antes da instalação, ele é reaberto ao término;
- se estava fechado, não é aberto automaticamente em instalação silenciosa.

## [0.21] - 2026-09-04

- ícones coloridos na barra superior;
- tratamento de fechamento e reabertura do aplicativo durante atualização;
- correções no fluxo de atualização automática.

## [0.20] - 2026-09-04

- área Ladder ampliada;
- interface mais compacta;
- painéis auxiliares recolhidos por padrão;
- modo foco com F11;
- consolidação da arquitetura multi-PLC e ferramentas Modbus.
