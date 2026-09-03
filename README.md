# UpgradeInterfacePLC

Modernização da experiência de uso do **PC12 Design Center 2.1** para o PLC **WEG TP02**, preservando o executável legado e desenvolvendo gradualmente um editor Ladder e uma camada de comunicação próprios.

## PC12 Studio TP02 — interface principal

O projeto possui uma **interface unificada** chamada `PC12_Studio.exe`. Ela reúne no mesmo ambiente:

- visão geral do projeto;
- Editor Ladder moderno;
- TP02 Bridge Lab;
- leitor da memória de programa por `RBP`;
- decodificador/calibrador `RBP -> Boolean/IL`;
- acesso ao PC12 original;
- informações de compatibilidade e estágio da modernização.

O arquivo `INICIAR_PC12.bat` recompila as interfaces e abre prioritariamente o **PC12 Studio**. Se a compilação não estiver disponível, o inicializador mantém os fallbacks para a central moderna anterior e, por fim, para o `pc12.exe` original.

## PC12 Ladder Studio — Etapa 2

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

A validação do Ladder Studio ainda não substitui a compilação oficial do PC12.

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
- integração direta no menu **Ler programa (RBP)** do PC12 Studio.

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
- amostra RBP documentada no manual com os words `5E1509`, `204006` e `20C10F`, mantidos como `UNKNOWN` até existir associação semântica comprovada;
- integração ao menu **Decodificar RBP** do PC12 Studio.

A metodologia de pesquisa está documentada em `docs/TP02_OPCODE_RESEARCH.md`. A estratégia é comparar programas quase idênticos, alterando apenas uma instrução ou um endereço por experimento para separar os bits de **opcode** dos bits de **operando**.

O manual do TP02 informa que o RBP retorna **linguagem de máquina**, não a lista Boolean/IL traduzida. Por isso o software não atribui automaticamente significados às palavras sem evidência.

**Nenhum comando `WBP`, RUN, STOP, escrita de registradores ou limpeza de memória é exposto nas ferramentas modernas.**

## Como iniciar

### PC12 Studio — recomendado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_PC12.bat`

### PC12 Ladder Studio separado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_EDITOR_LADDER.bat`

### TP02 Bridge Lab separado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_BRIDGE_TP02.bat`

### Leitor RBP separado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_LEITOR_RBP.bat`

### Decodificador RBP separado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_DECODIFICADOR_RBP.bat`

### PC12 clássico

`PC12_v2.1_Windows7_v3_portatil/INICIAR_PC12_CLASSICO.bat`

## Arquivos principais

- `PC12Studio.cs` — shell unificado e interface principal;
- `ModernPC12.cs` — central moderna anterior;
- `LadderEditor.cs` — editor Ladder;
- `TP02BridgeLab.cs` — análise de projetos PC12 e comunicação somente leitura;
- `TP02ProgramReader.cs` — leitor `RBP` da memória de programa;
- `TP02MachineDecoder.cs` — comparação/calibração de WORDs RBP e exportação para IL;
- `docs/TP02_OPCODE_RESEARCH.md` — metodologia e registro de evidências dos opcodes;
- `BUILD_INTERFACE_MODERNA.bat` — compilação local das interfaces;
- `INICIAR_PC12.bat` — inicializador principal;
- `INICIAR_EDITOR_LADDER.bat` — Ladder separado;
- `INICIAR_BRIDGE_TP02.bat` — Bridge separado;
- `INICIAR_LEITOR_RBP.bat` — leitor RBP separado;
- `INICIAR_DECODIFICADOR_RBP.bat` — decodificador separado;
- `INICIAR_PC12_CLASSICO.bat` — PC12 legado.

## Compatibilidade

As interfaces usam **Windows Forms + .NET Framework**, sem bibliotecas externas, mantendo **Windows 7 SP1 como base mínima** e visando também Windows 8.1, 10 e 11.

## Arquitetura de transição

1. central moderna e diagnóstico — concluído;
2. editor Ladder moderno — iniciado;
3. instruções e endereçamento reais do TP02 — iniciado;
4. interface unificada PC12 Studio — iniciada;
5. engenharia reversa do formato nativo do PC12 — em andamento;
6. comunicação serial em modo somente leitura — iniciada;
7. leitura do programa por `RBP` — implementada em nível de linguagem de máquina;
8. laboratório de decodificação RBP para Boolean/IL — implementado;
9. calibração sistemática dos opcodes e campos de endereço — próxima etapa com dumps controlados;
10. reconstrução automática Boolean/IL -> Ladder;
11. importação `.PLC` / RBP -> `.pladder`;
12. geração controlada do formato nativo;
13. transferência de programa após validação com hardware;
14. substituição progressiva do PC12 legado.
