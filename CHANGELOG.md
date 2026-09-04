# Changelog

Todas as mudanças relevantes do OpenLadder Studio são registradas neste arquivo.

## [0.32] - 2026-09-04

### Protocolo PG do PC12
- a comunicação do TP02 deixa de usar Computer Link como caminho principal para o cabo de programação validado pelo PC12 original;
- engenharia reversa estática do `pc12.exe` confirmou uma camada PG própria, diferente dos comandos ASCII `PSR/MRV/SCS/WRV/RUN/STP` do Host Protocol;
- o primeiro handshake do PC12 foi identificado exatamente como `43 4F 4E 2D 49 43 42 0D`, correspondente a `CON-ICB<CR>`;
- o segundo quadro de identificação usado pelo PC12 foi identificado como `F0 00 0F`;
- a rotina original força 19200 bps, 8 bits e 1 stop bit e valida quadros binários pela regra de checksum em que a soma dos bytes módulo 256 resulta em `FF`;
- nova tela **Link PG - WEG TP02** testa os perfis 19200/8O1 e 19200/8N1, com fallback de DTR/RTS, registra TX/RX em hexadecimal e informa se o Link PG foi confirmado;
- a porta COM utilizada é preservada nas configurações do OpenLadder Studio.

### Segurança operacional
- a v0.32 é deliberadamente limitada ao handshake e identificação PG;
- `RUN`, `STOP`, escrita de registradores, escrita de bobinas, download e apagamento de programa não são enviados pelo novo módulo;
- os comandos que alteram o PLC só serão incorporados ao protocolo PG depois da confirmação física do Link com o mesmo PLC/cabo que já funciona no PC12.

## [0.31] - 2026-09-04

### Comunicação WEG TP02
- corrigida a premissa da v0.30 que tratava a porta MMI como Computer Link sem verificar o modo elétrico PG/COM;
- a tela de Comunicação passa a identificar explicitamente que o protocolo operacional usa **MMI Computer Link**, distinto do modo PG usado pelo PC12 para programação;
- os quadros de Computer Link passam a usar o prefixo `::` de forma fixa;
- configuração inicial alterada para 19200 bps, 7 bits, sem paridade, 1 stop bit, estação 01 e resposta 4, mantendo perfis alternativos na autodetecção;
- nova **AUTO-DETECÇÃO TP02** varre as estações 01 a 99 e testa perfis seriais comuns, além de uma segunda tentativa com DTR/RTS ativos para conversores que dependam dessas linhas;
- a autodetecção diferencia três situações: resposta TP02 válida, bytes recebidos sem quadro válido e **zero bytes**;
- quando todas as estações e perfis retornam zero bytes, o diagnóstico orienta verificar o cabo/conversor e o modo da porta MMI: Computer Link requer PG/COM baixo, com o pino 4 ligado ao pino 5;
- parâmetros encontrados são aplicados e armazenados automaticamente.

### Segurança operacional
- `RUN`, `STP`, `SCS` e `WRV` permanecem desabilitados até o recebimento de um `PSR` válido com checksum correto;
- comandos de escrita e RUN/STOP deixam de ser enviados quando não existe comunicação confirmada;
- `CLR`, `WBP`, `ROM`, apagamento de memória e download de programa continuam bloqueados.

## [0.30] - 2026-09-04

### Comunicação e controle WEG TP02
- nova tela **Controle online - WEG TP02**, integrada ao item Comunicação do OpenLadder Studio;
- leitura do estado do PLC por `PSR`, de bobinas/relés por `MCR` e de registradores por `MRV`;
- escrita de bobinas/relés `Y`, `C` e `SC` por `SCS`, com leitura de confirmação após a operação;
- escrita de uma palavra em registradores `V`, `D`, `WS`, `WC` e `F` por `WRV`, com leitura de confirmação após a operação;
- comandos `RUN` e `STP` (STOP) com confirmação explícita e nova leitura `PSR` para verificar o estado resultante;
- registro técnico dos quadros TX/RX em texto e hexadecimal, checksum e mensagens de erro do protocolo;
- parâmetros de porta, baud, bits, paridade, stop bits e estação são preservados nas configurações do controlador.

### Segurança operacional
- comandos que alteram bobinas, registradores ou o estado RUN/STOP exigem confirmação antes do envio;
- a interface alerta que os comandos atuam no PLC físico e podem alterar saídas ou movimentar a máquina;
- `CLR`, `WBP`, `ROM`, apagamento de memória e download de programa continuam deliberadamente fora da tela operacional.

## [0.29] - 2026-09-04

### Comunicação WEG TP02
- nova varredura automática de parâmetros na tela de Comunicação. O botão VARRER
  PARÂMETROS percorre 144 combinações de baud rate, paridade, bits de dados, stop bits
  e prefixo de quadro, e para na primeira que obtiver resposta do PLC;
- ao encontrar, os parâmetros são aplicados aos campos da tela, prontos para uso;
- a varredura roda em thread própria, com botão de parada e progresso no registro, de
  modo que a janela continua respondendo durante os testes;
- silêncio em todas as combinações é informação útil e passa a ser dito de forma
  explícita: indica elo físico ou estação divergente, não parâmetro serial.

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
