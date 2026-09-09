# WEG TP02 — reprodução controlada do preflight PG F0

Data: 2026-09-09

Este registro complementa `tp02-pg-experimental-findings-2026-09-04.md` com a reprodução física controlada do quadro `F0 00 0F` no mesmo enlace PG usado pelo PC12.

## Ambiente validado

- perfil serial: **19200 bps, 8O1**;
- `DTR=ON`;
- `RTS=ON`;
- PLC em **STOP**, confirmado pelo HELLO;
- handshake: `43 4F 4E 2D 49 43 42 0D` (`CON-ICB<CR>`).

O TP02 não respondeu nas primeiras tentativas de HELLO e respondeu posteriormente, comportamento coerente com as campanhas anteriores.

## Sequência física reproduzida

### 1. HELLO

TX:

```text
43 4F 4E 2D 49 43 42 0D
```

RX:

```text
80 01 09 75
```

Soma módulo 256:

```text
80 + 01 + 09 + 75 = FF
```

Latência observada: aproximadamente **248 ms**.

A resposta `80 01 09 75` permanece correlacionada experimentalmente com PLC em **STOP**.

### 2. Status/preflight

TX:

```text
F0 00 0F
```

O próprio quadro transmitido fecha em `FF`:

```text
F0 + 00 + 0F = FF
```

RX reproduzido:

```text
00 02 10 22 CB
```

Soma módulo 256:

```text
00 + 02 + 10 + 22 + CB = FF
```

Latência observada: aproximadamente **267 ms**.

A recepção tem exatamente **5 bytes**, como previsto pela análise estática do `pc12.exe` para a rotina de preflight.

### 3. Escuta passiva

Após a resposta ao F0, foi realizada escuta passiva por aproximadamente 3 s.

Resultado:

```text
nenhum byte adicional
```

Portanto a resposta de 5 bytes é autocontida; não houve continuação espontânea do quadro no ensaio.

## Comparação com a observação física anterior

Uma captura física anterior havia registrado:

```text
40 02 10 22 8B
```

A nova reprodução em STOP foi:

```text
00 02 10 22 CB
```

Os bytes centrais são idênticos:

```text
02 10 22
```

A única diferença de conteúdo está no bit `0x40` do primeiro byte:

```text
00 -> bit 0x40 limpo
40 -> bit 0x40 ativo
```

O checksum compensa exatamente a alteração:

```text
CB -> 8B
```

Isso é coerente com a análise estática do PC12, que consulta o bit `0x40` do primeiro byte recebido depois do F0 para distinguir estado operacional. Como o ensaio atual estava em STOP e retornou primeiro byte `00`, a evidência de bancada reforça fortemente a interpretação:

```text
bit 0x40 = 0  -> STOP observado
bit 0x40 = 1  -> provável RUN
```

A associação da captura antiga `40 02 10 22 8B` a RUN ainda deve ser reproduzida em um ensaio explicitamente realizado com o PLC em RUN antes de ser promovida a fato experimental confirmado.

## Estrutura provável da resposta F0

A resposta se encaixa naturalmente no formato binário já recuperado do PC12:

```text
STATUS/CMD  LEN  PAYLOAD[LEN]  CHECKSUM
```

Para o ensaio atual:

```text
00  02  10 22  CB
```

Interpretação de trabalho:

- byte 0: status, contendo pelo menos o bit operacional `0x40`;
- byte 1: comprimento `02`;
- bytes 2–3: payload `10 22`;
- byte 4: checksum que fecha a soma em `FF`.

A semântica de `10 22` ainda não está estabelecida.

## Estado de confiança

| Evidência | Estado |
|---|---|
| Perfil 19200 8O1, DTR/RTS ON | confirmado em bancada |
| HELLO `80 01 09 75` em STOP | confirmado e reproduzido |
| TX `F0 00 0F` como preflight | confirmado por análise estática e bancada |
| Resposta F0 de 5 bytes | confirmado em bancada |
| F0 STOP `00 02 10 22 CB` | confirmado neste ensaio |
| Soma módulo 256 = `FF` | confirmada |
| `LEN=02` com dois bytes de payload | evidência forte |
| bit `0x40` como indicador operacional | evidência forte |
| F0 `40 02 10 22 8B` = RUN | provável; falta reprodução explícita em RUN |
| significado de `10 22` | desconhecido |

## Próximo ensaio recomendado

O próximo passo de baixo risco é repetir exatamente:

```text
HELLO -> F0
```

com o PLC explicitamente em **RUN** e verificar se a resposta muda de:

```text
00 02 10 22 CB
```

para:

```text
40 02 10 22 8B
```

Depois dessa confirmação, o próximo candidato de leitura é `38 00 C7` **na mesma sessão, depois do F0**. Ele continua desabilitado no pacote padrão até esse ensaio específico ser autorizado. `34 03 00 00 A0 28` deve permanecer desabilitado até a resposta ao `38` ser caracterizada.

## Segurança

Continuam bloqueados ou fora do ensaio:

- `0F 00 F0` — Clear All Memory;
- RUN/STOP remoto;
- escrita de registradores e bobinas;
- download de programa;
- atualização de firmware;
- quadros de senha não compreendidos.
