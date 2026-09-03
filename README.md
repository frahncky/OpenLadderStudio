# UpgradeInterfacePLC

Modernização da experiência de uso do **PC12 Design Center 2.1** para o PLC **TP02**, preservando o executável legado e iniciando a construção de um editor Ladder moderno próprio.

## PC12 Modern

A camada **PC12 Modern** adiciona uma central visual mais atual para Windows 7, com:

- painel inicial com status do pacote;
- abertura do PC12 em modo normal ou como administrador;
- detecção das portas COM disponíveis;
- acesso rápido ao Gerenciador de Dispositivos;
- checklist de comunicação com o TP02;
- ferramenta para limpar `lastfile.cpu` e `lastfile.dir` sem apagar projetos;
- abertura da pasta do software;
- acesso à ajuda local;
- modo clássico disponível a qualquer momento.

## PC12 Ladder Studio — Etapa 1

O repositório agora também possui a primeira versão do **editor Ladder moderno próprio**.

Recursos já iniciados:

- visual Ladder redesenhado;
- múltiplos rungs;
- contato normalmente aberto (NA);
- contato normalmente fechado (NF);
- bobina de saída;
- endereçamento `X`, `Y`, `M`, `T` e `C`;
- seleção e exclusão de elementos;
- edição de endereço por duplo clique;
- adicionar e remover rungs;
- desfazer alterações (`Ctrl+Z`);
- novo projeto, abrir e salvar;
- formato de projeto moderno `.pladder`;
- atalhos de teclado;
- compatibilidade com Windows Forms/.NET Framework.

Nesta etapa o Ladder Studio é um **editor local**. A geração do formato nativo do PC12 e a transferência do programa para o TP02 serão habilitadas somente depois da validação do formato dos projetos e do protocolo de comunicação, para evitar gravações incorretas no PLC.

## Como iniciar

### Central PC12 Modern

Abra:

`PC12_v2.1_Windows7_v3_portatil/INICIAR_PC12.bat`

### Editor Ladder moderno

Abra:

`PC12_v2.1_Windows7_v3_portatil/INICIAR_EDITOR_LADDER.bat`

Se `PC12_Ladder.exe` ainda não existir, o script executa a compilação automaticamente.

## Arquivos da modernização

- `ModernPC12.cs` — código-fonte da central moderna;
- `LadderEditor.cs` — primeira implementação do editor Ladder moderno;
- `BUILD_INTERFACE_MODERNA.bat` — compila a central e o Ladder Studio;
- `INICIAR_PC12.bat` — inicializador da central moderna;
- `INICIAR_EDITOR_LADDER.bat` — inicializador do novo editor Ladder;
- `INICIAR_PC12_CLASSICO.bat` — inicialização direta do software legado.

## Compatibilidade

As interfaces foram construídas com **Windows Forms + .NET Framework**, sem bibliotecas externas, para manter o pacote simples e compatível com Windows 7.

## Arquitetura de transição

O `pc12.exe` original permanece no pacote porque ainda é a referência para comunicação com o TP02 e compatibilidade com projetos antigos. A modernização está sendo feita progressivamente:

1. central moderna e diagnóstico;
2. editor Ladder moderno;
3. modelo completo de instruções do TP02;
4. importação/exportação de projetos;
5. comunicação serial validada;
6. leitura e transferência de programa para o PLC;
7. substituição progressiva da dependência do PC12 legado.
