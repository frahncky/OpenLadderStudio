# OpenLadder Studio

O **OpenLadder Studio** é um ambiente moderno de programação Ladder e ferramentas de engenharia para o PLC **WEG TP02**.

O projeto preserva a compatibilidade com o **PC12 Design Center 2.1** e com seus arquivos legados, mas o nome do novo software é **OpenLadder Studio**. O PC12 passa a ser tratado apenas como software legado, fonte de compatibilidade e referência para a evolução do projeto.

## OpenLadder Studio — interface principal

A interface principal é compilada como `OpenLadderStudio.exe` e reúne no mesmo ambiente:

- Editor Ladder moderno;
- comunicação e diagnóstico do TP02;
- TP02 Bridge Lab;
- leitor da memória de programa por `RBP`;
- decodificador `RBP -> Boolean/IL`;
- laboratório de calibração automática de opcodes;
- conversão IL -> Ladder;
- verificação de atualizações;
- acesso às ferramentas de compatibilidade com o PC12 original.

O arquivo `PC12_v2.1_Windows7_v3_portatil/INICIAR_PC12.bat` recompila as interfaces e abre prioritariamente o **OpenLadder Studio**.

A interface principal usa tema escuro, barra de menus, barra de ferramentas, área central de edição, painéis laterais e barra de status, seguindo uma organização semelhante a softwares industriais atuais.

## Editor Ladder — Etapa 2

O editor Ladder próprio usa a nomenclatura real do TP02 e já possui:

- contatos normalmente abertos e fechados;
- pontos `X0001–X0384`, `Y0001–Y0384`, `C0001–C2048` e `SC001–SC128`;
- bobina `OUT` para `Y` e `C`;
- ramificações paralelas;
- `TMR` e `CNT` com identificadores `V0001–V0256`;
- presets diretos ou por `D0001–D2048`;
- `SET` F-23 e `RESET` F-24;
- detectores de borda F-05/F-06;
- bloco genérico `FUN`;
- `END` F-00;
- múltiplos rungs, desfazer, edição, salvar/abrir e validação estrutural;
- formato `.pladder` versão 2 com leitura da versão anterior.

A validação do editor ainda não substitui a compilação oficial do PC12.

## TP02 Bridge Lab — Etapa 3

A terceira etapa inicia a compatibilidade real com o PC12 e a comunicação direta com o TP02.

### Laboratório de arquivos do PC12

Foi confirmado que `lastfile.cpu` e `lastfile.dir` são apenas arquivos auxiliares/históricos. Um projeto salvo pelo PC12 é formado por um conjunto de arquivos com o mesmo nome-base:

- `.PLC` — programa do usuário;
- `.sys1` — memória de sistema `WSxxx`;
- `.sys2` — marcadores especiais `SCxxx`;
- `.cnt` — posição/final do programa Ladder;
- `.reg1` — registradores `Vxxxx`;
- `.reg2` — registradores `Dxxxx`;
- `.reg3` — registradores `WCxxx`;
- `.sym` — símbolos/rótulos;
- `.file` — registradores de texto;
- `.cmt` — comentários;
- `.typ` — tipo do módulo básico.

O Bridge permite localizar arquivos auxiliares, gerar SHA-256/hexdump, extrair strings, comparar arquivos byte a byte e salvar relatórios de engenharia reversa.

### Comunicação serial somente leitura

O Bridge implementa a moldura ASCII do protocolo TP02 com checksum por complemento de dois e oferece:

- `PSR` — ler estado do PLC;
- `MCR` — ler entradas, saídas, relés auxiliares e especiais;
- `MRV` — ler registradores `V`, `D`, `WS`, `WC` e `F`.

A configuração inicial é 19200 bps, 7 bits, paridade EVEN, 2 stop bits, estação 01 e tempo de resposta 50 ms, podendo ser ajustada para coincidir com o PLC.

## Leitor RBP — Etapa 4

O `TP02ProgramReader.cs` implementa o comando oficial `RBP` em modo somente leitura.

Recursos atuais:

- endereço inicial `0000–4000`, adequado ao TP02-40/60;
- leitura de 1 a 100 passos por comando;
- opção rápida para ler `0000–0099`;
- checksum de comando e validação da resposta;
- agrupamento de cada passo em **3 bytes / 6 caracteres hexadecimais**;
- tabela com `passo`, `word`, byte alto, byte baixo e byte externo;
- salvamento de dumps `.rbpdump`;
- integração direta no menu **Ler programa** do OpenLadder Studio.

## Decodificador RBP -> Boolean/IL — Etapa 5

O `TP02MachineDecoder.cs` adiciona uma camada de engenharia reversa controlada entre a leitura RBP e a futura reconstrução Ladder.

Recursos:

- abertura de dumps `.rbpdump`;
- tabela por passo com `WORD`, `HIGH`, `LOW` e `EXT`;
- comparação de dois dumps no mesmo intervalo;
- cálculo do XOR dos 24 bits de cada passo alterado;
- indicação de quais bytes `HIGH/LOW/EXT` mudaram;
- cadastro local de um WORD como `STR`, `STR NOT`, `AND`, `AND NOT`, `OR`, `OR NOT`, `AND STR`, `OR STR`, `OUT`, `TMR`, `CNT`, `FUN` ou `END`;
- operando associado ao mapeamento;
- nível de evidência: `Manual`, `Teste controlado`, `Inferido por comparação` ou `Não confirmado`;
- mapa local em `tp02_opcode_map.tsv`;
- exportação do dump para uma lista `.il.txt`, mantendo `UNKNOWN` onde ainda não houver prova suficiente;
- amostra RBP documentada no manual com os words `5E1509`, `204006` e `20C10F`, mantidos como `UNKNOWN` até existir associação semântica comprovada.

## Calibração automática de opcodes — Etapa 6

O `TP02OpcodeCalibration.cs` automatiza a comparação de vários experimentos controlados.

Fluxo:

1. criar no PC12 original programas mínimos que diferem em apenas um item;
2. ler sempre a mesma faixa com `RBP`;
3. salvar cada leitura como `.rbpdump`;
4. informar ao laboratório qual instrução e operando foram usados;
5. executar **INFERIR MÁSCARAS**.

O laboratório calcula:

- máscara de bits que variam entre operandos da mesma instrução;
- máscara candidata de opcode, usando os bits que permanecem constantes;
- valor candidato do opcode;
- comparação de instruções diferentes usando o mesmo operando;
- XOR de 24 bits para localizar diferenças de opcode;
- representação binária por `HIGH / LOW / EXT`;
- relatório de calibração `.cal.txt`.

Há também um **ROTEIRO DE TESTES** embutido com sequências como:

- `STR X0001`, `STR X0002`, `STR X0004`, `STR X0016`;
- `STR X0001`, `STR NOT X0001`, `AND X0001`, `AND NOT X0001`, `OR X0001`, `OR NOT X0001`;
- `STR X0001`, `STR Y0001`, `STR C0001`, `STR SC001`;
- testes separados de `OUT`, `TMR` e `CNT`.

Uma máscara inferida **não é automaticamente tratada como comprovada**. Ela deve ser repetida com vários endereços e famílias de operandos antes de virar regra definitiva no decodificador.

A metodologia de pesquisa também está documentada em `docs/TP02_OPCODE_RESEARCH.md`.

**Nenhum comando `WBP`, RUN, STOP, escrita de registradores ou limpeza de memória é exposto nas ferramentas modernas.**

## Como iniciar

### OpenLadder Studio — recomendado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_PC12.bat`

### Editor Ladder separado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_EDITOR_LADDER.bat`

Esse inicializador gera e abre `OpenLadderEditor.exe`.

### TP02 Bridge Lab separado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_BRIDGE_TP02.bat`

### Leitor RBP separado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_LEITOR_RBP.bat`

### Decodificador RBP separado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_DECODIFICADOR_RBP.bat`

### Calibração automática separada

`PC12_v2.1_Windows7_v3_portatil/INICIAR_CALIBRACAO_OPCODE.bat`

### PC12 clássico — compatibilidade

`PC12_v2.1_Windows7_v3_portatil/INICIAR_PC12_CLASSICO.bat`

## Arquivos principais

- `PC12DirectStudio.cs` — shell principal do **OpenLadder Studio**;
- `LadderEditor.cs` — editor Ladder;
- `TP02BridgeLab.cs` — análise de projetos PC12 e comunicação somente leitura;
- `TP02ProgramReader.cs` — leitor `RBP` da memória de programa;
- `TP02MachineDecoder.cs` — comparação de WORDs RBP e exportação para IL;
- `TP02OpcodeCalibration.cs` — inferência automática de máscaras de opcode/operando;
- `TP02CalibrationCampaign.cs` — campanha de calibração;
- `TP02AutoDecoder.cs` — decodificação automática;
- `TP02IlToLadder.cs` — reconstrução IL -> Ladder;
- `PC12Updater.cs` — atualizador do OpenLadder Studio;
- `PC12Studio.cs` e `ModernPC12.cs` — componentes de transição/compatibilidade mantidos no código;
- `docs/TP02_OPCODE_RESEARCH.md` — metodologia e registro de evidências dos opcodes;
- `BUILD_INTERFACE_MODERNA.bat` — compilação local das interfaces;
- `INICIAR_PC12.bat` — inicializador principal.

## Compatibilidade

As interfaces usam **Windows Forms + .NET Framework**, sem bibliotecas externas, mantendo **Windows 7 SP1 como base mínima** e visando também Windows 8.1, 10 e 11.

## Arquitetura de transição

1. central moderna e diagnóstico — concluído;
2. editor Ladder moderno — iniciado;
3. instruções e endereçamento reais do TP02 — iniciado;
4. interface unificada OpenLadder Studio — iniciada;
5. engenharia reversa do formato nativo do PC12 — em andamento;
6. comunicação serial em modo somente leitura — iniciada;
7. leitura do programa por `RBP` — implementada em nível de linguagem de máquina;
8. laboratório de decodificação RBP para Boolean/IL — implementado;
9. inferência automática de máscaras de opcode/operando — implementada;
10. validação dos opcodes com dumps controlados reais — depende dos experimentos no hardware/PC12;
11. reconstrução automática Boolean/IL -> Ladder;
12. importação `.PLC` / RBP -> `.pladder`;
13. geração controlada do formato nativo;
14. transferência de programa após validação com hardware;
15. substituição progressiva do PC12 legado.

## Identidade do projeto

**Nome do software:** OpenLadder Studio  
**PLC-alvo:** WEG TP02  
**Software legado compatível:** PC12 Design Center 2.1  
**Versão atual:** 0.11
