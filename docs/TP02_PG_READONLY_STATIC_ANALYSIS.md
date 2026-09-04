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

## Evidência física do quadro isolado

O Laboratório PG transmitiu `34 03 00 00 A0 28` uma única vez após HELLO válido em duas condições controladas:

1. PLC em STOP — HELLO `80 01 09 75` — resposta ao quadro de leitura: `RX []` após aproximadamente 4 s.
2. PLC em RUN — HELLO `C0 01 09 35` — resposta ao quadro de leitura: `RX []` após aproximadamente 4 s.

Portanto, o quadro principal não deve permanecer em `readOnlyAllowlist` quando usado isoladamente.

## Preâmbulo obrigatório encontrado no fluxo

A análise aprofundada do mesmo handler mostrou que, antes de montar `34 03 00 00 A0 28`, o PC12 executa outro intercâmbio serial.

Em `0x4B1B79` o programa monta:

`38 00 C7`

Define comprimento `3` e chama `0x46F5E6` em `0x4B1BB0`. O fluxo contém novas chamadas de comunicação em caso de erro/repetição.

Depois desse intercâmbio, o PC12 consulta dados recebidos. Em `0x4B1D31–0x4B1D43`, os bytes em `0x530232` e `0x530233` são combinados em um valor de 16 bits **antes** de o quadro `34 03 00 00 A0 28` ser montado.

Isso demonstra que a requisição `34...` depende do estado/resposta produzidos por uma etapa anterior do protocolo.

### Evidência adicional sobre `38 00 C7`

A mesma construção de `38 00 C7` aparece também no fluxo rotulado `Compare PLC Program...`. Isso reforça a interpretação de que o quadro participa de uma preparação/consulta comum a operações de leitura/comparação de programa, mas sua semântica exata e sua resposta esperada ainda precisam ser fechadas antes de qualquer validação física.

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

Após os dois ensaios sem RX, o pacote de bancada voltou ao modo conservador:

- `readOnlyAllowlist` vazia;
- `34 03 00 00 A0 28` desabilitado e reclassificado como `CANDIDATE` para uso isolado;
- `38 00 C7` registrado como preâmbulo candidato, desabilitado;
- `14 00 EB` registrado como candidato do ramo de senha, desabilitado;
- Read PLC System e leituras de registradores continuam desabilitados;
- somente HELLO e captura passiva ficam ativos.

Nenhum novo quadro será transmitido até a análise estática definir a sequência e os critérios de resposta esperados.

## Estado experimental do HELLO

A bancada correlacionou repetidamente:

- `C0 01 09 35` com PLC em RUN;
- `80 01 09 75` com PLC em STOP;
- `0D 01 09 E8` permanece observado, porém não classificado.

A diferença RUN/STOP observada está no bit `0x40` do primeiro byte, com checksum ajustado de forma correspondente.
