# Simulação de processo do OpenLadder Studio

O OpenLadder Studio executa programas Ladder em um PLC virtual acoplado a uma planta simulada. O objetivo é validar lógica, treinar e demonstrar comportamento antes de ligar qualquer equipamento real.

A **fase A** entregou o motor de varredura, o I/O virtual, o forçamento, o driver simulado e a primeira planta. A **fase B** transformou a planta única em uma biblioteca de seis processos discretos, com sinóptico descrito pela própria planta.

## O que é simulado e o que não é

A simulação é **fenomenológica**: reproduz o comportamento observável do processo com modelos físicos plausíveis, não a identificação de um equipamento específico. Isso é suficiente para validar lógica, treinar operação e demonstrar sequências. Não substitui comissionamento nem certifica desempenho de uma planta real.

Nenhuma saída física é acionada. O simulador é o único lugar do produto em que escrita, forçamento e transferência de programa são liberados, exatamente porque não existe hardware do outro lado.

## Arquitetura

| Camada | Arquivo | Responsabilidade |
|---|---|---|
| Domínio | `LadderSimulation.cs` | endereçamento, imagem de processo, forçamento, entradas de campo, contatos especiais e motor de varredura |
| Domínio | `ProcessSimulation.cs` | blocos de imperfeição física, contrato de planta, modelo de cena, montagem de rungs e catálogo |
| Domínio | `SimulatedPlants.cs` | as seis plantas e os programas Ladder de exemplo |
| Infraestrutura | `PLCPlatform.cs` | `SimulatedPlcDriver` e o perfil `openladder.simulator.plc` |
| Apresentação | `LadderSimulator.cs` | janela de simulação, renderizador de cena, tabela de I/O, forçamento e injeção de falhas |
| Verificação | `SimulationSelfTest.cs` | autoteste em console, executado no build e no CI |

`LadderSimulation.cs`, `ProcessSimulation.cs` e `SimulatedPlants.cs` não referenciam WinForms. `scripts/ValidateProject.ps1` bloqueia o build se essa regra for violada.

## Ciclo de varredura

Cada passo de simulação executa, nesta ordem:

1. a planta avança o próprio relógio e escreve as entradas que ela controla;
2. as entradas de campo (botoeiras) são reafirmadas na imagem de processo;
3. os forçamentos de entrada sobrescrevem o que veio da planta e do campo;
4. os contatos especiais são atualizados;
5. os rungs são resolvidos na ordem do programa, até o `END`;
6. os forçamentos de saída, auxiliares e TMR/CNT sobrescrevem o resultado da lógica.

O passo padrão é de 10 ms, e o relógio da planta é o mesmo do PLC nesta fase. A interface acumula o tempo real decorrido e executa quantos passos forem necessários; se a máquina não acompanhar, o atraso é descartado em vez de acumular.

A etapa 2 existe por um motivo prático: sem reafirmar as entradas de campo a cada varredura, liberar um forçamento deixaria a entrada congelada no último valor forçado. Entradas que nem a planta nem o campo escrevem permanecem indefinidas.

## Endereçamento

O simulador usa o mesmo endereçamento do editor Ladder:

| Área | Faixa | Uso |
|---|---|---|
| `X` | `X0001`–`X0384` | entradas |
| `Y` | `Y0001`–`Y0384` | saídas |
| `C` | `C0001`–`C2048` | auxiliares |
| `SC` | `SC001`–`SC128` | contatos especiais |
| `V` | `V0001`–`V0256` | identificadores de TMR/CNT |
| `D` | `D0001`–`D2048` | registradores de dados |

Um contato em `V` lê o bit de conclusão do temporizador ou contador correspondente, e `RESET V####` zera o acumulado. Sem isso nenhuma sequência temporizada seria expressável, e metade dos processos desta biblioteca — semáforo, prensa, porta do elevador — não existiria.

### Contatos especiais

Os contatos especiais implementados são uma **convenção do simulador**. A correspondência com o mapa real do TP02 depende da pesquisa registrada em [`TP02_OPCODE_RESEARCH.md`](TP02_OPCODE_RESEARCH.md) e ainda não foi confirmada em hardware.

| Endereço | Significado |
|---|---|
| `SC001` | sempre ligado |
| `SC002` | sempre desligado |
| `SC003` | pulso de 0,1 s |
| `SC004` | pulso de 1 s |
| `SC005` | pulso de 1 min |
| `SC006` | primeira varredura |

## Semântica dos elementos

O modelo do editor tem oito colunas por rung: sete de condição e a última reservada à saída. Cada coluna de condição pode ter um elemento na via série e um ramo paralelo em torno dela.

O valor de uma coluna é resolvido assim:

- série e paralelo preenchidos: `série OU paralelo`;
- somente série: valor da série;
- somente paralelo: valor do ramo, que passa a ser a única condição da coluna;
- nenhum dos dois: fio, sem efeito sobre o rung.

O rung energiza quando todas as colunas com conteúdo resultam em verdadeiro.

| Elemento | Comportamento |
|---|---|
| Contato NA / NF | lê o bit; NF inverte |
| Bobina | copia a energização do rung para o bit |
| `SET` / `RESET` | com o rung energizado, liga ou desliga o bit; `RESET` em `V` também zera o acumulado |
| `TMR` | temporizador na energização; sem `RESET` marcado, o acumulado zera quando o rung desliga |
| `TMR` com `RESET` | retentivo: preserva o acumulado com o rung desligado e só zera por um `RESET V####` explícito |
| `CNT` | conta bordas de subida da energização e liga o bit ao atingir o preset |
| Bordas `F-05` / `F-06` | o pulso é calculado, mas o modelo do editor não permite associá-las a uma bobina; o simulador avisa na carga e não escreve em memória |
| Funções `F-xx` | ainda não executadas; o simulador registra um aviso na carga |
| `END` | encerra a varredura |

Uma unidade de preset de temporizador equivale a **100 ms**. Preset `10` é 1,0 s.

Bobina comum continua restrita a `Y` e `C`: quem aciona o bit de um `V` é o próprio bloco TMR/CNT.

## Biblioteca de plantas

Todas as plantas seguem o mesmo contrato: leem as saídas do PLC, escrevem as entradas, expõem falhas injetáveis, descrevem o próprio sinóptico e trazem um programa Ladder de exemplo escrito apenas com elementos que o editor sabe inserir.

| Planta | O que exercita | Falhas injetáveis |
|---|---|---|
| Esteira com desviador | selo de partida, intertravamento, contagem, contato de pulso, temporizador retentivo | esteira patinando, sensor de saída travado, desviador emperrado |
| Silo com enchimento e descarga | controle liga-desliga por chaves de nível, descarga condicionada, transbordo | válvula travada aberta, chave de nível alto cega, material empedrado |
| Partida estrela-triângulo | comutação temporizada, tempo morto entre contatores, permissão por corrente | contator de estrela colado, térmico com ajuste baixo, carga pesada |
| Cruzamento semafórico | sequenciador de quatro fases em cascata, atendimento sob demanda | hora de pico, laço detector cego, lâmpada queimada |
| Elevador de carga de dois níveis | chamadas, intertravamento de sentido, ciclo de porta temporizado, sobrepeso | porta emperrada, fim de curso superior cego, carga acima do limite |
| Prensa com comando bimanual | comando sem selo, cortina de luz, tempo de prensagem, anti-repetição | operador na zona de risco, vazamento hidráulico, fim de curso inferior cego |

### O que cada planta ensina quando a lógica está errada

O valor de uma planta simulada está em falhar de forma observável. Cada uma conta um evento que só acontece com lógica malfeita:

- **Estrela-triângulo** conta **curto entre fases** quando estrela e triângulo ficam fechados ao mesmo tempo. O contator de estrela leva cerca de 50 ms para abrir: sem o tempo morto do programa, o curto acontece em toda partida.
- **Semáforo** conta **conflito entre verdes**, varredura a varredura.
- **Prensa** conta **descida com a cortina interrompida**. Retire o contato da cortina do primeiro rung e injete a falha para ver.
- **Silo** conta **transbordo** quando a única proteção de nível é removida ou cega.
- **Elevador** conta **colisão no fim do curso** quando o fim de curso superior não atua.
- **Esteira** conta **caixas perdidas** no fim da correia.

### Imperfeições físicas

O realismo vem das imperfeições, não da equação ideal. A caixa de ferramentas em `ProcessSimulation.cs` traz três blocos reutilizados por todas as plantas:

- `FirstOrderLag` — tempo de resposta de sensores e grandezas que não mudam em degrau;
- `RateLimiter` — rampa de motor, curso de válvula, curso de pistão, deslocamento de cabine;
- `HysteresisSwitch` — comparação com banda, para que ruído não gere chaveamento no limiar.

Somam-se a isso o atraso de transporte inerente (a caixa leva o tempo real de percurso entre os sensores), o jitter dos geradores de eventos e os contadores de falha. Os geradores usam semente fixa, portanto uma execução é reproduzível.

## Sinóptico

A planta descreve o próprio sinóptico em `BuildScene`, usando primitivas semânticas: retângulo, elipse, linha, texto, correia, nível e sinaleiro. As coordenadas são de uma tela virtual de 1000 × 320, e as cores são **papéis** (`SimTone.Active`, `SimTone.Danger`, `SimTone.Cargo`), não valores RGB.

A interface escala a cena com proporção preservada e resolve os papéis na paleta da cena. A moldura da janela do simulador segue o tema escolhido no shell, mas o sinóptico em si mantém fundo escuro nos dois temas: fundo escuro é a convenção de sala de controle e não deve depender da preferência visual da interface. O domínio continua sem depender de WinForms, e uma planta nova custa uma dezena de linhas de desenho em vez de um controle gráfico próprio.

## Como usar

Pelo shell principal: **Ferramentas → Simulação de processo**, ou o item **Simular processo** na navegação lateral. Se o editor já tiver elementos, o projeto aberto é carregado no PLC virtual; caso contrário, o simulador mantém o programa de exemplo da planta selecionada.

Como ferramenta separada: `INICIAR_SIMULADOR.bat`.

Na janela:

- **Iniciar**, **Parar**, **Passo** e **Reiniciar** controlam a execução; **Passo** executa uma varredura por vez;
- a lista **Planta** troca o processo simulado, refazendo tabela de I/O, botoeiras, falhas e programa de exemplo;
- a velocidade pode ser 1x, 2x ou 5x do tempo real;
- as botoeiras de campo permanecem acionadas enquanto pressionadas, com mouse ou teclado;
- a tabela de I/O permite forçar 1, forçar 0 e liberar pontos selecionados;
- a faixa de rungs mostra quais estão energizados e quais não foram alcançados na varredura.

## Verificação

`SimulationSelfTest.cs` gera `OpenLadderSimTest.exe`, que roda o par PLC virtual + planta e verifica endereçamento, carga limpa do programa de cada planta, e o comportamento específico de cada processo — inclusive sob falha injetada. São mais de cem verificações em menos de um quinto de segundo.

O autoteste é executado pelo `BUILD_INTERFACE_MODERNA.bat` e pelo GitHub Actions. Uma falha interrompe o build e a publicação.

## Como adicionar uma planta

1. Derive de `SimulatedProcessBase` em `SimulatedPlants.cs`.
2. Registre os pontos no construtor com `Output`, `Sensor` e `Button`, e as falhas com `Fault`.
3. Implemente `Step` com a física, usando os blocos de imperfeição em vez de degraus ideais.
4. Implemente `BuildScene` com as primitivas de cena e `StateSummary` com os números que importam.
5. Escreva `BuildSampleProgram` com `LadderBuild` e `RungBuilder`, e explique rung a rung em `DescribeSampleProgram`.
6. Acrescente a planta em `SimulatedProcessCatalog.Create` e um bloco de verificação em `SimulationSelfTest.cs`.

## Próximas fases

- **Fase C** — blocos de comparação, aritmética e analógicos no modelo Ladder, abrindo caminho para processos contínuos: nível de tanque, forno com tempo morto, pressão, vazão.
- **Fase D** — servidor Modbus TCP expondo o PLC virtual, cenários de falha roteirizados e replay sobre o histórico de tendências.
