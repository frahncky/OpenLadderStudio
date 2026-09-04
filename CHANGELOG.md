# Changelog

Todas as mudanças relevantes do OpenLadder Studio são registradas neste arquivo.

## [0.28] - 2026-09-04

### Comunicação WEG TP02
- as linhas DTR e RTS passam a ser controláveis e vêm ativas por padrão. Antes ficavam
  em `false`, o padrão do .NET, e cabo de programação opto-isolado costuma se alimentar
  delas: sem isso o quadro sai pela porta e nada retorna;
- a leitura passa a preservar respostas incompletas em vez de descartá-las. Receber
  alguns bytes sem `<CR>` distingue "nada respondeu" de "respondeu com baud, paridade
  ou bits divergentes" — causas com correções diferentes;
- o registro passa a mostrar os parâmetros seriais em uso, o estado de DTR e RTS, e o
  conteúdo em hexadecimal além do texto, para diagnosticar sem depender de suposição;
- as três melhorias valem tanto para a tela de Comunicação quanto para a leitura de
  programa por RBP.

## [0.27] - 2026-09-04

### Correção
- as barras de título e de rodapé deixam de ser desenhadas por cima do conteúdo.
  A ancoragem do WinForms é resolvida do último filho para o primeiro, e chamar
  `BringToFront()` em uma barra ancorada faz o painel principal ocupar a área inteira
  antes dela. Na tela de controladores isso escondia a linha de títulos das colunas
  e cortava a primeira linha da lista;
- a regra foi aplicada aos 15 formulários do software, não só ao gerenciador;
- o botão EXCLUIR deixa de ser cortado na borda: os botões do cabeçalho passam a ser
  posicionados pela própria largura, e não por deslocamentos fixos em pixels;
- o painel de detalhes deixa de cortar o texto: a divisória passa a ser posicionada
  depois do layout e proporcional à fonte, e os rótulos acompanham a largura do painel.

### Engenharia de software
- `ValidateProject.ps1` passa a recusar `BringToFront()` em barra ancorada.

## [0.26] - 2026-09-04

### Correção
- o escalonamento por DPI passa a funcionar de fato. Os formulários declaravam
  `AutoScaleMode.Dpi` mas nunca definiam `AutoScaleDimensions`, e sem essa dimensão
  de referência o fator de escala do WinForms é 1: a declaração não tinha efeito algum;
- em telas com escala, as fontes cresciam por serem definidas em pontos, enquanto os
  controles permaneciam no tamanho de 96 DPI, causando texto transbordando das caixas
  em toda a interface;
- os 17 formulários do software passam a declarar `AutoScaleDimensions` de 96 DPI.

### Engenharia de software
- `ValidateProject.ps1` passa a recusar `AutoScaleMode` sem `AutoScaleDimensions`.

## [0.25] - 2026-09-04

### Correção
- corrigida a sobreposição entre o cabeçalho de colunas e a primeira linha das listas
  em telas com escala: a faixa de cabeçalho tinha altura fixa em pixels enquanto a
  fonte, definida em pontos, crescia com o DPI;
- cabeçalho e linhas dos sete grids do software passam a se ajustar ao conteúdo:
  controladores, mapa de memória, monitor Modbus, decodificador, calibração,
  campanha de calibração e decodificador de máquina.

### Engenharia de software
- `ValidateProject.ps1` passa a recusar `DataGridView` sem `ColumnHeadersHeightSizeMode`.

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
