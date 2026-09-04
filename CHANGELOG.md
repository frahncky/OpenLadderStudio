# Changelog

Todas as mudanças relevantes do OpenLadder Studio são registradas neste arquivo.

## [0.24] - 2026-09-04

### Correção
- cinco formulários não declaravam `AutoScaleMode` e, com o reconhecimento de DPI
  introduzido na v0.23, passavam a ser desenhados menores do que deveriam em telas
  com escala: gerenciador de controladores (duas janelas), monitor Modbus, histórico
  de tendências e mapa de memória;
- todos os formulários passam a usar `AutoScaleMode.Dpi`.

### Engenharia de software
- `ValidateProject.ps1` passa a recusar qualquer formulário sem `AutoScaleMode`.

## [0.23] - 2026-09-04

### Interface
- a janela deixa de ser ampliada como bitmap em telas com escala de 125%, 150% ou 200%;
  o manifesto passa a declarar reconhecimento de DPI do sistema e a interface fica nítida;
- erros inesperados passam a exibir uma mensagem em vez de encerrar o aplicativo em silêncio.

### Confiabilidade
- tratamento global de exceções instalado nos seis executáveis;
- falhas são registradas em `%APPDATA%\OpenLadder Studio\logs`, com data, versão,
  sistema operacional e pilha de chamadas, para permitir diagnóstico e reporte;
- a mensagem de erro informa o caminho do registro gerado.

### Engenharia de software
- `ValidateProject.ps1` passa a exigir que todo executável embuta o manifesto de DPI e
  o módulo de diagnóstico, para que a correção não se perca em alterações futuras.

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
