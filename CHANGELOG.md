# Changelog

Todas as mudanças relevantes do OpenLadder Studio são registradas neste arquivo.

## [0.82] - 2026-09-07

### Editor Ladder
- arrastar elemento entre células, com desfazer, limiar de 4 px e destino marcado durante o movimento;
- botão direito sobre um elemento abre menu com Editar parâmetro, Copiar, Colar e Apagar; com ferramenta armada ele continua soltando a ferramenta;
- símbolos padrão no lugar de caixas de texto com o código TP02: `SET` → `-( S )-`, `RESET` → `-( R )-`, borda de subida → `-| P |-`, borda de descida → `-| N |-`. O retângulo fica reservado a TMR, CNT, função e END;
- corrigido o hit-testing divergente entre hover e seleção: `CanvasMouseMove` usava `LogicalCanvasWidth` e trilho em `-30`, `SelectFromPoint` usava `ClientSize` com piso 850 e trilho em `-28`, e a detecção de via ainda usava `y = rungTop + 40` enquanto o desenho já passara para `+ 44`.

### Controlador
- o perfil de partida passa a ser o PLC virtual, não `weg.tp02.60mr`: o produto abria apontado para um controlador que o usuário não escolheu;
- o submenu **Diagnóstico avançado TP02** passa a seguir o controlador ativo, em vez de aparecer sempre e recusar depois do clique.

### Interface
- a aresta inferior da barra superior passa a usar `OpenLadderPalette.HeaderLine`, um passo mais forte que as divisórias internas.

### Manutenção
- removidas 1 732 linhas de código inalcançável, incluindo `TP02PgLinkV32.cs` e `TP02PgLinkV33.cs`, dois formulários compilados dentro do `OpenLadderStudio.exe` e instanciados por ninguém;
- `lastfile.cpu` e `lastfile.dir` saem do controle de versão;
- nomes dos scripts de preparação padronizados em `Prepare<Área>V<NN>[a|b].ps1`, sem qualificadores de qualidade;
- auditoria do repositório em `docs/repository-audit.md`.
## [0.81] - 2026-09-07

### Formato de projeto
- corrigida perda de projeto no formato `.pladder` v2: o til (`~`), que separa a via série da via paralela dentro da célula, é *unreserved* na RFC 3986 e passava sem escape por `Uri.EscapeDataString`. Um til no conteúdo gravava um separador a mais e o arquivo era recusado na leitura seguinte com "quantidade de ramificações inválida" — o projeto salvava e depois não abria;
- o til passa a ser gravado como `%7E`; arquivos já gravados seguem legíveis, porque `Uri.UnescapeDataString` sempre decodificou essa sequência;
- o autoteste do formato passa a cobrir os três separadores (`|`, `~` e `:`) dentro do conteúdo.

## [0.80] - 2026-09-06

### Grade do editor Ladder
- linhas identificadas por **L1, L2…** na lateral e colunas por **C1 a C8** no topo, no lugar de `001`/`002` e do rótulo único "SAÍDA";
- cabeçalho de colunas e calha de linhas ficam congelados: o cabeçalho acompanha só a rolagem horizontal e a calha só a vertical;
- a linha e a coluna da célula selecionada são destacadas nas duas réguas;
- linhas verticais deixam de ser pontilhadas, acompanham cada linha do programa e os segmentos encostam entre si, eliminando o tracejado nos limites;
- separador horizontal encosta nos dois trilhos;
- `GridLine` ganha contraste nos dois temas; no escuro a grade quase não aparecia;
- `TopMargin` sobe de 30 para 46 para abrir espaço ao cabeçalho fixo.

### Zoom do editor
- corrigido o laço que fazia as barras de rolagem piscarem: mostrar a barra vertical reduzia a largura útil, mudando a escala de ajuste, a altura do conteúdo e escondendo a barra de novo. A largura útil passa a reservar a calha da barra;
- a escala efetiva passa a valer no cálculo de rolagem, então o ponto central não salta mais ao dar zoom.

## [0.79] - 2026-09-06

### Interface
- refinados os temas claro e escuro com superfícies mais distintas, seleção azul e texto secundário mais legível;
- corrigidas as cores fixas de menus, painéis, rodapé e desenho Ladder, incluindo o editor executado separadamente;
- melhorado o contraste das cores de estado e do texto dos botões preenchidos;
- grades sem estilo próprio recebem linhas alternadas, separadores horizontais e cabeçalhos na paleta do tema;
- atualizado o guia de interface com as novas cores e regras de contraste.

## [0.78] - 2026-09-06

### Temas
- o produto passa a ter tema **escuro** (novo padrão) e **claro**, escolhidos em **Exibir → Tema**, com a preferência gravada em `%APPDATA%\OpenLadder Studio\tema.txt`;
- toda a cor passa a vir de uma paleta única, `OpenLadderPalette` em `AppBranding.cs`, compilada em todos os executáveis;
- corrigida a mistura de temas entre telas: o gerenciador de controladores, o mapa de memória, o monitor Modbus e o histórico de tendências continuavam na paleta escura legada com destaque verde, enquanto o editor Ladder, o atualizador e as ferramentas TP02 usavam uma terceira paleta com cabeçalho azul-marinho;
- campos de entrada, listas e grades que nasciam com a cor de sistema passam a seguir o tema;
- ícones ganham calibragem por tema, mantendo a família de matiz e o contraste mínimo em cada fundo;
- a moldura do simulador segue o tema; o sinóptico mantém fundo escuro nos dois, por convenção de sala de controle.

### Editor Ladder
- a ferramenta passa a ser de uso único: inserido o elemento, o mouse volta sozinho ao modo ponteiro, sem precisar clicar em **Selecionar**;
- `Ctrl` pressionado no clique mantém a ferramenta ativa para inserções em sequência;
- botão direito no diagrama e `Esc` soltam a ferramenta;
- o cursor vira cruz enquanto uma ferramenta está armada;
- inserção cancelada não consome a ferramenta.

### Português
- corrigida a corrupção de acentuação nos textos gerados no build: scripts de preparação sem BOM eram lidos como Windows-1252, e `•`/`—` chegavam à tela como `â€¢`/`â€"` no monitor Modbus, no aviso de nova versão e no painel do controlador;
- corrigido o defeito funcional decorrente: a limpeza do indicador de alteração no nome do projeto procurava um marcador corrompido e nunca encontrava;
- mensagens de erro do núcleo e o relatório do autoteste passam a sair acentuados.

### Documentação
- guia de interface reescrito em torno da paleta central e dos dois temas;
- guia de desenvolvimento passa a registrar as âncoras textuais dos scripts `Prepare*.ps1` e a exigência de BOM.

## [0.70] - 2026-09-06

### Normalização PT-BR
- a normalização passa a pular literais que não são texto de interface: URLs, identificadores de API em snake_case, padrões de expressão regular e caminhos técnicos;
- a corrupção de token técnico passa a ser evitada na origem; o reparo no build permanece apenas como rede de segurança;
- a varredura de tokens corrompidos passa a cobrir todos os fontes, não só o atualizador;
- corrigida a concordância de "download" traduzido: "Falha no download." gerava "Falha no transferência.".

### Atualização automática
- removida a ponte de compatibilidade das notas da v0.69: no corpo da release as aspas voltam escapadas, então a expressão regular do cliente antigo nunca casa;
- documentado que as versões 0.66, 0.67 e 0.68 exigem uma instalação manual, por terem sido publicadas com o atualizador corrompido.

## [0.67] - 2026-09-06

### Ferramentas e navegação
- o menu **Ferramentas** passa a exibir apenas funções de uso direto: simulação do projeto e verificação de compatibilidade com o controlador;
- recursos de engenharia reversa do WEG TP02 passam para **Diagnóstico avançado TP02**;
- o diagnóstico avançado fica restrito a teste de comunicação PG, monitor MMI em leitura, captura serial, decodificador RBP e análise PC12/TP02;
- **Calibração de opcodes** e **IL para Ladder** deixam de aparecer na interface normal por ainda serem recursos internos/incompletos;
- ferramentas externas específicas do TP02 só podem ser abertas quando um perfil TP02 está ativo.

### Comunicação TP02
- o comando **Conectar** deixa de abrir diretamente o laboratório de engenharia reversa;
- uma central separa claramente programação PG de MMI/Computer Link;
- o comando **Monitor** abre o TP02 em modo somente leitura, ocultando RUN/STOP e escritas no fluxo operacional comum.

### Instalação
- os executáveis técnicos continuam instalados, mas deixam de criar atalhos independentes no menu do Windows;
- o grupo de programas fica centrado no OpenLadder Studio.

## [0.66] - 2026-09-06

### Biblioteca de processos simulados
- cinco plantas novas: silo com enchimento e descarga, partida estrela-triângulo, cruzamento semafórico, elevador de carga de dois níveis e prensa com comando bimanual;
- cada planta traz física própria, falhas injetáveis, sinóptico e programa Ladder de exemplo comentado rung a rung;
- seleção de planta na janela de simulação, refazendo tabela de I/O, botoeiras, falhas e programa;
- modelo de cena no domínio: a planta descreve o próprio sinóptico em primitivas semânticas e a interface resolve escala e cores;
- contadores de falha que tornam visível a lógica malfeita: curto entre fases, conflito entre verdes, transbordo, colisão no fim de curso e descida com a cortina de luz interrompida.

### Editor Ladder
- contatos passam a aceitar `V0001`–`V0256`, que leem o bit de conclusão de TMR/CNT;
- `RESET` passa a aceitar `V0001`–`V0256`, zerando o acumulado do bloco;
- sem isso um temporizador podia ser inserido mas nunca usado, e nenhuma sequência temporizada era expressável.

### Engenharia de software
- autoteste ampliado para cobrir as seis plantas e o comportamento sob falha injetada;
- `ValidateProject.ps1` passa a exigir `SimulatedPlants.cs` e a bloquear dependência de WinForms nele;
- textos visíveis da simulação já escritos no padrão de linguagem do projeto, sem depender da normalização do build.

## [0.65] - 2026-09-06

### Interface e linguagem
- padronizados os textos visíveis para português consistente, usando “linha” no lugar de “rung”, “on-line/off-line” e “taxa de transmissão”;
- traduzidos os rótulos Modbus de leitura, identificação e parâmetros seriais, preservando nomes de protocolos e comandos técnicos;
- o antigo rótulo “TP02 BRIDGE LAB” passa a ser exibido como “LABORATÓRIO TP02”;
- referências de interface a “download” passam a usar “transferência” quando tratam do envio ou recebimento de programa.

### Engenharia de software
- a auditoria de português passa a bloquear também linguagem híbrida recorrente na interface;
- a normalização por palavra inteira corrige termos dentro de frases compostas sem alterar chaves técnicas internas;
- compilação, autotestes e geração do instalador continuam condicionados à aprovação da auditoria de linguagem.

## [0.64] - 2026-09-06

### Português e codificação
- revisão ampla dos textos visíveis do Studio, editor Ladder, atualizador, Monitor Modbus, mapa de memória e ferramentas TP02/PG;
- corrigidos textos corrompidos por interpretação incorreta de UTF-8, incluindo sequências como `Ã`, `Â` e símbolos quebrados;
- palavras exibidas sem acentuação passam por normalização no estágio final do build;
- notas das releases passam a ler o `CHANGELOG.md` explicitamente como UTF-8.

### Engenharia de software
- adicionada normalização central de textos imediatamente antes da compilação;
- nova auditoria de português bloqueia a publicação quando encontra texto de interface sem acento ou codificação corrompida;
- identificadores técnicos internos e chaves de configuração são preservados pela normalização.

## [0.63] - 2026-09-06

### Formato de projeto
- o codec do formato `.pladder` foi extraído para `OpenLadderStudio.Core`, sem dependência de WinForms ou de fabricante;
- editor Ladder e reconstrução segura de IL do TP02 passam a usar a mesma implementação para gravar projetos;
- mantida a abertura de projetos `PC12-LADDER|1` e a gravação em `PC12-LADDER|2`, incluindo ramificações paralelas;
- arquivos corrompidos passam a indicar a linha e a coluna da célula inválida, em vez de serem aceitos parcialmente.

### Engenharia de software
- iniciada a estrutura física `src/OpenLadderStudio.Core` prevista no plano de arquitetura;
- novo autoteste verifica ida e volta do formato, caracteres reservados, ramificações, migração da versão 1 e recusa de dados inválidos;
- o build e o GitHub Actions passam a executar `OpenLadderCoreTest.exe` antes de publicar a versão.

## [0.62] - 2026-09-05

### Atualização
- a atualização automática passa a exibir uma janela própria, visível também na barra de tarefas;
- o usuário acompanha as etapas de verificação da versão, download, validação do SHA-256 e fechamento para instalação;
- o aviso informa antecipadamente que o OpenLadder Studio será fechado e reaberto automaticamente;
- mensagens e estados do fluxo de atualização foram revisados para indicar claramente o que está acontecendo.

### Interface
- revisão final de termos exibidos, incluindo “Monitor on-line” e “Janela de atualização”.

## [0.61] - 2026-09-05

### Instalação e atualização
- o aviso de nova versão passa a iniciar diretamente o atualizador externo;
- o comando manual e a atualização automática passam a usar o mesmo fluxo;
- antes de abrir o instalador, o estado da sessão é salvo e o OpenLadder Studio recebe uma solicitação de fechamento gracioso;
- se o processo não encerrar dentro do prazo, o encerramento forçado é usado apenas como contingência;
- o instalador preserva a reabertura do aplicativo e a retomada da sessão ao final.

### Interface
- revisão de acentuação e clareza dos textos do shell, do atualizador e do editor Ladder.

## [0.60] - 2026-09-05

### Interface
- o botão “Conectar” deixa de aparecer permanentemente destacado;
- o destaque visual fica reservado aos estados reais de interação e conexão.

## [0.59] - 2026-09-05

### Interface
- os botões da barra superior passam a calcular a largura pelo texto real;
- removido o truncamento por reticências, inclusive no comando “Conectar”;
- dimensões mínimas foram reforçadas para permanecer legíveis em diferentes escalas de DPI;
- estabilizado o feedback de hover das células do editor Ladder.

## [0.58] - 2026-09-05

### Editor Ladder
- novo feedback visual ao passar o ponteiro sobre as células;
- a coluna destinada às saídas passa a ser identificada e diferenciada visualmente;
- o hover não interfere na seleção ativa do elemento.

### Atualização
- a verificação em segundo plano repete a consulta em caso de falha transitória de rede;
- nenhum aviso é exibido quando a versão instalada já é a mais recente.

## [0.57] - 2026-09-05

### Editor Ladder
- canvas redesenhado com mais espaço, guias discretas de coluna e trilhos mais definidos;
- numeração das linhas e seleção receberam hierarquia visual mais clara;
- contatos, bobinas, blocos e ramos paralelos ganharam desenho mais legível;
- removidas referências visuais remanescentes ao antigo “PC12 Ladder Studio”.

## [0.56] - 2026-09-05

### Interface e produtividade
- a barra superior foi simplificada, sem repetição do nome e da versão do aplicativo;
- a barra de status passa a mostrar apenas o contexto operacional;
- adicionados atalhos globais para novo, abrir, salvar, salvar como, desfazer, refazer, selecionar e apagar;
- os atalhos Ladder continuam funcionando mesmo quando o foco está no painel lateral.

## [0.55] - 2026-09-05

### Interface
- contatos, saídas, temporização, contagem, funções e operações de linha foram consolidados na lista “Elementos”;
- as propriedades da seleção ficaram mais enxutas;
- a tecla Delete passa a ser a ação visual única para excluir o componente selecionado;
- a linguagem apresentada ao usuário foi padronizada de “rung” para “linha”;
- ajuda e mensagens do editor foram simplificadas e alinhadas aos atalhos realmente disponíveis.

## [0.54] - 2026-09-05

### Interface
- a biblioteca lateral passa a separar os componentes Ladder por categorias;
- ações de seleção, edição e manutenção de linhas deixam de aparecer misturadas aos componentes;
- a janela “Sobre” foi reduzida à identificação útil do produto.

### Atualização
- o estado da sessão passa a ser salvo antes da abertura do atualizador;
- reforçado o fechamento do Studio e sua reabertura automática durante a instalação;
- o aviso de nova versão ganhou ancoragem mais estável e permanece visível até a ação do usuário.

## [0.53] - 2026-09-05

### Simulação de processo
- motor de varredura Ladder próprio, com imagem de processo, contatos, bobinas, SET/RESET, TMR, TMR retentivo, CNT, contatos especiais e END;
- planta virtual de esteira com alimentador, sensores fotoelétricos, desviador pneumático e proteção térmica;
- modelagem das imperfeições físicas: rampa de motor, tempo de curso, atraso de sensor, histerese, jitter e atraso de transporte;
- três falhas injetáveis: esteira patinando, sensor de saída travado e desviador emperrado;
- janela de simulação com sinóptico animado, tabela de I/O, forçamento, botoeiras de campo e faixa de energização dos rungs;
- execução em tempo real, passo a passo ou acelerada em 2x e 5x;
- driver `PLC virtual OpenLadder` e perfil de dispositivo correspondente, com escrita liberada por não existir hardware.

### Engenharia de software
- primeiro autoteste automatizado do projeto: `OpenLadderSimTest.exe` verifica o motor de varredura e a planta no build e no CI;
- `UniversalLadderElement` passa a preservar a coluna de origem, o que mantém os ramos paralelos fiéis ao editor;
- `ValidateProject.ps1` passa a exigir os fontes da simulação e a bloquear dependência de WinForms no núcleo;
- catálogo de módulos passa a classificar o motor de varredura e as plantas simuladas no domínio.

## [0.47] - 2026-09-04

### Pesquisa do protocolo TP02
- novo capturador de trafego serial entre o PC12 original e o PLC, em executavel proprio
  `OpenLadderTP02Capture.exe`, para rodar junto com o PC12;
- funciona como ponte: abre a porta virtual em que o PC12 acredita estar o PLC e a porta
  fisica do PLC, repassa os bytes e registra os dois sentidos com hora, sentido, hexadecimal
  e ASCII;
- separa quadros por silencio configuravel, valida a soma modulo 256 e anota CMD e LEN
  quando o quadro segue o formato derivado do codigo do PC12;
- nao transmite nada por conta propria: e estritamente um rele, sem risco de enviar comando
  desconhecido ao equipamento;
- captura exportavel em texto.

## [0.46] - 2026-09-04

### Comunicacao WEG TP02
- o perfil serial padrao passa de 19200 7E2 para **19200 8O1**, que e o que o PC12
  original forca ao abrir a porta;
- alterado na tela de Comunicacao, na leitura de programa por RBP, no perfil de conexao
  persistido do controlador e na descricao do driver;
- os perfis 7E2 continuam disponiveis como candidatos nas varreduras de deteccao.

## [0.45] - 2026-09-04

### Protocolo PG — preflight de status
- a análise estática do `pc12.exe` confirmou `F0 00 0F` como consulta de status/preflight da conexão, e não como comando de apagamento;
- o PC12 espera resposta de 5 bytes ao F0 e usa o bit `0x40` do primeiro byte recebido para determinar o estado operacional do PLC;
- a configuração serial do PC12 original foi fechada em 19200/8O1 para a taxa validada em bancada;
- o ensaio isolado de `38 00 C7` após o HELLO retornou zero bytes, mostrando que a sequência precisava do preflight anterior;
- pacote `2026.09.04.8` valida somente `HELLO -> F0 -> captura passiva`, mantendo `38...` e `34...` desabilitados.

### Segurança operacional
- motor PG Lab atualizado para 1.2;
- `F0 00 0F` deixa o bloqueio interno, mas só pode ser transmitido como `READ_ONLY_VERIFIED`, com autorização manual e presença explícita na `readOnlyAllowlist`;
- `0F 00 F0` continua bloqueado internamente e no pacote como Clear All Memory;
- RUN/STOP remoto, escrita, download, apagamento, senha e demais comandos candidatos continuam desabilitados.

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