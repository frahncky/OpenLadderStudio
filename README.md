# UpgradeInterfacePLC

Modernização da experiência de uso do **PC12 Design Center 2.1** para o PLC **TP02**, preservando o executável legado e sua compatibilidade com os arquivos de projeto e com a comunicação existente.

## O que foi modernizado

A nova camada **PC12 Modern** adiciona uma central visual mais atual para Windows 7, com:

- painel inicial com status do pacote;
- faixas visuais de compatibilidade, dependências e fallback logo na abertura;
- painel principal em destaque com ações rápidas para abrir o PC12 e acessar a conexão serial;
- cabeçalhos visuais modernos também nas telas de conexão, ferramentas e ajuda;
- abertura do PC12 em modo normal ou como administrador;
- detecção das portas COM disponíveis;
- cópia rápida da porta COM selecionada para configurar a mesma porta dentro do PC12;
- resumo visual da ajuda local e do último projeto conhecido;
- atalho para abrir a pasta do último projeto salvo quando o caminho ainda existe;
- acesso rápido ao Gerenciador de Dispositivos;
- checklist de comunicação com o TP02;
- cópia de diagnóstico do ambiente para facilitar suporte;
- ferramenta para limpar `lastfile.cpu` e `lastfile.dir` sem apagar projetos;
- abertura da pasta do software;
- acesso à ajuda local;
- modo clássico disponível a qualquer momento.

## Como usar

Abra:

`PC12_v2.1_Windows7_v3_portatil/INICIAR_PC12.bat`

Na primeira execução, o script tenta compilar automaticamente a interface `PC12_Moderno.exe` usando o compilador C# do .NET Framework instalado no Windows 7. Depois disso, as próximas execuções abrem diretamente a nova central.

Se a compilação não estiver disponível, o script informa o motivo e inicia o `pc12.exe` original como fallback, portanto a funcionalidade existente não é perdida.

## Arquivos da modernização

- `ModernPC12.cs` — código-fonte da nova interface;
- `BUILD_INTERFACE_MODERNA.bat` — compilação local sem dependências externas;
- `INICIAR_PC12.bat` — inicializador padrão, agora priorizando a interface moderna;
- `INICIAR_PC12_CLASSICO.bat` — inicialização direta do software legado.

## Compatibilidade

A interface foi construída somente com **Windows Forms + .NET Framework**, sem bibliotecas de terceiros. O objetivo é manter o pacote simples e utilizável em Windows 7.

## Limitação importante

O repositório possui o `pc12.exe` compilado, mas não contém o código-fonte original do PC12. Por isso, nesta etapa a modernização funciona como uma camada de inicialização, diagnóstico e suporte.

Os menus, o editor ladder e as janelas internas do PC12 continuam sendo renderizados pelo executável legado. Uma modernização completa dessas telas exigiria acesso ao código-fonte original ou a reimplementação controlada do editor e do protocolo de comunicação.
