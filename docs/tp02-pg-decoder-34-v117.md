# TP02 PG Lab 1.17 — decodificação local do quadro 34

## Objetivo

Transformar a evidência da captura física `TP02-PG-Lab-20260910-135502.txt` em uma decodificação automática, conservadora e READ-ONLY no OpenLadder Studio.

A alteração não acrescenta nenhum TX serial, probe, escrita, download, apagamento, firmware ou RUN/STOP remoto. O fluxo físico permanece:

```text
HELLO -> F0 -> 38 -> 34
```

## Implementação

Foi criado:

```text
src/OpenLadderStudio.Core/Tp02Pg34Decoder.cs
```

O decoder valida:

- tamanho do quadro 34;
- checksum com soma total `0xFF`;
- `LEN=0xF0` para a geometria atualmente conhecida;
- Região A com 80 pares `HIGH/LOW`;
- Região B com 80 bytes `BRAW`.

Para cada passo:

```text
HIGH = payload[2*i]
LOW  = payload[2*i+1]
BRAW = payload[0xA0+i]
```

## Booleano atualmente decodificado

A máscara física confirmada é:

```text
opcode = LOW & 0x78
```

Mapa:

```text
10 STR
18 STR NOT
20 AND
28 AND NOT
30 OR
38 OR NOT
40 OUT
```

No escopo X/Y/C observado, o endereço é reconstruído por:

```text
group = HIGH & 0x1F
bit   = LOW & 0x07
n     = group*8 + bit + 1

HIGH base 00 -> X
HIGH base 20 -> Y
HIGH base 40 -> C
```

## Verificação BRAW

Somente para passos booleanos reconhecidos, o decoder calcula:

```text
expectedBraw =
    (HIGH >> 4)
  + (HIGH & 0x0F)
  + (LOW  >> 4)
  + (LOW  & 0x0F)
```

A captura de 26 passos de 2026-09-10 fechou essa relação em todos os passos booleanos observados.

O decoder usa essa relação como verificação de integridade no escopo booleano. Ela **não é aplicada como regra geral a TMR, CNT ou F-xx** sem captura física correspondente.

## Uso cauteloso do comando 38

A relação atualmente observada é:

```text
2 instruções booleanas -> 38 payload[1] = 02
3 instruções booleanas -> 38 payload[1] = 04
26 instruções booleanas -> 38 payload[1] = 32
```

Isto coincide com:

```text
2*(N-1)
```

ou com o deslocamento do primeiro byte do último par ativo da Região A para instruções booleanas de um passo.

No código, o valor do 38 é tratado apenas como **hint estrutural**. Se ele divergir da cauda não nula do payload 34, o decoder não trunca o programa e usa a cauda do próprio payload. Portanto a hipótese do 38 não foi promovida prematuramente a regra universal.

## PG Lab 1.17

Foi adicionado:

```text
src/OpenLadderStudio.Desktop/PreparePgLabDecode34V27.ps1
```

O `BuildTp02Lab.bat` aplica esse patch depois do V26. Após um quadro 34 válido, o Lab registra:

```text
DECOD34  resumo da decodificação
DECOD34  coerência do hint do 38
IL34     um registro por passo decodificado
```

Exemplo esperado para a captura completa:

```text
IL34 000 H=00 L=10 B=01 -> STR X0001 BRAW=OK
...
IL34 024 H=02 L=11 B=04 -> STR X0018 BRAW=OK
IL34 025 H=21 L=42 B=09 -> OUT Y0011 BRAW=OK
```

## Autoteste físico

Foi criado:

```text
tests/OpenLadderStudio.Core.Tests/Tp02Pg34DecoderSelfTest.cs
```

O fixture é montado a partir dos 26 pares HIGH/LOW e dos 26 BRAW efetivamente capturados. O restante do payload é zero. O autoteste verifica, entre outros pontos:

- quadro de 243 bytes;
- `LEN=F0`;
- checksum físico final `0x98`;
- 26 passos;
- reconstrução integral das 26 instruções;
- zero divergências BRAW;
- `38=...32...` coerente com a cauda ativa;
- detecção de BRAW adulterado;
- rejeição de checksum quebrado.

## CI

O workflow `validate-tp02-compiler` passou a compilar e executar o decoder e seu autoteste e também compilar o PG Lab. Após a correção do fixture inicial, o run #11 (`34515659960`) concluiu com sucesso, incluindo:

```text
encoder TP02: OK
Ladder -> TP02: OK
decoder PG34: OK
build PG Lab 1.17: OK
```

## Limites atuais

Ainda não chamar de resolvido:

- formato físico completo de TMR;
- formato físico completo de CNT;
- operandos/presets no quadro 34;
- BRAW de TMR/CNT/F-xx;
- semântica universal do `38 payload[1]`;
- funções F-xx de múltiplos passos no quadro 34.

Esses itens exigem a próxima captura física com instruções de tamanho variável.
