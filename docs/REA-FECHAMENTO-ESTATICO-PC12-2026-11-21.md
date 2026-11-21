# Fechamento estático do PC12 com REA

Data: 2026-11-21  
Alvo: `src/OpenLadderStudio.Desktop/pc12.exe`  
SHA-256: `05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0`  
Formato: PE x86 nativo

## Conclusão

A engenharia reversa **estática do lado PC12** está fechada para as operações PG
acionáveis pelo programa e necessárias à reconstrução do cliente. Foram
recuperados os quadros transmitidos, guardas de estado, tentativas, validação de
timeout/checksum/NAK, formatos de leitura e escrita e interpretação das respostas.

Esse fechamento não afirma que o firmware do TP02 executa corretamente cada
comando. O efeito interno no PLC, diferenças entre versões de firmware e os
códigos efetivamente devolvidos pelo equipamento não estão contidos no
`pc12.exe` e continuam sendo propriedades de bancada ou de uma futura imagem do
firmware.

O bundle completo desta rodada está em
`docs/data/rea-pc12-static-closure-2026-11-21.json`. Ele contém 20 registros de
Evidence e nenhum desconhecido interno reportado pelo REA. “Nenhum desconhecido”
refere-se às consultas realizadas contra o executável, não ao firmware externo.

## Controles diretos

| Operação | Quadro montado pelo PC12 | Guarda observada | Evidence |
|---|---|---|---|
| RUN | `02 00 FD` | COM ligada; não pode estar em monitor; executa somente se o estado em cache não estiver RUN | `ev_c387afe798bac19645027b619dd28cf57a1161ce44e24d0b64f4f0c875553dad`, `ev_c48a8ef409a5035698dbc3d89e471af8833b375fde9f74c261215970cd2a671d` |
| STOP/Program | `01 00 FE` | COM ligada; encerra estados de monitor antes da transação | `ev_f864ba6fe433a1ba2bd5d28fdff51773a0581142872b56d1ef374822b89736c5`, `ev_81e163e50390184098d02fd963e0d13a44418c1f1b1a82a80c0d91b96e4c810a` |
| Clear System | `04 00 FB` | PLC precisa estar parado | `ev_7f88005fc822411d38bb9e4311d5ebdfad8c45f4f28c2d102ec7666e03aa7ffe`, `ev_9ffcf9c0b756897c5c1c1c183b978ebf8523d9a0b7284dd0a028a0eaac338e3e` |
| Clear Data | `11 00 EE` | PLC precisa estar parado | `ev_c517f97a51d18433ea4224d02d535bb7de08004a0c8b66ddaafb4922f175b486`, `ev_3baa33e035e07aef095915ef05eb218f4426ffada9efb94acf42f8a1553596d3` |
| Clear Program | `03 00 FC` | PLC parado e confirmação do operador | `ev_afd67b66b4a41d210c31fb50f26490f5f0be10d7b94c2e143876219adf9519a9`, `ev_6f72459f3af0690fbbb83a70dcde46d91d966bba125df4640b7aa0096aa2d6b2` |
| Clear All Memory | `0F 00 F0` | PLC parado e confirmação do operador | `ev_89e687cf1bbb5a2245e29a123ab1e12fe5f8f73e785434f55f81ba21b352f295`, `ev_4bb67111f594054ad0a89de8b8b1ab494d8fcdf9b5936ed7c89bffed8e93d858` |

RUN e STOP usam recepção de pelo menos três bytes e repetem a transação quando
há timeout ou checksum inválido. RUN trata especialmente o código de erro `15`
com consultas F0 adicionais. O significado do código `15` dentro do firmware
não é estabelecido pelo cliente.

## EEPROM PACK e senha

O handler de EEPROM exige COM ligada, PLC parado e ausência de monitor. Quando
o bit de senha está ativo, compara a senha digitada com a representação local e
envia o preflight `14 00 EB`. A direção escolhida no diálogo define:

```text
12 00 ED  EEPROM PACK -> PLC
13 00 EC  PLC -> EEPROM PACK
```

Cada transação tem até três tentativas e valida timeout, checksum e bit de erro.
Evidence: `ev_f7bbad374b5cd881029c741c0a02a9f62b52923893212cf5c6046ff75548deda`.

## RTC e scan time

O PC12 lê o RTC completo com:

```text
0A 03 53 F9 0E 98
```

Ele interpreta sete palavras de 16 bits e, após edição, grava as mesmas sete
palavras com:

```text
09 11 53 F9 0E <14 bytes> <checksum>
```

Evidence: `ev_4507d0c370cdfdccb2f062faa3cca7f8d50a1b7df644ec6f3022576adc6b1b2c`.

O monitor reduzido de horário usa `0A 03 53 F9 06 A0` e mostra três palavras
como hora, minuto e segundo. Evidence:
`ev_1575ae266ff18b9fec2176e2529a7dffe035a88bedc9b08365850fc24cbcf7c8`.

O scan time usa `0A 03 60 00 06 8C`, lê três palavras e as apresenta como tempo
atual, máximo e mínimo. Evidence:
`ev_f870d0d3e399ef17402e1147b3d36649982c5ab69ef5131bbc8d89d84003bf36`.

## Memória, registradores e monitor

O handler de leitura em lote confirma o opcode `0A`, paginação e tamanhos:

- V: 16 páginas de 64 palavras, base `0x5000`;
- D: 32 páginas de 64 palavras, base `0x9000`;
- WC: 16 páginas de 57 palavras, base `0x7000`;
- FL: 13 páginas, base `0x8000`, com tamanho dependente do modo.

O PC12 exige respostas com comprimento declarado, soma `0xFF` e bit de erro
limpo antes de persistir os dados nos arquivos locais. Evidence:
`ev_3ecf9cfe273f4697c8fc42e88d0e444ce59e53c5298a530bff5ba108d0fb8229`.

As entradas “Modify Register” e “Set/Reset I/O Coil” só são abertas quando o
cliente está em modo Monitor. Os construtores internos já cobertos pelos
emuladores do repositório produzem `09 05 ...` para uma palavra e `35 03 ...`
para bobinas. Os handlers externos e suas guardas foram novamente confirmados:
`ev_e8b18dc56c38765b5e648e89a74042b81104ec9c3b92fc4e360ef15947fa43d2`
e `ev_ce25bd6ff044a5f1617ced36ea4a4eb117f5b68e182e79c2b46ad538988af812`.

## Programa Ladder

Permanecem válidos os fechamentos anteriores, ligados ao mesmo SHA-256:

- F0 identifica modelo, firmware, RUN/STOP e presença de senha;
- `38` fornece o limite usado pelo laço de leitura;
- `34` lê páginas e avança pelo número de palavras inteiras decodificadas;
- `03 00 FC` precede a escrita de programa do PC12;
- `33` transporta os blocos de programa;
- ACK aceito: pelo menos três bytes, soma `0xFF` e bit 7 limpo;
- NAK: bit 7 ativo e código em `rx[2]`;
- não existe rollback automático depois de escrita parcial;
- `37` pertence exclusivamente à atualização de BIOS e transmite sem esperar
  resposta no quadro final `37 02 FF FF C8`.

As Evidence anteriores continuam indexadas em
`docs/pc12-v2.1-static-inventory.md`.

## Fronteira final

### Resolvido pelo REA no artefato distribuído

- todos os opcodes conhecidos emitidos pelo PC12;
- bytes e checksums construídos pelo cliente;
- pré-condições de UI/estado;
- política de retry e timeout;
- critérios de ACK/NAK;
- interpretação de respostas pelo cliente;
- paginação e formatos de dados;
- fluxos de senha, EEPROM, RTC, monitor, leitura e escrita de programa.

### Não demonstrável a partir do `pc12.exe`

- implementação interna desses opcodes no firmware;
- efeitos físicos sobre saídas e memória não volátil;
- diferenças entre revisões de firmware;
- quais NAKs cada revisão realmente produz;
- atomicidade e recuperação após perda de energia;
- segurança operacional em uma máquina conectada.

Para resolver essa segunda lista é necessária uma imagem do firmware compatível
com análise estática ou uma campanha de bancada. Ela não representa uma lacuna
remanescente da engenharia reversa do executável PC12.
