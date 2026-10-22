# Validação física — dois contatos abertos em paralelo (2026-09-10 12:11 BRT)

## Objetivo

Registrar de forma permanente a captura `TP02-PG-Lab-20260910-121113.txt`, obtida com **OpenLadder Studio v1.04 / TP02 PG Lab 1.16**, PLC em STOP e fluxo estritamente READ-ONLY.

O programa ensaiado, mantendo exatamente os mesmos operandos da captura série anterior e alterando apenas a topologia, foi:

```text
      +-- X0009 aberto --+
------|                  |------(Y0003)
      +-- X0002 aberto --+
```

Baseline imediatamente anterior para comparação:

```text
X0009 aberto -- X0002 aberto -- (Y0003)
```

## Sessão válida

A sessão 7 completou o fluxo conhecido:

```text
HELLO -> 80 01 09 75
F0    -> 00 02 10 22 CB
38    -> 00 02 00 04 F9
34    -> quadro de 243 bytes, LEN=F0, payload=240 bytes, checksum válido
```

O checksum final do quadro 34 foi `0x5D`.

## Payload 34 observado

Os únicos bytes não nulos do payload foram:

```text
payload[0x000] = 01
payload[0x001] = 10
payload[0x003] = 31
payload[0x004] = 20
payload[0x005] = 42
payload[0x0A0] = 02
payload[0x0A1] = 04
payload[0x0A2] = 08
```

Pela geometria já recuperada:

```text
passo i:
  HIGH = payload[2*i]
  LOW  = payload[2*i+1]
  BRAW = payload[0x0A0+i]
```

resulta:

```text
passo 0: HIGH=01 LOW=10 BRAW=02
passo 1: HIGH=00 LOW=31 BRAW=04
passo 2: HIGH=20 LOW=42 BRAW=08
```

## Comparação byte a byte com o programa em série

Captura série anterior:

```text
passo 0: HIGH=01 LOW=10 BRAW=02   -> STR X0009
passo 1: HIGH=00 LOW=21 BRAW=03   -> AND X0002
passo 2: HIGH=20 LOW=42 BRAW=08   -> OUT Y0003
checksum = 0x6E
```

Captura paralela atual:

```text
passo 0: HIGH=01 LOW=10 BRAW=02   -> STR X0009
passo 1: HIGH=00 LOW=31 BRAW=04   -> OR X0002
passo 2: HIGH=20 LOW=42 BRAW=08   -> OUT Y0003
checksum = 0x5D
```

Somente dois bytes do payload mudaram:

```text
payload[0x003]: 0x21 -> 0x31
payload[0x0A1]: 0x03 -> 0x04
```

Todos os demais 238 bytes do payload permaneceram idênticos.

A soma dos dois incrementos é `0x10 + 0x01 = 0x11`; de forma coerente, o checksum caiu de `0x6E` para `0x5D`, isto é, exatamente `0x11`, mantendo a soma total do quadro em `0xFF`.

## OR confirmado fisicamente

No segundo passo da captura paralela:

```text
LOW = 0x31
0x31 & 0x78 = 0x30
```

A análise estática do `pc12.exe` já associava `0x30` ao opcode **OR**. Como a única alteração experimental foi série -> paralelo, mantendo X0009, X0002, Y0003 e os tipos dos contatos, esta captura confirma fisicamente no TP02:

```text
STR X0009
OR  X0002
OUT Y0003
```

A captura série havia confirmado:

```text
STR X0009
AND X0002
OUT Y0003
```

Portanto, a diferença topológica série/paralelo está isolada no opcode do segundo contato:

```text
AND X0002: LOW=0x21
OR  X0002: LOW=0x31
```

A diferença é exatamente `+0x10`, enquanto os três bits inferiores permanecem `001`, preservando o índice de X0002.

## Região B bruta

No mesmo passo, BRAW mudou:

```text
série:    BRAW=0x03
paralelo: BRAW=0x04
```

Como o endereço X0002 permaneceu o mesmo, essa diferença mostra novamente que BRAW não pode ser tratado como um campo puramente independente de endereço. Há informação ligada à representação/instrução/topologia ou alguma transformação intermediária ainda não resolvida.

Não se atribui, neste momento, significado exato à mudança `0x03 -> 0x04`.

## Comando 38

O comando 38 retornou em ambos os casos, série e paralelo:

```text
00 02 00 04 F9
```

Logo, `PAYLOAD[1]=0x04` do 38 **não distingue AND de OR neste par controlado**. Isso enfraquece interpretações em que esse byte seria um hash ou código diretamente sensível ao opcode de cada instrução. Ele pode estar relacionado a alguma grandeza estrutural mais grossa, como quantidade/tamanho/estado do programa, mas a semântica exata continua desconhecida.

## O que esta captura confirma

- o quadro 34 é válido para o programa paralelo, com payload de 240 bytes e checksum FF;
- `STR X0009` permanece `HIGH=01 LOW=10 BRAW=02`;
- `OR X0002` aparece como `HIGH=00 LOW=31 BRAW=04`;
- `OUT Y0003` permanece `HIGH=20 LOW=42 BRAW=08`;
- o opcode OR (`LOW & 0x78 = 0x30`) está agora confirmado fisicamente em bancada;
- série -> paralelo alterou apenas `payload[0x003]` e `payload[0x0A1]`;
- `38 PAYLOAD[1]=0x04` permaneceu igual entre série e paralelo;
- a semântica completa de BRAW continua aberta.

## Próximo experimento recomendado

Manter a topologia paralela e os mesmos operandos, alterando **somente o segundo contato X0002 de aberto para fechado**:

```text
      +-- X0009 aberto  --+
------|                   |------(Y0003)
      +-- X0002 fechado --+
```

Objetivo: isolar fisicamente **OR NOT**. A análise estática prevê, para X0002, um LOW compatível com `0x39` (`0x38 | 0x01`), mas essa previsão só deve ser promovida a fato após a captura.

Depois, fazer o teste equivalente em série, mudando apenas X0002 para fechado, para validar **AND NOT**.

## Segurança

Nenhuma escrita foi habilitada. O teste permaneceu no fluxo:

```text
HELLO -> F0 -> 38 -> 34
```

com PLC em STOP. `0F 00 F0` continua bloqueado e nenhum comando de escrita, download, apagamento, firmware ou RUN/STOP remoto foi transmitido.
