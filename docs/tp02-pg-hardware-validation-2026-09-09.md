# TP02 PG — validação física do handshake e do preflight F0

Data: 2026-09-09

Este registro consolida uma captura de bancada feita com um TP02 físico usando o laboratório PG do OpenLadder Studio. Diferentemente da análise estática/emulação do PC12, os bytes abaixo foram recebidos do equipamento real.

## Configuração serial validada

- 19200 bit/s
- 8 bits de dados
- paridade ímpar (8O1)
- 1 stop bit
- DTR = ligado
- RTS = ligado

## Handshake

TX:

```text
43 4F 4E 2D 49 43 42 0D
```

ASCII:

```text
CON-ICB<CR>
```

Nas quatro primeiras tentativas não houve resposta. Na quinta tentativa o PLC respondeu em aproximadamente 248 ms:

```text
80 01 09 75
```

Soma módulo 256:

```text
80 + 01 + 09 + 75 = FF
```

A captura classificou essa resposta como `HELLO-STOP`, portanto a sessão ficou estabelecida com o PLC em STOP. Não houve eco exato do quadro transmitido.

## Status/preflight do PC12

Com a sessão já estabelecida, foi transmitido uma única vez o quadro previamente identificado por análise do PC12:

```text
F0 00 0F
```

O TP02 respondeu em aproximadamente 267 ms:

```text
00 02 10 22 CB
```

A soma também fecha em `FF`:

```text
00 + 02 + 10 + 22 + CB = FF
```

Aplicando o formato de quadro já reconstruído para o protocolo PG:

```text
CMD LEN payload[LEN] CHECKSUM
```

a resposta pode ser segmentada como:

```text
CMD      = 00
LEN      = 02
PAYLOAD  = 10 22
CHECKSUM = CB
```

Isso confirma fisicamente a previsão obtida no PC12 de que a resposta ao `F0 00 0F` possui 5 bytes, isto é, `LEN=2` mais os três bytes de enquadramento.

## Escuta passiva

Após a resposta ao F0 foi mantida escuta passiva por 3000 ms. Nenhum byte adicional foi recebido. Isso indica, para esta execução, que a resposta é autocontida e não inicia espontaneamente uma transferência adicional.

## Nível de evidência

Passam a ser considerados **confirmados em hardware**:

- perfil serial `19200 8O1`, DTR e RTS ligados;
- handshake `CON-ICB<CR>`;
- resposta `80 01 09 75` associada ao estado STOP na sessão observada;
- regra de checksum por soma módulo 256 igual a `FF` para os quadros binários observados;
- comando de status/preflight `F0 00 0F`;
- resposta física `00 02 10 22 CB` ao F0;
- comprimento total de 5 bytes da resposta ao F0;
- ausência de bytes adicionais nos 3 s posteriores à resposta.

Ainda **não** está estabelecida a semântica individual dos bytes de payload `10 22`. O código do PC12 consulta bits do primeiro byte recebido em rotinas de estado, mas não devemos atribuir significado definitivo a `10` ou `22` apenas a partir desta única captura.

## Próximo ensaio recomendado

O próximo teste de leitura deve preservar exatamente esta sequência de sessão:

```text
CON-ICB<CR>
    ↓
80 01 09 75
    ↓
F0 00 0F
    ↓
00 02 10 22 CB
    ↓
38 00 C7
```

`38 00 C7` deve permanecer classificado como candidato e não deve ser enviado automaticamente pela aplicação normal. O objetivo do próximo ensaio é apenas verificar se ele passa a responder quando executado **na mesma sessão e imediatamente após o F0 validado**, pois testes anteriores isolados não produziram resposta.

Comandos de escrita, RUN/STOP remoto, limpeza de memória e atualização de firmware permanecem bloqueados.