# OpenLadder Studio

**OpenLadder Studio** é um ambiente de engenharia para programação Ladder, configuração e monitoramento de PLCs. O projeto começou pela compatibilidade com o WEG TP02/PC12, mas a arquitetura atual é orientada a múltiplos fabricantes, perfis de dispositivo e drivers.

A versão oficial do produto é definida em `PC12_v2.1_Windows7_v3_portatil/version.txt`. As releases são geradas automaticamente a partir da `main` após validação e build no GitHub Actions.

## Objetivos do projeto

- oferecer um editor Ladder moderno e independente de fabricante;
- separar modelo Ladder, perfis de PLC, drivers e interface;
- suportar comunicação e monitoramento por drivers específicos e protocolos genéricos;
- manter escrita/download de programa explicitamente separados das funções de leitura;
- preservar compatibilidade com Windows 7 enquanto a arquitetura é modernizada;
- evoluir de forma incremental sem depender do PC12 como núcleo do produto.

## Recursos atuais

- shell principal multi-PLC;
- editor Ladder com contatos, bobinas, ramificações, temporizadores, contadores, SET/RESET, bordas, funções e END;
- modelo Ladder universal e verificação de portabilidade;
- catálogo de controladores por fabricante, família e modelo;
- perfis personalizados Modbus RTU e Modbus TCP;
- persistência de conexão e mapa de memória por PLC;
- monitor Modbus com FC01, FC02, FC03 e FC04;
- leitura automática em múltiplos blocos;
- monitoramento periódico;
- histórico e tendências de sinais;
- exportação CSV;
- suporte de leitura e pesquisa para WEG TP02;
- atualizador e instalador próprios;
- modo foco do editor com `F11`.

## Situação dos drivers

| Perfil | Comunicação | Monitoramento | Leitura de programa | Download Ladder |
|---|---:|---:|---:|---:|
| WEG TP02-60MR | Sim | Sim | Sim, via RBP | Não |
| Modbus RTU genérico | Sim | Sim | Não | Não |
| Modbus TCP genérico | Sim | Sim | Não | Não |
| Perfis Modbus personalizados | Sim | Sim | Não | Não |
| Outros fabricantes cadastrados | Planejado | Planejado | Planejado | Planejado |

A presença de um perfil no catálogo não significa que exista compilador ou protocolo de programação implementado para esse PLC.

## Arquitetura

Fluxo conceitual:

```text
Interface
   ↓
Serviços de aplicação
   ↓
Modelo Ladder / domínio
   ↑
Drivers e infraestrutura
   ↓
PLC
```

A regra de dependência é detalhada em [`docs/SOFTWARE_ARCHITECTURE.md`](docs/SOFTWARE_ARCHITECTURE.md). A arquitetura específica de drivers também está documentada em [`docs/PLC_DRIVER_ARCHITECTURE.md`](docs/PLC_DRIVER_ARCHITECTURE.md).

## Estrutura do repositório

```text
.github/workflows/                 CI, build e publicação de releases
assets/branding/                   identidade visual e fonte vetorial do ícone
docs/                              arquitetura, drivers, UI e pesquisa TP02
installer/                         template do instalador Inno Setup
scripts/                           validações e preparação de release
PC12_v2.1_Windows7_v3_portatil/   fontes atuais, ferramentas e compatibilidade legada
CHANGELOG.md                       histórico de versões
CONTRIBUTING.md                    regras de contribuição
```

O diretório `PC12_v2.1_Windows7_v3_portatil` ainda contém uma mistura histórica de fontes e compatibilidade. Ele é tratado como dívida técnica controlada; a migração gradual para uma estrutura `src/` está descrita na documentação de arquitetura.

## Identidade e interface

A interface usa tema escuro, destaque verde OpenLadder e cores semânticas por função. O ícone oficial combina trilhos Ladder verdes, rung branco e bloco PLC azul, sem texto, para permanecer legível em tamanhos pequenos.

O `.ico` é gerado em múltiplas resoluções: 16, 24, 32, 48, 64, 128 e 256 px.

As regras visuais estão em [`docs/UI_GUIDELINES.md`](docs/UI_GUIDELINES.md).

## Build

No Windows:

```bat
cd PC12_v2.1_Windows7_v3_portatil
BUILD_INTERFACE_MODERNA.bat
```

Antes do build/release, a estrutura pode ser validada com:

```powershell
.\scripts\ValidateProject.ps1
```

O instalador é preparado a partir de `version.txt` por:

```powershell
.\scripts\PrepareInstaller.ps1
```

Isso gera `installer/PC12Studio.build.iss`, que é um arquivo temporário e não deve ser versionado.

## Versionamento e releases

- `version.txt` é a fonte principal da versão;
- o shell lê essa versão durante o processo de build;
- o template do instalador recebe a mesma versão automaticamente;
- o CI valida a estrutura antes de compilar;
- a release usa `CHANGELOG.md` como fonte das notas.

Esse fluxo evita divergências como aplicativo em uma versão e instalador em outra.

## Atualização do aplicativo

Durante uma atualização, o instalador pode fechar o OpenLadder Studio para substituir os arquivos. Se o aplicativo estava aberto antes da instalação, ele é reaberto ao final. Se estava fechado, a atualização silenciosa não força sua abertura.

## Segurança operacional

As ferramentas modernas permanecem conservadoras quanto a operações de escrita. Comandos de RUN/STOP, limpeza de memória, alteração de saídas e download de programa só devem ser habilitados após implementação e validação específica no hardware real.

## Desenvolvimento

Consulte:

- [`CONTRIBUTING.md`](CONTRIBUTING.md) — fluxo de contribuição e regras de código;
- [`CHANGELOG.md`](CHANGELOG.md) — histórico das versões;
- [`docs/SOFTWARE_ARCHITECTURE.md`](docs/SOFTWARE_ARCHITECTURE.md) — organização e plano de evolução;
- [`docs/UI_GUIDELINES.md`](docs/UI_GUIDELINES.md) — identidade e padrões de interface.
