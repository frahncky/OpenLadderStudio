# Guia de interface do OpenLadder Studio

## Princípios

A interface deve parecer uma ferramenta de engenharia profissional: compacta, previsível, legível e com cor usada para significado, não como decoração excessiva.

## Identidade

- fundo principal: grafite escuro;
- destaque de produto: verde OpenLadder;
- texto principal: cinza muito claro;
- texto secundário: cinza neutro;
- azul: arquivos, controlador e informação;
- âmbar: abrir/atenção;
- turquesa/ciano: salvar, monitor e conversão;
- violeta: histórico/desfazer/configuração;
- vermelho: remoção/erro;
- amarelo: energia/aviso.

A cor de um ícone deve permanecer estável entre barra superior, navegação e abas.

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

- fundo grafite;
- trilhos Ladder verdes;
- rung em branco;
- bloco PLC em azul;
- ausência de texto, para manter legibilidade em 16x16.

O arquivo `.ico` deve ser gerado em múltiplas resoluções pelo script `GenerateOpenLadderIcon.ps1`.
