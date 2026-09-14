# TP02 PG/PC12 — validação física de 2026-09-14

## Equipamento e perfil

- PLC: WEG TP02-60MR / família TP02-40/60MR(T) V2.2.4K.
- Porta usada na sessão: COM1.
- Perfil físico: 19200 bit/s, 8 bits, paridade ímpar, 1 stop bit, DTR=OFF, RTS=OFF.
- A sessão foi estritamente READ-ONLY: somente `CON-ICB\r`, `F0`, `38` e `34` foram transmitidos.

## Resultado físico confirmado

O leitor PG/PC12 conseguiu completar a sequência abaixo no PLC real:

```text
HELLO -> F0 -> 38 -> PG34@0000 -> parser Ladder
```

### HELLO

Quadro confirmado:

```text
RX: 80 01 09 75
```

Interpretação observada: PLC em STOP.

A sincronização só ocorreu após warm-up, fechamento/reabertura da COM e nova sessão, confirmando que o TP-232PG/porta serial pode exigir estabilização adicional.

### F0

```text
TX: F0 00 0F
RX: 00 02 10 22 CB
```

O F0 foi transmitido aproximadamente 450 ms após o HELLO válido. Esta temporização é significativamente mais confiável que a campanha anterior que transmitia F0 quase imediatamente após o HELLO.

### 38

```text
TX: 38 00 C7
RX: 00 02 00 04 F9
```

O programa lido possuía 3 machine words até `F-00 END`; nesta amostra o campo `04` novamente coincide com `2 * (N - 1)` para `N=3`. Esta relação permanece tratada como observação física, não como regra universal.

### PG34 página 0

```text
TX: 34 03 00 00 A0 28
RX: status=00, LEN=F0, 240 bytes de dados, checksum total FF
```

Programa reconstruído:

```text
0000  001809  STR NOT X0001
0001  204006  OUT Y0001
0002  007007  F-00 END
```

Resultado do parser:

- páginas PG34: 1;
- passos: 3;
- END global: 0002;
- BRAW divergentes: 0;
- UNKNOWN: 0.

Os três BRAW também coincidem com a fórmula reconstruída:

```text
00 18 -> 0+0+1+8 = 09
20 40 -> 2+0+4+0 = 06
00 70 -> 0+0+7+0 = 07
```

## Temporização física que funcionou

A sessão bem-sucedida observou, aproximadamente:

- HELLO válido -> F0 TX: 450 ms;
- F0 RX -> 38 TX: 420 ms;
- 38 validado -> PG34 TX: 395–450 ms.

O leitor robusto mantém warm-up, nova sessão limpa, retry de HELLO/F0 e estas pausas antes de avançar.

## Próxima prova física: paginação PG34 longa

A paginação reconstruída do PC12 usa `START_H, START_L` e páginas de 80 words:

```text
start 0   / 0x0000 -> 34 03 00 00 A0 28
start 80  / 0x0050 -> 34 03 00 50 A0 D8
start 160 / 0x00A0 -> 34 03 00 A0 A0 88
start 240 / 0x00F0 -> 34 03 00 F0 A0 38
start 320 / 0x0140 -> 34 03 01 40 A0 E7
```

O objetivo agora é provar fisicamente duas fronteiras:

1. `0x0000 -> 0x0050`, confirmando a segunda página;
2. `0x00F0 -> 0x0140`, confirmando a troca do byte alto de START.

Para isso foram adicionados:

- `src/OpenLadderStudio.Desktop/GenerateTp02Pg34BoundaryProject.ps1` — gera offline um projeto Ladder de 323 words, sem saída física Y;
- `src/OpenLadderStudio.Desktop/AnalyzeTp02Pg34BoundarySession.ps1` — audita offline uma pasta de leitura física e classifica as fronteiras como PASS/PENDENTE/FAIL.

O projeto gerado contém 161 rungs `STR X0001 -> OUT C0001`, seguidos de `F-00 END`:

- 323 machine words;
- END no passo 322 (`0x0142`);
- nenhuma saída física `Y` referenciada;
- páginas esperadas: 0, 80, 160, 240 e 320.

A gravação desse projeto no PLC não é automática. Deve ser feita somente com backup prévio do programa existente, PLC em condição segura/STOP e restauração do programa original ao final.
