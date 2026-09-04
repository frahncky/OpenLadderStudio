# TP02 PG — análise estática de comandos de leitura do PC12

Data: 2026-09-04

## Objetivo

Identificar quadros PG que o `pc12.exe` original associa explicitamente a operações de leitura, reconstruir a sequência necessária e evitar tentativas isoladas que não correspondam ao fluxo real do PC12. Escrita, RUN/STOP remoto, download e apagamento permanecem fora desta investigação.

## Cadeia de transmissão confirmada

A rotina de comunicação em `0x46F5E6` usa o buffer global de TX em `0x4FA7A8`, o comprimento em `0x4FA8AC` e chega ao `WriteFile`. A resposta é recebida em área iniciada em `0x530230` e o software valida quadros cuja soma módulo 256 fecha em `0xFF`.

Assim, um quadro montado em `0x4FA7A8` e seguido de chamada a `0x46F5E6` não é apenas uma constante encontrada no executável: ele é preparado para transmissão serial pelo PC12.

## Read PLC Program — quadro principal

O handler rotulado no executável como `Read PLC Program...` monta, a partir de `0x4B1D62`, os seis bytes:

`34 03 00 00 A0 28`

Em seguida grava comprimento `6` em `0x4FA8AC` e chama a rotina de comunicação `0x46F5E6` em `0x4B1E39`.

Checksum:

`34 + 03 + 00 + 00 + A0 + 28 = FF` (módulo 256).

O quadro está estaticamente confirmado como uma requisição do fluxo Read PLC Program, mas **não é autônomo**.

## Evidência física do quadro 34 isolado

O Laboratório PG transmitiu `34 03 00 00 A0 28` uma única vez após HELLO válido em duas condições controladas:

1. PLC em STOP — HELLO `80 01 09 75` — resposta ao quadro de leitura: `RX []` após aproximadamente 4 s.
2. PLC em RUN — HELLO `C0 01 09 35` — resposta ao quadro de leitura: `RX []` após aproximadamente 4 s.

Portanto, o quadro principal não deve permanecer em `readOnlyAllowlist` quando usado isoladamente.

## Preâmbulo 38 00 C7 — semântica fechada estaticamente

A análise aprofundada mostrou que `38 00 C7` é montado explicitamente em apenas dois fluxos de programa localizados no executável:

- `0x4AF8D4`: fluxo `Compare PLC Program...`;
- `0x4B1B79`: fluxo `Read PLC Program...`.

Nos dois pontos o PC12 executa a mesma sequência:

- grava `38 00 C7` no buffer TX `0x4FA7A8`;
- define comprimento TX `3` em `0x4FA8AC`;
- define modo de recepção `2` em `0x560364`;
- define comprimento de dados esperado `2` em `0x560360`;
- chama a rotina serial `0x46F5E6`.

Checksum da requisição:

`38 + 00 + C7 = FF`.

### Formato esperado da resposta ao 38

Na rotina de recepção, quando `0x560364 = 2`, o PC12 espera comprimento total igual a `0x560360 + 3`. Para o preâmbulo 38 isso significa **5 bytes de resposta**.

Depois da recepção o PC12:

1. soma todos os bytes recebidos e exige soma módulo 256 igual a `0xFF`;
2. marca TIME-OUT quando não há quadro completo;
3. marca CHECK SUM ERROR quando a soma não fecha em `0xFF`;
4. testa o bit `0x80` do primeiro byte da resposta; se esse bit estiver ativo, entra no tratador de erro do PLC;
5. no tratador de erro, o byte de resposta em `0x530232` é usado como código de erro;
6. quando o bit `0x80` está limpo e o checksum é válido, o fluxo de Read/Compare combina `0x530232` e `0x530233` em big-endian para formar um valor de 16 bits antes de prosseguir.

Nos fluxos Read/Compare esse valor de 16 bits é usado como metadado do programa antes da leitura por blocos. Portanto, `38 00 C7` está classificado estaticamente como **consulta de pré-leitura/metadados do programa**, sem evidência de alteração de memória ou estado operacional.

### Política para a primeira validação física do 38

A validação de bancada deve transmitir somente `38 00 C7`, uma única vez, depois de um HELLO válido, e deve encerrar sem transmitir `34...` nem qualquer outro comando. A resposta será apenas caracterizada.

Critérios esperados para uma resposta normal:

- exatamente 5 bytes;
- soma módulo 256 = `FF`;
- bit `0x80` do primeiro byte limpo;
- bytes 2 e 3 interpretáveis como valor de 16 bits big-endian.

Se o bit `0x80` vier ativo, a resposta deve ser registrada como quadro de erro e nenhum comando posterior deve ser enviado.

## Estrutura da etapa 34 após o preâmbulo

Depois de um 38 válido, o PC12 monta `34 03 00 00 A0 28`, configura `0x560364 = 2` e `0x560360 = 0xF0`. Isso faz a rotina serial esperar até **243 bytes** (`0xF0 + 3`) para a resposta do primeiro bloco.

Ao longo do fluxo, os bytes de endereço do quadro `34` são recalculados e o checksum é recomposto para solicitar blocos subsequentes. Portanto, a leitura integral do programa é uma sequência de múltiplos blocos e não uma única requisição fixa.

A etapa 34 permanece desabilitada até que a resposta física ao 38 seja conhecida.

## Ramo condicional de senha

Existe ainda um ramo associado às mensagens `PassWord Error`, `PassWord Message` e `This Function Need Password !`.

Quando esse ramo é percorrido, o PC12 monta e transmite:

`14 00 EB`

Esse quadro **não é requisito universal**: o executável possui caminho que salta diretamente para a sequência posterior quando a condição de senha não está ativa. Por isso `14 00 EB` permanece somente como candidato relacionado a senha e não será testado como parte do caminho padrão de leitura.

## Read PLC System

O handler `Read PLC System...`, iniciado em torno de `0x4B43A0`, monta dois quadros em duas passagens:

1. `0A 03 60 00 AC E6`
2. `0A 03 60 AC AC 3A`

O checksum é calculado pelo próprio PC12, o comprimento é ajustado para 6 bytes e a rotina `0x46F5E6` é chamada para transmissão. Esses quadros permanecem desabilitados até que a sequência de sessão/leitura seja compreendida de ponta a ponta.

## Leituras de registradores

Também foram localizados handlers distintos para:

- `Read PLC Vxxxx Register...`
- `Read PLC Dxxxx Register...`
- `Read PLC WCxxx Register...`
- `Read PLC FILE Register...`

Os quadros desses handlers dependem do endereço/página solicitado. Como ainda falta fechar a semântica dos campos variáveis e o preâmbulo de sessão, eles não entram na allowlist.

## Estado atual do gate de segurança

Para a próxima validação, apenas `38 00 C7` pode entrar temporariamente na `readOnlyAllowlist`, com as seguintes proteções:

- autorização manual obrigatória no Laboratório PG;
- uma única transmissão após HELLO válido;
- nenhum `34...` ativo no mesmo ensaio;
- nenhum Read PLC System ativo;
- nenhum comando de senha ativo;
- captura e relatório do RX antes de qualquer passo posterior;
- `F0 00 0F`, `0F 00 F0`, RUN/STOP remoto, escrita, download e apagamento continuam bloqueados.

## Estado experimental do HELLO

A bancada correlacionou repetidamente:

- `C0 01 09 35` com PLC em RUN;
- `80 01 09 75` com PLC em STOP;
- `0D 01 09 E8` permanece observado, porém não classificado.

A diferença RUN/STOP observada está no bit `0x40` do primeiro byte, com checksum ajustado de forma correspondente.
