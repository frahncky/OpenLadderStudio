# TP02 PG — análise estática de comandos de leitura do PC12

Data: 2026-09-04

## Objetivo

Identificar quadros PG que o `pc12.exe` original associa explicitamente a operações de leitura, sem executar tentativas de escrita, RUN/STOP remoto, download ou apagamento no TP02.

## Cadeia de transmissão confirmada

A rotina de comunicação em `0x46F5E6` usa o buffer global de TX em `0x4FA7A8`, o comprimento em `0x4FA8AC` e chega ao `WriteFile`. A resposta é recebida em área iniciada em `0x530230` e o software valida quadros cuja soma módulo 256 fecha em `0xFF`.

Assim, um quadro montado em `0x4FA7A8` e seguido de chamada a `0x46F5E6` não é apenas uma constante encontrada no executável: ele é preparado para transmissão serial pelo PC12.

## Read PLC Program

O handler rotulado no executável como `Read PLC Program...` monta diretamente, a partir de `0x4B1D62`, os seis bytes:

`34 03 00 00 A0 28`

Em seguida grava comprimento `6` em `0x4FA8AC` e chama a rotina de comunicação `0x46F5E6` em `0x4B1E39`.

Checksum:

`34 + 03 + 00 + 00 + A0 + 28 = FF` (módulo 256).

Conclusão: `34 03 00 00 A0 28` está **estaticamente confirmado como requisição transmitida pelo handler Read PLC Program do PC12 original**. A resposta do TP02 ainda precisa ser caracterizada em bancada.

## Read PLC System

O handler `Read PLC System...`, iniciado em torno de `0x4B43A0`, monta dois quadros em duas passagens do laço iniciado em `0x4B4459`:

1. `0A 03 60 00 AC E6`
2. `0A 03 60 AC AC 3A`

O checksum é calculado pelo próprio PC12 subtraindo os cinco primeiros bytes de `0xFF`, o comprimento é ajustado para 6 bytes e a rotina `0x46F5E6` é chamada para transmissão.

Esses quadros ficam registrados como leitura estática conhecida, mas permanecem desabilitados no pacote de bancada até a validação do primeiro comando de leitura.

## Leituras de registradores

Também foram localizados handlers distintos para:

- `Read PLC Vxxxx Register...`
- `Read PLC Dxxxx Register...`
- `Read PLC WCxxx Register...`
- `Read PLC FILE Register...`

Os quadros desses handlers dependem do endereço/página solicitado. Como ainda falta fechar a semântica dos campos variáveis, eles não entram na allowlist de bancada nesta etapa.

## Gate de segurança para a primeira validação física

A primeira validação ativa usa somente `34 03 00 00 A0 28` e obedece às seguintes condições:

- só ocorre depois de um HELLO PG válido;
- exige marcação manual da opção de READ-ONLY verificado no Laboratório PG;
- o quadro precisa constar explicitamente em `readOnlyAllowlist`;
- é transmitido uma única vez por execução;
- qualquer ausência de resposta encerra a sequência;
- não existe outro comando ativo depois dele, apenas captura passiva;
- `F0 00 0F`, `0F 00 F0`, RUN, STOP, escrita, download e apagamento continuam fora desta validação.

## Estado experimental do HELLO

A bancada já correlacionou:

- `C0 01 09 35` com PLC em RUN;
- `80 01 09 75` com PLC em STOP;
- `0D 01 09 E8` permanece observado, porém não classificado.

A diferença RUN/STOP observada está no bit `0x40` do primeiro byte, com checksum ajustado de forma correspondente.
