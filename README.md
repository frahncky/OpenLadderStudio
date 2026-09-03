# UpgradeInterfacePLC

Modernização da experiência de uso do **PC12 Design Center 2.1** para o PLC **WEG TP02**, preservando o executável legado e desenvolvendo um editor Ladder moderno próprio.

## PC12 Modern

A camada **PC12 Modern** adiciona uma central visual atual para Windows 7, com:

- painel de status do pacote;
- abertura do PC12 normal ou como administrador;
- detecção de portas COM;
- acesso ao Gerenciador de Dispositivos;
- checklist de comunicação com o TP02;
- limpeza segura de `lastfile.cpu` e `lastfile.dir`;
- acesso à pasta, ajuda e modo clássico.

## PC12 Ladder Studio — Etapa 2

O editor Ladder próprio foi ampliado usando a nomenclatura real do TP02.

### Elementos e instruções

- contatos normalmente abertos e fechados;
- pontos `X0001–X0384`, `Y0001–Y0384`, `C0001–C2048` e `SC001–SC128`;
- bobina `OUT` para `Y` e `C`;
- ramificações paralelas com contatos NA/NF;
- `TMR` com identificadores `V0001–V0256`;
- temporizador com ou sem entrada de RESET;
- `CNT` com identificadores `V0001–V0256`;
- presets diretos ou indiretos por `D0001–D2048`;
- `SET` — função especial **F-23**;
- `RESET` — função especial **F-24**;
- detector de borda de subida — **F-05**;
- detector de borda de descida — **F-06**;
- bloco genérico `FUN` para funções especiais do TP02;
- `END` — **F-00**.

O editor também verifica se um mesmo registrador `Vxxxx` foi usado simultaneamente por TMR/CNT, pois temporizadores e contadores compartilham os identificadores `V0001–V0256` no TP02.

### Edição

- múltiplos rungs;
- seleção e exclusão de elementos;
- edição por duplo clique;
- adicionar/remover rungs;
- desfazer (`Ctrl+Z`);
- novo, abrir e salvar projeto;
- formato `.pladder` versão 2;
- leitura dos projetos `.pladder` da Etapa 1;
- validação estrutural básica pelo botão `VALIDAR`.

A validação do Ladder Studio **não substitui a compilação oficial do PC12**. A transferência para o PLC permanece desabilitada até que o formato nativo e o protocolo de comunicação sejam reproduzidos e testados com segurança.

## Como iniciar

### Central PC12 Modern

`PC12_v2.1_Windows7_v3_portatil/INICIAR_PC12.bat`

### PC12 Ladder Studio

`PC12_v2.1_Windows7_v3_portatil/INICIAR_EDITOR_LADDER.bat`

Se os executáveis modernos ainda não existirem, os scripts usam `BUILD_INTERFACE_MODERNA.bat` para compilá-los com o .NET Framework disponível no Windows.

## Arquivos principais

- `ModernPC12.cs` — central moderna;
- `LadderEditor.cs` — editor Ladder;
- `BUILD_INTERFACE_MODERNA.bat` — compilação local;
- `INICIAR_PC12.bat` — central moderna;
- `INICIAR_EDITOR_LADDER.bat` — Ladder Studio;
- `INICIAR_PC12_CLASSICO.bat` — PC12 legado.

## Compatibilidade

As interfaces usam **Windows Forms + .NET Framework**, sem bibliotecas externas, com foco em Windows 7.

## Arquitetura de transição

1. central moderna e diagnóstico;
2. editor Ladder moderno;
3. instruções e endereçamento reais do TP02;
4. importação/exportação do formato nativo do PC12;
5. comunicação serial validada;
6. leitura, monitoramento e transferência de programa;
7. substituição progressiva do PC12 legado.
