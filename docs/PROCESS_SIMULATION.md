# Simulação de processo do OpenLadder Studio

O OpenLadder Studio executa programas Ladder em um PLC virtual acoplado a uma planta simulada. O objetivo é validar lógica, treinar e demonstrar comportamento antes de ligar qualquer equipamento real.

Esta é a **fase A** do plano de simulação: motor de varredura, I/O virtual, forçamento, driver simulado e uma planta de esteira para validação.

## O que é simulado e o que não é

A simulação é **fenomenológica**: reproduz o comportamento observável do processo com modelos físicos plausíveis, não a identificação de um equipamento específico. Isso é suficiente para validar lógica, treinar operação e demonstrar sequências. Não substitui comissionamento nem certifica desempenho de uma planta real.

Nenhuma saída física é acionada. O simulador é o único lugar do produto em que escrita, forçamento e transferência de programa são liberados, exatamente porque não existe hardware do outro lado.

## Arquitetura

| Camada | Arquivo | Responsabilidade |
|---|---|---|
| Domínio | `LadderSimulation.cs` | endereçamento, imagem de processo, forçamento, entradas de campo, contatos especiais e motor de varredura |
| Domínio | `ProcessSimulation.cs` | contrato de planta, blocos de imperfeição física, planta de esteira e programa de exemplo |
| Infraestrutura | `PLCPlatform.cs` | `SimulatedPlcDriver` e o perfil `openladder.simulator.plc` |
| Apresentação | `LadderSimulator.cs` | janela de simulação, sinóptico, tabela de I/O, forçamento e injeção de falhas |
| Verificação | `SimulationSelfTest.cs` | autoteste em console, executado no build e no CI |

`LadderSimulation.cs` e `ProcessSimulation.cs` não referenciam WinForms. `scripts/ValidateProject.ps1` bloqueia o build se essa regra for violada.

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

### Divergências conhecidas entre o motor e o editor

O motor aceita `V0001`–`V0256` como endereço de contato e como alvo de `RESET`, para que o bit de conclusão de um temporizador ou contador possa ser usado na lógica. O editor ainda não permite inserir esses elementos: os diálogos de contato aceitam apenas `X`, `Y`, `C` e `SC`, e `SET`/`RESET` aceitam apenas `Y` e `C`.

O programa de exemplo foi escrito de propósito dentro do subconjunto que o editor sabe inserir, para poder ser reproduzido à mão. Fechar essa lacuna no editor é trabalho da fase C.

## Planta de esteira

A planta de referência é uma esteira de 2,0 m com alimentador, dois sensores fotoelétricos, desviador pneumático e proteção térmica.

| Endereço | Ponto | Origem |
|---|---|---|
| `Y0001` | motor da esteira | PLC |
| `Y0002` | desviador pneumático | PLC |
| `Y0003` | sinaleiro de marcha | PLC |
| `X0001` | sensor de entrada S1 | planta |
| `X0002` | sensor de saída S2 | planta |
| `X0003` | fim de curso do desviador | planta |
| `X0004` | botoeira liga | operador |
| `X0005` | botoeira para | operador |
| `X0006` | relé térmico do motor | planta |

O realismo vem das imperfeições, não da equação ideal:

- rampa de aceleração e frenagem do motor, em vez de degrau de velocidade;
- tempo de curso do pistão, diferente no avanço e no recuo;
- atraso de primeira ordem na resposta dos sensores, com histerese na comparação;
- janela física de captura do desviador, definida pela largura da placa;
- jitter no intervalo do alimentador;
- atraso de transporte inerente: a caixa leva o tempo real de percurso entre os sensores;
- sobrecarga térmica que só atua após um tempo sustentado de esforço.

O gerador de jitter usa semente fixa, portanto uma execução é reproduzível.

### Falhas injetáveis

| Falha | Efeito |
|---|---|
| Esteira patinando | reduz a velocidade da correia e leva o motor à sobrecarga térmica |
| Sensor de saída travado | `X0002` congela no último valor lido |
| Desviador emperrado | o curso não completa e `X0003` nunca é atingido |

A falha do desviador é a mais didática: o programa de exemplo dá `SET` em `Y0002` e depende do fim de curso para o `RESET`. Sem o fim de curso, a saída fica travada e o intertravamento desliga o motor — exatamente o que aconteceria na máquina.

## Programa de exemplo

```text
1  X0004 ou C0001, com X0005 e X0006 normalmente fechados, selam C0001 (marcha).
2  C0001 com Y0002 normalmente fechado aciona Y0001 (motor da esteira).
3  X0002 e C0001, com X0003 normalmente fechado, dão SET em Y0002 (avança o desviador).
4  X0003 dá RESET em Y0002 (recolhe o desviador no fim de curso).
5  X0003 incrementa o contador V0002 (caixas desviadas).
6  C0001 com SC004 pisca Y0003 (sinaleiro de marcha a 1 Hz).
7  C0001 alimenta o temporizador retentivo V0001 (horímetro de marcha).
8  END.
```

## Como usar

Pelo shell principal: **Ferramentas → Simulação de processo**, ou o item **Simular processo** na navegação lateral. Se o editor já tiver elementos, o projeto aberto é carregado no PLC virtual; caso contrário, o simulador mantém o programa de exemplo.

Como ferramenta separada: `INICIAR_SIMULADOR.bat`.

Na janela:

- **Iniciar**, **Parar**, **Passo** e **Reiniciar** controlam a execução; **Passo** executa uma varredura por vez;
- a velocidade pode ser 1x, 2x ou 5x do tempo real;
- as botoeiras de campo permanecem acionadas enquanto pressionadas, com mouse ou teclado;
- a tabela de I/O permite forçar 1, forçar 0 e liberar pontos selecionados;
- a faixa de rungs mostra quais estão energizados e quais não foram alcançados na varredura.

## Verificação

`SimulationSelfTest.cs` gera `OpenLadderSimTest.exe`, que roda o par PLC virtual + planta e verifica endereçamento, selo de partida, ciclo completo da esteira, contato de pulso, temporizador retentivo, forçamento e as três falhas injetáveis.

O autoteste é executado pelo `BUILD_INTERFACE_MODERNA.bat` e pelo GitHub Actions. Uma falha interrompe o build e a publicação.

## Próximas fases

- **Fase B** — biblioteca de processos discretos: silo, partida estrela-triângulo, semáforo, elevador, prensa.
- **Fase C** — blocos de comparação, aritmética e analógicos no modelo Ladder, abrindo caminho para processos contínuos: nível de tanque, forno com tempo morto, pressão, vazão.
- **Fase D** — servidor Modbus TCP expondo o PLC virtual, cenários de falha roteirizados e replay sobre o histórico de tendências.
