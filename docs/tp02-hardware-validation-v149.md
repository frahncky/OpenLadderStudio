# TP02 — campanha física READ-ONLY exaustiva (v1.49)

## Objetivo

A v1.49 transforma a validação física PG0A de amostras em uma varredura de toda a geometria de memória reconstruída do PC12, sem habilitar qualquer escrita.

## Perfil serial

- 19200 bit/s;
- 8 bits;
- paridade ímpar;
- 1 stop bit;
- DTR=OFF;
- RTS=OFF;
- sem handshake de hardware.

## Sequência segura

Antes da memória são executados:

1. `CON-ICB<CR>`;
2. `F0 00 0F`;
3. `38 00 C7`;
4. PG34 P0;
5. PG34 P80.

Em seguida, com **Varredura completa PG0A** marcada, são feitas 102 leituras:

| Área | Páginas | Quantidade normal por página |
|---|---:|---:|
| X | 3 | 16 bytes |
| Y | 3 | 16 bytes |
| C | 16 | 16 bytes |
| SC | 1 | 16 bytes |
| V | 16 | 128 bytes |
| D | 32 | 128 bytes |
| WC | 16 | 114 bytes |
| FL | 13 | 200 bytes |
| WS | 2 | 172 bytes |
| **Total** | **102** | — |

A última página de uma área pode ter `Q` menor quando necessário. WS mantém exatamente as duas consultas especiais reconstruídas do PC12 (`6000/AC` e `60AC/AC`), sem inferir páginas adicionais.

RTC (`53F9`, 14 bytes) e scan time (`6000`, 6 bytes) são testados separadamente depois da varredura.

## Allowlist

A barreira de segurança aceita somente:

- HELLO exato;
- F0 exato;
- 38 exato;
- PG34 com START 0 ou 80;
- PG0A que coincida byte a byte com uma página gerada por `Tp02PgMemoryProtocol.ReadAll`, com as quatro amostras rápidas conhecidas, RTC ou scan time.

Não basta um quadro ter opcode `0A` e checksum válido. Endereços/quantidades arbitrários são recusados antes de `SerialPort.Write`.

## PG34 P80

A v1.49 separa duas afirmações:

- **aceitação física do START=80**: chegou frame `LEN=F0` com checksum válido;
- **prova conclusiva da segunda página**: a página 80 contém END global >=80 ou conteúdo de programa diferente da página 0.

Se apenas a primeira afirmação puder ser feita, o relatório registra `PARTIAL`, não `PASS` conclusivo.

## Arquivos de evidência

Cada TX e RX continua sendo salvo em `.hex`, com timestamp no `session.log`. A sessão também produz:

- `hardware-validation-summary.txt`;
- `hardware-validation-summary.csv`;
- `hardware-validation-summary.json`;
- `next-physical-stages.txt`.

O JSON/CSV permite consolidar campanhas realizadas em diferentes firmwares/modelos.

## O que continua fora da campanha automática

- PG09;
- PG33;
- PG35;
- RUN/STOP remoto;
- Clear 03/04/0F/11;
- EEPROM 12/13;
- gate 14;
- qualquer opcode desconhecido ou não explicitamente permitido.

Para monitor Q=4 e operações capazes de alterar estado, preferir primeiro captura passiva com `OpenLadderTP02Capture.exe` e o PC12 original. A habilitação de TX deve ocorrer somente depois de evidência física e procedimento de recuperação/backup adequado.
