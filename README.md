# UpgradeInterfacePLC

Modernização da experiência de uso do **PC12 Design Center 2.1** para o PLC **WEG TP02**, preservando o executável legado e desenvolvendo gradualmente um editor Ladder e uma camada de comunicação próprios.

## PC12 Studio TP02 — interface principal

O projeto agora possui uma **interface unificada** chamada `PC12_Studio.exe`. Ela reúne no mesmo ambiente:

- visão geral do projeto;
- Editor Ladder moderno;
- TP02 Bridge Lab;
- acesso ao PC12 original;
- informações de compatibilidade e estágio da modernização.

O arquivo `INICIAR_PC12.bat` passa a recompilar as interfaces e abrir prioritariamente o **PC12 Studio**. Se a compilação não estiver disponível, o inicializador mantém os fallbacks para a central moderna anterior e, por fim, para o `pc12.exe` original.

## PC12 Modern

A camada **PC12 Modern** anterior continua disponível e adiciona uma central visual para Windows 7, com:

- painel de status do pacote;
- abertura do PC12 normal ou como administrador;
- detecção de portas COM;
- acesso ao Gerenciador de Dispositivos;
- checklist de comunicação com o TP02;
- limpeza segura de `lastfile.cpu` e `lastfile.dir`;
- acesso à pasta, ajuda e modo clássico.

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

### 1. Laboratório de arquivos do PC12

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

O **TP02 Bridge Lab** permite selecionar um `.PLC` e:

- localizar automaticamente os arquivos auxiliares do mesmo projeto;
- mostrar tamanho, SHA-256 e perfil textual/binário;
- extrair strings legíveis;
- gerar hexdump dos primeiros bytes;
- comparar dois arquivos byte a byte;
- apontar offsets e faixas alteradas;
- salvar relatório de engenharia reversa.

A comparação foi criada para um procedimento controlado: salvar dois projetos PC12 quase idênticos e alterar somente um item por vez, permitindo descobrir progressivamente a codificação do `.PLC`.

### 2. Comunicação serial somente leitura

O Bridge implementa a moldura ASCII do protocolo TP02 com checksum por complemento de dois e oferece, nesta etapa, somente comandos que não alteram o PLC:

- `PSR` — ler estado do PLC (`STOP`, `RUN` ou `ERROR`);
- `MCR` — ler estado de entradas, saídas, relés auxiliares e especiais;
- `MRV` — ler registradores `V`, `D`, `WS`, `WC` e `F`.

A configuração inicial da tela é:

- 19200 bps;
- 7 bits;
- paridade EVEN;
- 2 stop bits;
- estação 01;
- tempo de resposta 50 ms.

Esses parâmetros podem ser alterados para coincidir com `WS041/WS042` do PLC. O aplicativo também permite testar o prefixo de compatibilidade `::` além do formato padrão iniciado por `:`.

**Nenhum comando de escrita, RUN, STOP, limpeza de memória ou gravação de programa é exposto nesta etapa.**

## Como iniciar

### PC12 Studio — recomendado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_PC12.bat`

Esse inicializador recompila a versão atual e abre `PC12_Studio.exe`.

### PC12 Ladder Studio separado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_EDITOR_LADDER.bat`

### TP02 Bridge Lab separado

`PC12_v2.1_Windows7_v3_portatil/INICIAR_BRIDGE_TP02.bat`

### PC12 clássico

`PC12_v2.1_Windows7_v3_portatil/INICIAR_PC12_CLASSICO.bat`

## Arquivos principais

- `PC12Studio.cs` — shell unificado e interface principal;
- `ModernPC12.cs` — central moderna anterior;
- `LadderEditor.cs` — editor Ladder;
- `TP02BridgeLab.cs` — análise de projetos PC12 e comunicação TP02 somente leitura;
- `BUILD_INTERFACE_MODERNA.bat` — compilação local dos quatro aplicativos;
- `INICIAR_PC12.bat` — inicializador principal do PC12 Studio;
- `INICIAR_EDITOR_LADDER.bat` — Ladder Studio separado;
- `INICIAR_BRIDGE_TP02.bat` — Bridge Lab separado;
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
7. leitura do programa Boolean (`RBP`) e decodificação da linguagem de máquina;
8. importação `.PLC` -> `.pladder`;
9. geração controlada do formato nativo;
10. transferência de programa após validação com hardware;
11. substituição progressiva do PC12 legado.
