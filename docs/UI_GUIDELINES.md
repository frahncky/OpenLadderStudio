# Guia de interface do OpenLadder Studio

## Princípios

A interface deve parecer uma ferramenta de engenharia profissional: compacta, previsível, legível e com cor usada para significado, não como decoração excessiva.

## Paleta central

Existe **uma única fonte de cor** no produto: `OpenLadderPalette`, em
`PC12_v2.1_Windows7_v3_portatil/AppBranding.cs`. Esse arquivo entra em todos os
executáveis, então shell, editor, simulador, monitor Modbus, gerenciador de
controladores, atualizador e ferramentas TP02 leem exatamente as mesmas cores.

Nenhuma tela deve declarar cor própria. Cor fixa em `Color.FromArgb(...)` só é
aceitável para elementos de cena do sinóptico, que não seguem o tema.

## Temas

O produto tem dois temas, escolhidos em **Exibir → Tema**. A preferência fica em
`%APPDATA%\OpenLadder Studio\tema.txt` e a troca completa vale na próxima
abertura — muita tela fixa a cor no construtor, então repintar a quente deixaria
a janela pela metade. O aplicativo oferece reiniciar na hora.

| Papel | Escuro (padrão) | Claro |
|---|---|---|
| fundo da janela | `#181E28` | `#E9EFF6` |
| painel/chrome | `#202834` | `#F4F7FB` |
| superfície elevada | `#283241` | `#FAFCFF` |
| borda | `#3F4E62` | `#C2CFDF` |
| texto principal | `#E9EFF7` | `#1E2C3E` |
| texto secundário | `#B1BED0` | `#4D6077` |
| destaque | `#6EAEFF` | `#1C69D2` |

Nenhum dos dois usa preto puro ou branco puro em superfície grande: são as duas
extremidades que mais cansam a vista em jornada longa.

A navegação usa uma base mais profunda, os painéis ficam no nível intermediário
e os campos de entrada usam a superfície elevada. Seleção e item ativo recebem
um preenchimento azul discreto; bordas separam as regiões sem competir com o Ladder.
Texto secundário e rótulos discretos mantêm contraste de pelo menos 4,5:1 sobre
os painéis e superfícies elevadas dos dois temas.

Botões preenchidos com `Accent` ou `AccentDark` devem usar `OnAccent`: texto
azul profundo no tema escuro e branco no tema claro. Não fixar branco sobre o
azul luminoso do tema escuro. Grades sem estilo próprio recebem linhas alternadas,
separadores horizontais e cabeçalhos na mesma paleta.

## Cor semântica

- azul: arquivos, controlador e informação;
- âmbar: abrir/atenção;
- turquesa/ciano: salvar, monitor e conversão;
- violeta: histórico/desfazer/configuração;
- vermelho: remoção/erro;
- amarelo/dourado: energia/aviso.

Cada ícone tem **duas calibragens**, uma por tema, declaradas como par em
`OpenLadderPalette.Duo(escuro, claro)`. A família de matiz é a mesma nos dois; o
que muda é a luminosidade, para manter contraste mínimo de 4:1 contra a
superfície do tema. A cor de um ícone deve permanecer estável entre barra
superior, navegação e abas.

## Barra superior

A barra superior deve conter somente ações frequentes. A ordem recomendada é:

`Novo | Abrir | Salvar || Desfazer | Rung | Validar || Monitor`

Regras:

- ícone acima e rótulo abaixo;
- ícones com cor semântica;
- fundo circular discreto, não um botão colorido inteiro;
- hover mais evidente que o estado normal;
- foco de teclado sempre visível;
- rótulos nunca devem ficar cortados na altura compacta.

## Navegação lateral

- usar grupos por responsabilidade;
- item ativo recebe uma barra de cor à esquerda;
- o ícone ativo/hover utiliza a mesma cor semântica da ação;
- texto ativo pode usar peso semibold;
- evitar duplicar o mesmo estado em vários lugares da tela.

## Abas

- uma aba representa um documento ou ferramenta aberta;
- a aba ativa deve ter contraste maior;
- ícone da aba usa a cor semântica da ferramenta;
- fechamento deve permanecer pequeno e secundário;
- não criar abas para simples diálogos modais.

## Espaçamento

Preferir múltiplos de 4 px. Referências:

- 4 px: microespaço;
- 8 px: espaço entre ícone e conteúdo;
- 12–16 px: margens internas de painéis;
- 24 px: separação entre blocos funcionais.

## Escalonamento e DPI

Os executáveis embutem um manifesto que declara reconhecimento de DPI do sistema
(`dpiAware`). Sem essa declaração o Windows amplia a janela como bitmap e a interface
aparece borrada em telas com escala de 125%, 150% ou 200%.

Consequências para quem escreve interface:

- os formulários devem manter `AutoScaleMode.Dpi`;
- tamanhos de fonte são definidos em pontos, nunca em pixels;
- desenho de ícone deve ser proporcional ao retângulo recebido, como faz `StudioGlyph.Draw`,
  e não em coordenadas absolutas;
- a declaração é de DPI do sistema, não per-monitor: mover a janela entre telas de escalas
  diferentes não redimensiona os controles até reabrir o aplicativo.

No editor Ladder, 100% ajusta o diagrama à largura disponível. Redimensionar a
janela ou os painéis recalcula esse ajuste; o zoom de 50% a 200% amplia ou reduz
o desenho a partir dessa base. Desenho, seleção e hover compartilham a mesma
transformação, inclusive com rolagem. Os fundos de destaque são desenhados antes
dos fios para preservar as conexões dos elementos. O grid usa linhas contínuas,
com L1, L2… na lateral e C1, C2… no topo para identificar as posições.

## Erros visíveis ao usuário

Falha inesperada não pode encerrar o aplicativo em silêncio. `StudioDiagnostics` captura
exceções não tratadas, registra em `%APPDATA%\OpenLadder Studio\logs` e mostra uma
mensagem que inclui o caminho do registro, para o usuário conseguir reportar o problema.

## Tipografia

- interface: Segoe UI;
- títulos/itens ativos: Segoe UI Semibold;
- console e dados técnicos: Consolas;
- evitar caixa alta em textos longos;
- caixa alta é aceitável em rótulos curtos de seção.

## Estados

- sucesso/conectado: verde;
- informação: azul;
- aviso: âmbar/amarelo;
- erro: vermelho;
- offline/neutro: cinza.

Nunca depender somente de cor para transmitir estado; combinar cor com texto ou símbolo.

## Acessibilidade

Controles de ação devem:

- aceitar foco via Tab quando apropriado;
- permitir Enter ou Espaço;
- mostrar retângulo de foco;
- manter contraste suficiente;
- ter texto legível mesmo sem interpretar a cor do ícone.

## Ícone do aplicativo

O ícone oficial combina:

- fundo azul-grafite arredondado, com leve gradiente vertical;
- escada Ladder (dois trilhos e três degraus) em branco;
- um "O" em âmbar como ponto focal da marca;
- um "L" branco integrado à direita;
- um único ponto de estado em verde;
- ausência de texto, para manter legibilidade em 16x16.

O arquivo `.ico` deve ser gerado em múltiplas resoluções pelo script `GenerateOpenLadderIcon.ps1`.
