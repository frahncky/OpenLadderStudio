# WEG TP02 — evidências experimentais do protocolo PG

Data de consolidação: 2026-09-04

Este documento registra apenas o que já foi observado em bancada ou confirmado por análise estática do `pc12.exe`, separando fatos, hipóteses e comandos ainda não validados.

## 1. Ambiente físico validado

- PLC: WEG TP02.
- Comunicação PG pelo mesmo enlace usado pelo PC12.
- Perfil serial que efetivamente responde em bancada: **19200 bps, 8 bits, paridade ODD, 1 stop bit (8O1), DTR=ON, RTS=ON**.
- O TP02 frequentemente ignora várias tentativas consecutivas antes de responder, mesmo com o perfil correto.
- O laboratório atual preserva TX/RX, latência, soma módulo 256 e relatórios TXT/JSON.

## 2. Handshake PG confirmado

O PC12 transmite:

`43 4F 4E 2D 49 43 42 0D`

ASCII:

`CON-ICB<CR>`

O checksum simples desse TX não fecha em `FF`; a regra de soma módulo 256 = `FF` é aplicada aos quadros binários de resposta/PG observados.

## 3. Respostas físicas observadas ao HELLO

Foram observadas três variantes com soma módulo 256 igual a `FF`:

| Quadro | Soma | Estado experimental |
|---|---:|---|
| `80 01 09 75` | `FF` | correlacionado com PLC em **STOP** |
| `C0 01 09 35` | `FF` | correlacionado com PLC em **RUN** |
| `0D 01 09 E8` | `FF` | fisicamente observado, ainda **não classificado** |

Nos dois quadros RUN/STOP os bytes `01 09` permaneceram constantes. A diferença está no bit `0x40` do primeiro byte:

- `80 = 1000 0000` → STOP observado;
- `C0 = 1100 0000` → RUN observado.

O checksum compensa exatamente a alteração do primeiro byte.

## 4. Campanhas controladas RUN/STOP

### STOP

Campanha de 12 ciclos:

- respostas conhecidas: 5;
- `80 01 09 75`: 5;
- `C0 01 09 35`: 0;
- sem RX: 7;
- desconhecidas: 0;
- trocas de variante: 0;
- latência das respostas válidas: aproximadamente 239 ms.

### RUN — primeira campanha

Campanha de 12 ciclos:

- respostas conhecidas: 4;
- `C0 01 09 35`: 4;
- `80 01 09 75`: 0;
- sem RX: 8;
- desconhecidas: 0;
- trocas de variante: 0;
- latência: aproximadamente 230–240 ms.

### RUN — repetição

Nova campanha de 12 ciclos confirmou novamente:

- `C0=4`;
- `80=0`;
- `NO_RX=8`;
- `unknown=0`;
- `switches=0`;
- latência `min/avg/max ≈ 229/236,8/240 ms`.

### Conclusão experimental do HELLO

A correlação **STOP → `80 01 09 75`** e **RUN → `C0 01 09 35`** foi reproduzida sem mistura entre as duas variantes nos ensaios controlados realizados até aqui.

A interpretação do bit `0x40` como indicador RUN/STOP é, portanto, uma conclusão experimental forte, embora continue documentada como comportamento observado do TP02 testado.

## 5. Quadro `34 03 00 00 A0 28`

A análise estática do `pc12.exe` confirmou que esse quadro é montado no handler **Read PLC Program** e transmitido pela rotina serial do PC12.

Checksum:

`34 + 03 + 00 + 00 + A0 + 28 = FF`

### Testes físicos isolados

Foram realizados dois testes controlados depois de um HELLO válido:

1. PLC em STOP → `HELLO-STOP` → TX `34 03 00 00 A0 28` → `RX []` após ~4 s.
2. PLC em RUN → `HELLO-RUN` → TX `34 03 00 00 A0 28` → `RX []` após ~4 s.

Conclusão: **o quadro 34 não funciona isoladamente** e não deve ser tratado como comando autônomo de leitura.

Ele permanece `CANDIDATE` e fora da `readOnlyAllowlist` até a sequência completa ser reproduzida.

## 6. Quadro `38 00 C7`

A análise estática mostrou `38 00 C7` nos fluxos:

- `Read PLC Program`;
- `Compare PLC Program`.

No PC12, ele é enviado antes do quadro `34...`. A rotina de recepção é configurada para esperar **5 bytes** no total e, no caminho normal, bytes da resposta são usados como metadados antes da leitura do programa.

### Teste físico isolado após HELLO

Com o PLC em STOP:

- HELLO confirmado como `80 01 09 75`;
- TX `38 00 C7` uma única vez;
- `RX []` após ~4 s;
- resultado do laboratório: `STEP_NO_RX`.

Conclusão: **`38 00 C7` também não responde quando usado imediatamente após o HELLO**. Isso indica que o fluxo real do PC12 possui uma etapa anterior de sessão/preflight.

Ele permanece desabilitado até que essa etapa anterior seja validada.

## 7. Quadro `F0 00 0F`

A análise estática mais recente do `pc12.exe` reposicionou esse quadro: ele aparece na rotina de **status/preflight da conexão**, anterior ao fluxo normal de operação.

O PC12 configura a recepção para **5 bytes** e depois consulta o bit `0x40` do primeiro byte recebido para inferir o estado operacional do PLC.

Há também uma observação física anterior de resposta após `F0 00 0F`:

`40 02 10 22 8B`

Soma:

`40 + 02 + 10 + 22 + 8B = FF`

Essa observação foi única e ainda precisa ser reproduzida de forma controlada na sequência atual.

Estado atual:

- `F0 00 0F` está autorizado somente como `READ_ONLY_VERIFIED` no pacote de teste vigente;
- exige autorização manual do operador;
- deve ser enviado uma única vez após HELLO válido;
- `38...` e `34...` permanecem desabilitados no mesmo ensaio até a resposta ao F0 ser caracterizada.

## 8. Quadro `0F 00 F0`

Permanece **bloqueado**.

A análise estática o associa a **Clear All Memory**. Ele não deve ser confundido com `F0 00 0F`.

## 9. Ramo de senha

O quadro:

`14 00 EB`

foi encontrado em um ramo condicional ligado a mensagens de senha no PC12.

Ele não é requisito universal do fluxo de leitura e continua como `CANDIDATE`, desabilitado e fora da allowlist.

## 10. Read PLC System

Foram identificados estaticamente:

- `0A 03 60 00 AC E6`;
- `0A 03 60 AC AC 3A`.

Ambos são montados pelo handler `Read PLC System`, mas continuam desabilitados até que a sessão/preflight seja compreendida e validada de ponta a ponta.

## 11. Leituras de registradores

Foram localizados handlers distintos no PC12 para:

- `Read PLC Vxxxx Register`;
- `Read PLC Dxxxx Register`;
- `Read PLC WCxxx Register`;
- `Read PLC FILE Register`.

Os quadros possuem campos variáveis de endereço/página e ainda não foram liberados para bancada.

## 12. Segurança vigente

Continuam fora dos testes:

- RUN remoto;
- STOP remoto;
- escrita de registradores/bobinas;
- download de programa;
- apagamento de memória;
- comandos de senha não compreendidos.

Política do Laboratório PG:

- `HANDSHAKE`: somente `CON-ICB<CR>` protegido;
- `PASSIVE`: sem TX;
- `CANDIDATE`: somente documentação, nunca TX;
- `BLOCKED`: nunca TX;
- `READ_ONLY_VERIFIED`: exige autorização manual e presença explícita na `readOnlyAllowlist`.

## 13. Estado atual do software de teste

- OpenLadder Studio: **v0.45**.
- Motor PG Lab: **1.2**.
- Pacote vigente: **`2026.09.04.8`**.
- Sequência de bancada preparada atualmente: **HELLO → `F0 00 0F` → captura passiva**.
- `38 00 C7` e `34 03 00 00 A0 28` permanecem desabilitados até a caracterização controlada da resposta ao F0.

## 14. Próximo marco experimental

Reproduzir de forma controlada a resposta ao `F0 00 0F` em STOP e registrar:

- quantidade exata de bytes;
- quadro hexadecimal completo;
- checksum;
- latência;
- relação do bit `0x40` com STOP/RUN;
- eventual código/valor nos demais bytes.

Somente depois disso deve ser decidido se a sequência pode avançar para `38 00 C7` dentro da mesma sessão.
