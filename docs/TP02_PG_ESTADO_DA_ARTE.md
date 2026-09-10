# WEG TP02-60MR — estado da arte da engenharia reversa do protocolo PG

> **Documento canônico de recuperação da pesquisa.**
>
> Snapshot consolidado: **2026-09-10 11:18 BRT**. Base de software: **OpenLadder Studio v1.04**, `main` no commit `0568ef4143cb6c5090546a8bd18f1364fa43f018`; motor de bancada **TP02 PG Lab 1.16**.
>
> Última evidência física: captura `TP02-PG-Lab-20260910-111814.txt`, correspondente ao teste controlado **X0009 aberto -> Y0003**. Ver [`tp02-pg-validacao-x0009-20260910.md`](tp02-pg-validacao-x0009-20260910.md).
>
> Este arquivo separa fatos de bancada, resultados de análise estática/emulação, hipóteses, interpretações refutadas e pontos ainda desconhecidos. Ele deve ser a primeira referência para qualquer retomada futura.

## 1. Objetivo

Compreender suficientemente o protocolo serial PG do PLC **WEG TP02-60MR** para permitir leitura segura do programa, reconstrução do formato interno e, posteriormente, decodificação confiável do ladder.

Hardware de bancada identificado como:

```text
TP02-40/60MR(T) V2.2.4K
```

Software histórico de referência: **PC12 Design Center v2.1**.

A política experimental permanece estritamente **READ-ONLY**. Escrita/download, apagamento, firmware e RUN/STOP remoto não são habilitados no OpenLadder Studio nesta fase.

## 2. Níveis de confiança

- **CONFIRMADO EM BANCADA** — observado fisicamente no PLC e validado por estrutura/checksum.
- **RESOLVIDO POR ANÁLISE ESTÁTICA/EMULAÇÃO** — recuperado diretamente do `pc12.exe`, com verificações automatizadas no repositório.
- **EVIDÊNCIA FORTE** — múltiplas observações coerentes, ainda sem prova completa de generalidade.
- **HIPÓTESE** — interpretação útil para orientar experimento, ainda não demonstrada.
- **REFUTADO** — interpretação que deixou de ser compatível com evidência posterior.
- **DESCONHECIDO** — sem evidência suficiente para atribuir significado.

Nenhuma hipótese deve ser promovida automaticamente a regra de protocolo ou usada para escrita em hardware.

## 3. Camada serial funcional

### 3.1 Perfil conhecido

**CONFIRMADO EM BANCADA:**

```text
Baud:       19200
Data bits:  8
Parity:     Odd
Stop bits:  1
DTR:        ON
RTS:        OFF
```

Essa configuração já produziu HELLO, F0, 38 e 34 válidos.

### 3.2 Intermitência observada

O enlace PG é intermitente. Várias tentativas podem ficar silenciosas e uma tentativa posterior, sem qualquer alteração física, pode responder corretamente.

Estratégia atual do PG Lab 1.16:

1. abrir a COM em 19200 8O1, DTR on, RTS off;
2. tentar HELLO até 6 vezes na mesma abertura;
3. se não houver HELLO-STOP válido, fechar a COM;
4. aguardar 1500 ms;
5. iniciar nova sessão limpa;
6. após HELLO-STOP, enviar F0 uma única vez naquela abertura da COM;
7. usar no máximo 12 sessões limpas por execução.

Na captura X0009, a sessão 2 chegou a HELLO-STOP mas o F0 ficou silencioso; a sessão 8 finalmente completou F0, 38 e 34. Isso reforça que silêncio do F0 não implica falha física do enlace.

## 4. Checksum e enquadramento

**CONFIRMADO EM BANCADA:**

```text
sum(todos os bytes do quadro) mod 256 = 0xFF
```

Para construir o último byte:

```text
checksum = (0xFF - (sum(data) & 0xFF)) & 0xFF
```

Família de respostas conhecida:

```text
[FLAGS/STATE] [LEN] [PAYLOAD de LEN bytes] [CHECKSUM]
```

Tamanho total esperado: `LEN + 3` bytes.

## 5. HELLO / estabelecimento de sessão

### Requisição

```text
ASCII: CON-ICB\r
HEX:   43 4F 4E 2D 49 43 42 0D
```

### STOP

```text
RX: 80 01 09 75
```

### RUN

```text
RX: C0 01 09 35
```

**EVIDÊNCIA FORTE:** o bit `0x40` do primeiro byte acompanha RUN. O mesmo padrão foi observado no comando 14.

O significado do bit/base `0x80` do HELLO permanece **DESCONHECIDO**.

## 6. Comando F0

```text
TX: F0 00 0F
RX conhecido: 00 02 10 22 CB
```

**CONFIRMADO EM BANCADA:**

- pode responder corretamente em STOP;
- pode ficar silencioso mesmo após HELLO-STOP válido;
- STOP é necessário no fluxo conhecido de leitura de programa, mas não garante resposta do F0.

**DESCONHECIDO:** semântica exata.

**Regra:** F0 não é comando STOP. Não existe evidência para chamá-lo assim.

## 7. Comando 38

```text
TX: 38 00 C7
```

Respostas físicas conhecidas:

```text
00 02 00 0A F3
00 02 00 02 FB
00 02 00 04 F9
```

Estrutura comum:

```text
FLAGS      = 00
LEN        = 02
PAYLOAD[0] = 00
PAYLOAD[1] = variável
checksum   = fecha em FF
```

**EVIDÊNCIA FORTE:** `PAYLOAD[1]` varia com o programa/estado relacionado ao programa e não pode ser tratado como constante.

**DESCONHECIDO:** significado exato do byte variável.

O PG Lab 1.16 valida estruturalmente o retorno, sem enumerar valores específicos.

No teste X0009 aberto -> Y0003, o retorno foi novamente:

```text
00 02 00 02 FB
```

## 8. Comando 34 — leitura de programa

### 8.1 Quadro base

```text
TX: 34 03 00 00 A0 28
```

Resposta dos programas mínimos:

```text
00 F0 [240 bytes de payload] [checksum]
```

Total: 243 bytes.

### 8.2 Paginação — resolvida

**RESOLVIDO POR ANÁLISE ESTÁTICA/EMULAÇÃO:**

```text
34 03 [passo_hi] [passo_lo] A0 checksum
```

Os dois bytes de endereço representam o **contador de passos do programa**, não um offset linear em bytes.

A quantidade é fixa em `0xA0` no fluxo recuperado. O contador avança de **1 a 4** conforme o tamanho da instrução decodificada. A próxima requisição depende do conteúdo já lido.

A antiga hipótese de incrementos fixos:

```text
00F0 -> 01E0 -> 02D0
```

está **REFUTADA/DESCARTADA**.

Para `TP02-40/60MR(T)`, o PC12 usa limite de **4000 passos (`0x0FA0`)**.

### 8.3 Geometria do bloco — resolvida

O payload de 240 bytes representa **80 passos de 3 bytes**, organizados em dois planos:

```text
Região A: payload[0x000 .. 0x09F] = 160 bytes = 2 bytes por passo
Região B: payload[0x0A0 .. 0x0EF] =  80 bytes = 1 byte bruto por passo

Passo i:
  HIGH = payload[2*i]
  LOW  = payload[2*i + 1]
  BRAW = payload[0x0A0 + i]
```

Os pares observados em `0x001/0x0A0` e `0x003/0x0A1` não são duplicatas; pertencem ao mesmo passo distribuído nos dois planos.

**Importante após X0009:** a geometria física acima continua confirmada. O que precisou ser corrigido foi a interpretação direta de `BRAW` como o byte interno usado sem transformação pelo decodificador do PC12.

## 9. Decodificação de instruções

### 9.1 Booleanas

**RESOLVIDO POR ANÁLISE ESTÁTICA:** o PC12 mascara o byte LOW com `0x78` e reconhece:

| LOW & 0x78 | Instrução |
|---:|---|
| `0x10` | STR |
| `0x18` | STR NOT |
| `0x20` | AND |
| `0x28` | AND NOT |
| `0x30` | OR |
| `0x38` | OR NOT |
| `0x40` | OUT |
| `0x60` | TMR |
| `0x68` | CNT |

Os sete primeiros coincidem com o encoder implementado em `Tp02TargetCompiler.cs`; TMR e CNT foram recuperados posteriormente do decodificador.

Há uma família `0x08`–`0x0D` em outro ramo do PC12. Ela não apareceu em bancada e permanece sem interpretação completa.

### 9.2 Palavras fixas

```text
0x00 = vazio/NOP
0x01 = AND STR
0x02 = OR STR
```

## 10. Funções F-xx e fim de programa

**RESOLVIDO POR ANÁLISE ESTÁTICA:** quando o passo segue o caminho de funções, o PC12 usa o byte HIGH como índice em uma tabela de 72 entradas com 64 handlers válidos.

O cruzamento com `docs/data/tp02_function_map_normalized.csv` é exato para os 64 números de função mapeados.

O índice `0x00` corresponde a **F-00 / End**.

Portanto, o programa termina por instrução **End** no fluxo, não apenas por atingir um comprimento declarado.

## 11. Endereçamento — estado corrigido após X0009

### 11.1 O que o encoder produz

`Tp02TargetCompiler.EncodeBitInstruction` usa, para instruções bit:

```text
k     = number - 1
bit   = k & 0x07
group = k >> 3

HIGH = deviceBase | (group & 0x1F)
LOW  = opcode | bit
EXT  = ((group >> 1) & 0xF0)
```

Bases atuais:

```text
X = 0x00
Y = 0x20
C = 0x40
```

Essa fórmula previa para `STR X0009`:

```text
HIGH = 0x01
LOW  = 0x10
```

### 11.2 Validação física de X0009

Programa:

```text
X0009 aberto -- (Y0003)
```

Quadro `34` válido, checksum final `0x92`.

Bytes não nulos:

```text
payload[0x000] = 01
payload[0x001] = 10
payload[0x002] = 20
payload[0x003] = 42
payload[0x0A0] = 02
payload[0x0A1] = 08
```

Passos:

```text
passo 0: HIGH=01 LOW=10 BRAW=02   -> contato X0009 aberto
passo 1: HIGH=20 LOW=42 BRAW=08   -> bobina Y0003
```

Comparação com `X0001 aberto -> Y0003`:

```text
X0001: HIGH=00 LOW=10 BRAW=01
X0009: HIGH=01 LOW=10 BRAW=02
```

Somente `payload[0x000]` e `payload[0x0A0]` mudaram entre esses dois programas; a bobina permaneceu idêntica.

**CONFIRMADO EM BANCADA:** a fronteira de grupo ocorre em X0009. O índice de bit em LOW reinicia e o grupo passa a aparecer em HIGH, exatamente como previsto pelo encoder no plano A.

### 11.3 Fórmula linear antiga — refutada

A aproximação empírica:

```text
LOW ~= 0x0F + n
```

funcionava para X0001/X0002 e coincidia até X0008, mas previa `0x18` para X0009. O hardware retornou `0x10`.

Portanto, ela não é uma fórmula geral.

### 11.4 Correção importante sobre Região B

A análise estática encontrou uma rotina em `0x4B036C`–`0x4B03C6` que reconstrói um número usando:

```text
bits 0-2 <- LOW & 0x07
bit  3   <- LOW & 0x80
bits 4-6 <- byte do segundo cursor & 0x10/0x20/0x40
resultado + 1
```

Isso continua sendo um fato sobre o `pc12.exe`.

O que foi **REFUTADO pela captura X0009** é a suposição de que o byte testado nessa rotina seja, sem nenhuma transformação, exatamente o byte bruto `payload[0x0A0+i]` recebido do PLC.

Para X0009, `BRAW=0x02`; portanto `BRAW & 0x10 == 0`, embora o grupo de endereço já tenha mudado.

A interpretação correta, por enquanto, é:

- a geometria bruta do 34 está confirmada;
- HIGH/LOW do plano A para X0009 estão confirmados;
- a rotina interna do PC12 que lê LOW + um segundo byte está confirmada por análise estática;
- **a transformação/mapeamento entre BRAW e o byte efetivamente usado pela rotina interna ainda precisa ser rastreada**;
- a semântica completa de BRAW continua **DESCONHECIDA**.

## 12. Experimentos de bancada consolidados

| Teste | Ladder mínimo | p000 | p001 | p002 | p003 | p0A0 | p0A1 |
|---|---|---:|---:|---:|---:|---:|---:|
| A | X0001 fechado -> Y0002 | `00` | `18` | `20` | `41` | `09` | `07` |
| B | X0002 fechado -> Y0002 | `00` | `19` | `20` | `41` | `0A` | `07` |
| C | X0002 fechado -> Y0003 | `00` | `19` | `20` | `42` | `0A` | `08` |
| D | X0002 aberto -> Y0003 | `00` | `11` | `20` | `42` | `02` | `08` |
| E | X0001 aberto -> Y0003 | `00` | `10` | `20` | `42` | `01` | `08` |
| F | X0009 aberto -> Y0003 | `01` | `10` | `20` | `42` | `02` | `08` |

A captura F é a primeira evidência física fora do primeiro grupo de oito entradas.

O valor `payload[0x002]=0x20` permanece coerente com o HIGH do passo da bobina Y nos seis programas mínimos.

## 13. Caso de dois contatos em série

Programa já ensaiado parcialmente:

```text
X0001 aberto -- X0002 aberto -- (Y0003)
```

No PG Lab 1.15, HELLO-STOP e F0 foram válidos e o 38 retornou repetidamente:

```text
00 02 00 04 F9
```

A versão 1.15 bloqueou corretamente o 34 porque ainda enumerava apenas dois vetores do 38. Isso motivou o PG Lab 1.16.

**Próxima prioridade de bancada:** repetir esse mesmo ladder usando PG Lab 1.16 para obter o `34`.

## 14. Comando 0A — leitura geral

Formato observado:

```text
0A 03 AH AL LEN CS
```

`AH AL` comporta-se como endereço big-endian nos probes usados.

O 0A responde em RUN e STOP; nas respostas observadas, RUN adiciona `0x40` no primeiro byte.

**IMPORTANTE:** não assumir que 0A lê o ladder. O escopo exato dessa memória continua diferente/mais amplo que o fluxo 34.

## 15. Comando 14

```text
TX: 14 00 EB
STOP RX: 00 00 FF
RUN  RX: 40 00 BF
```

Semântica exata ainda desconhecida. Serve como evidência independente do bit `0x40` associado a RUN.

## 16. Mapa estático de escrita

A análise do `pc12.exe` encontrou a primitiva de escrita `0x09` como espelho do `0x0A`:

```text
leitura: 0A [n=3] [end_hi] [end_lo] [qtd]              chk
escrita: 09 [n=5] [end_hi] [end_lo] [qtd] [d0] [d1]    chk
```

Também existem quadros maiores como `09 11 ...` com 14 bytes de dados, usados em rotinas de registradores.

**NÃO RESOLVIDO:** o laço/orquestração completo de `Write PLC Program...` e a confirmação de que o download inteiro usa 09.

Esse conhecimento é apenas documental. Nenhuma escrita é transmitida pelo OpenLadder nesta fase.

## 17. Comando destrutivo bloqueado

```text
0F 00 F0
```

**RESOLVIDO POR ANÁLISE ESTÁTICA:** corresponde a **Clear All Memory**.

Permanece explicitamente bloqueado.

## 18. Fluxo READ-ONLY do PG Lab 1.16

```text
ABRIR COM: 19200 8O1, DTR ON, RTS OFF
  -> HELLO CON-ICB\r, até 6 tentativas
  -> exigir 80 01 09 75 (STOP)
  -> F0 00 0F, uma vez por abertura
  -> exigir 00 02 10 22 CB
  -> 38 00 C7
  -> exigir estrutura 00 02 00 XX CS com checksum FF
  -> 34 03 00 00 A0 28
  -> validar LEN/checksum
  -> salvar frame34 e payload34 em .bin
```

O fluxo não envia 0A, 14, 09, RUN/STOP remoto ou qualquer operação de escrita.

## 19. Evolução do laboratório

| Motor | Mudança principal |
|---|---|
| 1.13 | sessão limpa HELLO -> F0 -> 38 -> 34 |
| 1.14 | até 6 HELLOs por abertura da COM |
| 1.15 | aceitação dos dois vetores 38 então conhecidos |
| 1.16 | validação estrutural do 38 após surgir o terceiro valor `04` |

OpenLadder Studio v1.04 é o checkpoint de sincronização das descobertas; o motor de bancada permanece 1.16.

## 20. Arquivos centrais

```text
docs/TP02_PG_ESTADO_DA_ARTE.md
docs/tp02-pg-leitura-programa-emulacao.md
docs/tp02-pg-escrita-mapa-estatico.md
docs/tp02-pg-validacao-x0009-20260910.md
docs/data/tp02_pg_observations.tsv
docs/data/tp02_function_map_normalized.csv
scripts/emulate_pc12_readprog.py
src/OpenLadderStudio.Core/Tp02TargetCompiler.cs
src/OpenLadderStudio.Desktop/Tp02PgLab.cs.in
src/OpenLadderStudio.Desktop/TP02-PG-Tests.json
src/OpenLadderStudio.Desktop/PreparePgLab38StructuralV26.ps1
```

## 21. Estado atual — resolvido, corrigido e aberto

### Resolvido/confirmado

- serial 19200 8O1, DTR on, RTS off;
- checksum FF;
- HELLO em RUN/STOP;
- associação forte de `0x40` a RUN;
- resposta conhecida do F0 e sua intermitência;
- estrutura variável do 38;
- primeiro quadro 34;
- geometria 240 bytes = 80 passos em dois planos;
- paginação do 34 por contador de passos dependente do conteúdo;
- limite de 4000 passos para TP02-40/60MR(T);
- opcodes STR/STR NOT/AND/AND NOT/OR/OR NOT/OUT/TMR/CNT;
- palavras fixas vazio/AND STR/OR STR;
- funções F-xx e F-00/End;
- HIGH/LOW do encoder para X0009 confirmados fisicamente;
- fórmula linear geral de X refutada;
- primitiva 09 de escrita mapeada estaticamente;
- `0F 00 F0` como Clear All Memory.

### Corrigido após X0009

Não tratar mais como fato que `payload[0x0A0+i]` bruto seja diretamente o byte cujos bits `0x10/0x20/0x40` a rotina interna do PC12 usa para reconstruir o número do dispositivo.

A rotina estática existe, mas há uma transformação ou um mapeamento intermediário ainda não rastreado.

### Em aberto

- semântica exata do F0;
- semântica exata de `38 PAYLOAD[1]`;
- semântica completa do BRAW do quadro 34;
- transformação entre BRAW e representação interna usada pelo decoder;
- origem completa da classe X/Y/C no caminho booleano;
- família `0x08`–`0x0D`;
- fluxo B de leitura com diálogo OWL;
- download completo de programa;
- captura 34 de dois contatos em série;
- representação de paralelo/ramificação.

## 22. Próximos experimentos — ordem recomendada

### 1. Dois contatos em série

Gravar/manter exatamente:

```text
X0001 aberto -- X0002 aberto -- (Y0003)
```

Executar **PG Lab 1.16**, PLC em STOP, e capturar o `34`.

Objetivo: validar em hardware a sequência conceitual `STR X0001`, `AND X0002`, `OUT Y0003` e observar a posição do End.

### 2. Dois contatos em paralelo

```text
      +-- X0001 aberto --+
------|                  |------(Y0003)
      +-- X0002 aberto --+
```

Comparar byte a byte com a captura em série para estudar OR/ramificação.

### 3. Nova fronteira de endereço

Depois da estrutura série/paralelo, testar uma segunda fronteira, preferencialmente `X0017`, para verificar a progressão do HIGH e estudar BRAW em outro grupo.

### 4. Rastreamento estático dirigido

No `pc12.exe`, rastrear o caminho entre o buffer bruto de recepção `0x530230` e o byte efetivamente lido pela rotina `0x4B036C`–`0x4B03C6`. O objetivo é explicar por que X0009 chega fisicamente com `BRAW=02` enquanto a rotina estática testa bits altos em um segundo byte.

## 23. Regras de segurança permanentes

- PLC em STOP para o fluxo 34 conhecido;
- PC12 completamente fechado antes de abrir a COM no PG Lab;
- não enviar bytes arbitrários;
- não testar escrita, download, erase, firmware ou RUN/STOP remoto nesta fase;
- `0F 00 F0` permanece bloqueado;
- 38 somente após F0 válido na mesma sessão;
- 34 somente após 38 estruturalmente válido;
- se F0 falhar, fechar a COM e iniciar nova sessão;
- toda regra nova deve nascer de comparação controlada, alterando uma única variável de cada vez.

## 24. Como retomar no futuro

Contexto mínimo:

```text
Leia docs/TP02_PG_ESTADO_DA_ARTE.md.
Hardware: WEG TP02-60MR / TP02-40/60MR(T) V2.2.4K.
Software: OpenLadder Studio v1.04; PG Lab 1.16.
Serial: 19200 8O1, DTR on, RTS off.
Fluxo READ-ONLY: HELLO -> F0 -> 38 -> 34.
34: paginação por contador de passos; payload de 240 bytes em dois planos.
Última validação física: X0009 aberto -> Y0003, HIGH/LOW=01/10, BRAW=02.
A fórmula linear antiga foi refutada.
A associação direta BRAW -> byte interno de reconstrução de endereço também foi refutada e precisa ser rastreada.
Próximo teste: X0001 aberto + X0002 aberto em série -> Y0003, capturar 34.
Depois: mesmos contatos em paralelo.
Não habilitar escrita.
```

Consultar também:

- `docs/data/tp02_pg_observations.tsv`;
- `docs/tp02-pg-leitura-programa-emulacao.md`;
- `docs/tp02-pg-validacao-x0009-20260910.md`.
