# TP02 PG — análise estática de comandos de leitura do PC12

Data: 2026-09-04

## Objetivo

Identificar quadros PG que o `pc12.exe` original associa explicitamente a operações de leitura, reconstruir a sequência necessária e evitar tentativas isoladas que não correspondam ao fluxo real do PC12. Escrita, RUN/STOP remoto, download e apagamento permanecem fora desta investigação.

## Camada serial confirmada

A rotina de comunicação em `0x46F5E6` usa o buffer global de TX em `0x4FA7A8`, o comprimento em `0x4FA8AC` e chega ao `WriteFile`. A resposta é recebida em área iniciada em `0x530230` e o software valida quadros cuja soma módulo 256 fecha em `0xFF`.

A abertura da porta no PC12 original força 19200 bps, 8 bits, paridade ímpar e 1 stop bit quando a taxa é 19200. Isso coincide com o perfil validado fisicamente no TP02: `19200 / 8O1`.

## Handshake PG

O PC12 copia `CON-ICB`, acrescenta `0D`, define comprimento TX 8 e transmite pelo mesmo caminho serial. A bancada correlacionou repetidamente:

- `C0 01 09 35` com PLC em RUN;
- `80 01 09 75` com PLC em STOP;
- `0D 01 09 E8` permanece observado, porém não classificado.

A diferença RUN/STOP observada está no bit `0x40` do primeiro byte, com checksum ajustado de forma correspondente.

## Status/preflight F0 00 0F — papel fechado estaticamente

A análise da rotina `0x46F300` alterou a interpretação anterior de `F0 00 0F`.

Essa rotina salva o quadro e os parâmetros de recepção correntes, monta temporariamente:

`F0 00 0F`

configura:

- comprimento TX = 3;
- modo de recepção `2`;
- comprimento de dados esperado = 2;

faz a troca serial, com novas tentativas em caso de falha, e depois restaura o quadro e os parâmetros anteriores.

No modo de recepção 2, o PC12 espera `dados + 3`, portanto a resposta ao F0 deve ter **5 bytes**.

A rotina de conexão chama esse preflight antes de prosseguir com o uso normal do PLC. Depois da resposta, o código de conexão examina o primeiro byte recebido e usa especificamente o bit `0x40` para definir o estado operacional apresentado pelo software. O parser comum também trata o bit `0x80` como indicação de resposta de erro e valida soma módulo 256 igual a `FF`.

Conclusão: `F0 00 0F` é uma **consulta de status/preflight da conexão**, não o comando de apagamento. O comando associado ao Clear All Memory continua sendo `0F 00 F0` e permanece bloqueado.

Uma resposta física de cinco bytes ao F0 já havia sido observada em bancada anteriormente: `40 02 10 22 8B`, cuja soma módulo 256 fecha em `FF`. A nova validação serve para reproduzir e caracterizar esse quadro de forma controlada em uma sessão conhecida.

## Read PLC Program — quadro 38 00 C7

O quadro `38 00 C7` é montado explicitamente nos fluxos `Compare PLC Program...` e `Read PLC Program...`.

Nos dois pontos o PC12:

- grava `38 00 C7` no buffer TX;
- define comprimento TX 3;
- define modo de recepção 2;
- define comprimento de dados esperado 2;
- espera, portanto, uma resposta total de 5 bytes;
- valida checksum e o bit de erro antes de usar os bytes de dados como metadado do programa.

### Resultado físico do 38 isolado após HELLO

O Laboratório PG transmitiu `38 00 C7` uma única vez depois de HELLO válido com o PLC em STOP. O TP02 retornou `RX []` após aproximadamente 4 s.

Isso demonstra que `38 00 C7` também não deve ser tratado como comando autônomo logo após o HELLO. A sequência estática de conexão mostra agora o passo que faltava: o status/preflight `F0 00 0F` ocorre antes do uso normal do PLC.

Por isso o próximo ensaio não transmite o 38. Primeiro será caracterizada somente a resposta ao F0. Se o F0 for reproduzido com o formato esperado, o ensaio seguinte poderá testar `HELLO -> F0 -> 38` na mesma sessão, mantendo `34...` desabilitado.

## Read PLC Program — quadro principal 34 03 00 00 A0 28

O handler `Read PLC Program...` monta:

`34 03 00 00 A0 28`

com comprimento TX 6, modo de recepção 2 e comprimento de dados esperado `0xF0`. Portanto, a rotina espera até **243 bytes** (`0xF0 + 3`) para a resposta do bloco.

O quadro foi testado isoladamente após HELLO válido em duas condições:

1. PLC em STOP — `RX []` após aproximadamente 4 s;
2. PLC em RUN — `RX []` após aproximadamente 4 s.

Logo, `34 03 00 00 A0 28` permanece `CANDIDATE` e desabilitado até que as etapas anteriores da sessão sejam validadas.

## Ramo condicional de senha

O quadro `14 00 EB` aparece em um ramo associado às mensagens de senha. O executável também possui caminho que ignora esse ramo, portanto ele não é requisito universal para leitura de programa e permanece desabilitado.

## Read PLC System

O fluxo `Read PLC System...` monta dois quadros:

1. `0A 03 60 00 AC E6`
2. `0A 03 60 AC AC 3A`

Ambos permanecem desabilitados até que a sessão de conexão e a sequência de leitura sejam compreendidas de ponta a ponta.

## Leituras de registradores

Também existem handlers distintos para `Vxxxx`, `Dxxxx`, `WCxxx` e `FILE`. Os quadros dependem de endereços e páginas dinâmicas e continuam fora da allowlist nesta fase.

## Safety Gate — motor 1.2

O motor 1.1 bloqueava internamente tanto `0F 00 F0` quanto `F0 00 0F` porque a semântica do F0 ainda não estava fechada.

Com a análise estática acima, o motor 1.2 passa a manter bloqueio interno permanente somente para:

`0F 00 F0` — Clear All Memory.

`F0 00 0F` não é liberado de forma geral. Ele só pode ser transmitido quando todas as condições abaixo forem verdadeiras:

- etapa classificada como `READ_ONLY_VERIFIED`;
- quadro presente explicitamente em `readOnlyAllowlist`;
- autorização manual marcada pelo operador;
- pacote de teste habilita a etapa;
- nenhum bloqueio do pacote se aplica.

Na validação `2026.09.04.8`, somente o F0 entra na allowlist. `38...`, `34...`, senha, Read PLC System, RUN/STOP remoto, escrita, download e apagamento permanecem desabilitados.

## Próxima sequência experimental

Etapa atual:

`HELLO -> F0 -> captura passiva`

Se a resposta F0 de 5 bytes for confirmada, a próxima sequência candidata será:

`HELLO -> F0 -> 38 -> captura passiva`

Somente depois de um 38 válido será considerada a inclusão do primeiro bloco `34...`.
