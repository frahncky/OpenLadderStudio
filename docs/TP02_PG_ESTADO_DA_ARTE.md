# WEG TP02-60MR — estado da arte da engenharia reversa do protocolo PG

> **Documento canônico de recuperação da pesquisa.**
>
> Snapshot consolidado: **2026-09-10**. Base técnica analisada: `main` no commit `15cef64558714382642e9666fe47ce9c517110ec`, após o merge do PR #54. Release de sincronização: **OpenLadder Studio v1.04**. O motor de bancada permanece **TP02 PG Lab 1.16**.
>
> Este arquivo separa fatos de bancada, resultados de análise estática/emulação, hipóteses e pontos ainda desconhecidos. Ele deve ser a primeira referência para qualquer retomada futura da pesquisa.

## 1. Objetivo

Compreender suficientemente o protocolo serial PG do PLC **WEG TP02-60MR** para permitir leitura segura do programa, reconstrução do formato interno e, posteriormente, decodificação confiável do ladder.

O equipamento de bancada apresentou identificação **TP02-40/60MR(T) V2.2.4K**. O software histórico usado como referência é o **PC12 Design Center v2.1**.

A política de segurança permanece estritamente **READ-ONLY**. Escrita/download, apagamento, firmware e RUN/STOP remoto não são habilitados no OpenLadder Studio nesta fase.

## 2. Níveis de confiança

- **CONFIRMADO EM BANCADA** — observado fisicamente no PLC e validado por resposta/checksum.
- **RESOLVIDO POR ANÁLISE ESTÁTICA/EMULAÇÃO** — recuperado diretamente do `pc12.exe`, com verificações automáticas no repositório.
- **EVIDÊNCIA FORTE** — múltiplas observações coerentes, ainda sem prova completa de generalidade.
- **HIPÓTESE** — interpretação útil para orientar experimento, ainda não confirmada.
- **DESCONHECIDO** — sem evidência suficiente para atribuir significado.

Nenhuma hipótese deve ser promovida automaticamente a regra de protocolo.

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

Essa combinação já produziu HELLO, F0, 38 e 34 válidos.

### 3.2 Intermitência

O enlace PG é intermitente: várias tentativas podem retornar silêncio e uma tentativa posterior, sem mudança física, pode responder corretamente.

Estratégia atual do PG Lab 1.16:

1. abrir a COM em 19200 8O1, DTR on, RTS off;
2. tentar o HELLO até 6 vezes na mesma abertura;
3. se não houver HELLO-STOP válido, fechar a COM;
4. aguardar 1500 ms;
5. iniciar nova sessão limpa;
6. após HELLO-STOP, enviar F0 uma única vez naquela abertura da COM.

O laboratório usa até 12 sessões limpas por execução.

## 4. Checksum e enquadramento

**CONFIRMADO EM BANCADA:**

```text
sum(todos os bytes do quadro) mod 256 = 0xFF
```

Para construir o checksum:

```text
checksum = (0xFF - (sum(data) & 0xFF)) & 0xFF
```

Há uma família de respostas no formato:

```text
[FLAGS/STATE] [LEN] [PAYLOAD de LEN bytes] [CHECKSUM]
```

com tamanho total `LEN + 3` bytes. O formato é confirmado no 34 e compatível com 38, 0A e 14.

## 5. HELLO / sessão

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

**EVIDÊNCIA FORTE:** o bit `0x40` do primeiro byte acompanha RUN. Isso também aparece no comando 14.

O significado do bit/base `0x80` do HELLO permanece **DESCONHECIDO**.

## 6. Comando F0

```text
TX: F0 00 0F
RX conhecido: 00 02 10 22 CB
```

**CONFIRMADO EM BANCADA:**

- o F0 pode responder corretamente em STOP;
- o F0 também pode ficar silencioso mesmo após HELLO-STOP válido;
- STOP é necessário no fluxo de leitura de programa conhecido, mas não é suficiente para garantir resposta do F0.

**DESCONHECIDO:** semântica exata do F0.

**Regra importante:** F0 não é comando STOP. Não existe evidência para chamá-lo assim.

## 7. Comando 38

```text
TX: 38 00 C7
```

Respostas reais observadas:

```text
00 02 00 0A F3
00 02 00 02 FB
00 02 00 04 F9
```

Os três quadros têm:

```text
FLAGS      = 00
LEN        = 02
PAYLOAD[0] = 00
PAYLOAD[1] = variável
checksum   = FF
```

**EVIDÊNCIA FORTE:** `PAYLOAD[1]` depende do programa/estado relacionado ao programa e não é constante.

**DESCONHECIDO:** o significado exato desse byte variável.

O PG Lab 1.16 valida apenas a estrutura acima e registra o valor variável, sem lhe atribuir semântica.

## 8. Comando 34 — leitura de programa

### 8.1 Quadro base

Primeira leitura conhecida:

```text
TX: 34 03 00 00 A0 28
```

Resposta típica dos programas mínimos:

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

A quantidade é fixa em `0xA0` no fluxo recuperado. O contador avança de **1 a 4** conforme o tamanho da instrução decodificada. Portanto, a próxima requisição depende do conteúdo já lido.

A antiga hipótese de paginação fixa:

```text
00F0 -> 01E0 -> 02D0
```

está **DESCARTADA**.

Para o modelo `TP02-40/60MR(T)`, o PC12 fixa o limite de programa em **4000 passos (`0x0FA0`)**.

### 8.3 Geometria do bloco — resolvida

O payload de 240 bytes representa **80 passos de 3 bytes**, organizados em dois planos:

```text
Região A: payload[0x000 .. 0x09F] = 160 bytes = 2 bytes por passo
Região B: payload[0x0A0 .. 0x0EF] =  80 bytes = 1 byte por passo

Passo i:
  HIGH = payload[2*i]
  LOW  = payload[2*i + 1]
  B    = payload[0x0A0 + i]
```

Logo, os pares observados anteriormente em `0x001/0x0A0` e `0x003/0x0A1` **não são duplicatas**: são campos do mesmo passo distribuídos nos dois planos.

## 9. Decodificação de instruções

### 9.1 Booleanas

**RESOLVIDO POR ANÁLISE ESTÁTICA:** o PC12 mascara o byte LOW com `0x78` e identifica:

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

Os sete primeiros valores coincidem com o encoder já existente em `Tp02TargetCompiler.cs`. `TMR=0x60` e `CNT=0x68` foram recuperados posteriormente do decodificador.

Há ainda uma família de opcodes `0x08`–`0x0D` tratada em outro ramo, relacionada aos mesmos mnemônicos booleanos sem índice de bit. Ela não apareceu em bancada e permanece sem interpretação completa.

### 9.2 Palavras fixas

O PC12 reconhece também:

```text
0x00 = vazio/NOP
0x01 = AND STR
0x02 = OR STR
```

Esses valores coincidem com palavras fixas já reconstruídas no encoder.

## 10. Funções F-xx e fim do programa

**RESOLVIDO POR ANÁLISE ESTÁTICA:** quando o passo não corresponde ao caminho booleano/fixo, o PC12 usa o byte HIGH como índice de função em uma tabela de 72 entradas, com 64 handlers válidos.

O cruzamento com `docs/data/tp02_function_map_normalized.csv` coincidiu exatamente: os 64 índices com handler são os 64 números de função presentes no mapa.

O índice `0x00` corresponde a **F-00 / End**.

Portanto, o programa termina por uma instrução **End** no fluxo, e não simplesmente por um tamanho declarado.

## 11. Endereçamento e Região B

### 11.1 Reconstrução do número do dispositivo

**RESOLVIDO POR ANÁLISE ESTÁTICA:** o PC12 reconstrói o número do dispositivo combinando LOW e Região B:

```text
bits 0-2 <- LOW & 0x07
bit  3   <- LOW & 0x80
bits 4-6 <- B & 0x10 / 0x20 / 0x40
resultado final exibido = valor reconstruído + 1
```

Assim, a Região B carrega pelo menos os **bits altos do número do dispositivo**.

Os testes de bancada anteriores são todos compatíveis com essa reconstrução.

### 11.2 Por que a fórmula linear parecia funcionar

Para endereços até `n=8`, os bits altos permanecem zero e a aproximação linear observada em bancada coincide com a codificação real.

O primeiro ponto discriminante é **X0009**:

```text
modelo linear antigo: payload LOW = 0x18
modelo reconstruído:  payload LOW = 0x10, com bit alto migrando para Região B
```

Uma única captura de X0009 em hardware valida ou refuta esse aspecto da reconstrução.

### 11.3 Bits ainda desconhecidos

Os bits `4–6` da Região B têm papel de endereçamento recuperado. Os bits `0–3` e `7` continuam sem semântica completa neste caminho.

A origem exata da **classe do dispositivo** (`X`, `Y`, `C` etc.) no decodificador booleano também não está totalmente explicada pelo caminho estático já recuperado.

## 12. Experimentos de bancada consolidados

| Teste | Ladder mínimo | p001 | p002 | p003 | p0A0 | p0A1 |
|---|---|---:|---:|---:|---:|---:|
| A | X0001 fechado -> Y0002 | `18` | `20` | `41` | `09` | `07` |
| B | X0002 fechado -> Y0002 | `19` | `20` | `41` | `0A` | `07` |
| C | X0002 fechado -> Y0003 | `19` | `20` | `42` | `0A` | `08` |
| D | X0002 aberto -> Y0003 | `11` | `20` | `42` | `02` | `08` |
| E | X0001 aberto -> Y0003 | `10` | `20` | `42` | `01` | `08` |

Essas capturas bateram com o encoder já implementado em `Tp02TargetCompiler.cs` quando interpretadas segundo a geometria em dois planos.

O antigo `payload[0x002] = 0x20`, antes tratado como campo desconhecido, é agora interpretado como o **byte HIGH do passo da bobina**, coerente com a base do dispositivo Y no encoder.

## 13. Caso de dois contatos em série

Programa gravado:

```text
X0001 aberto -- X0002 aberto -- (Y0003)
```

No PG Lab 1.15, HELLO-STOP e F0 foram válidos, e o 38 retornou repetidamente:

```text
00 02 00 04 F9
```

Como a versão 1.15 ainda enumerava apenas dois vetores do 38, ela interrompeu antes do 34. Isso motivou o PG Lab 1.16, que passou a validar estruturalmente o 38.

Ainda falta uma captura de bancada do **34 desse programa em série** usando o PG Lab 1.16.

## 14. Comando 0A — leitura geral

Formato observado:

```text
0A 03 AH AL LEN CS
```

`AH AL` comporta-se como endereço big-endian nos probes usados.

Exemplos históricos:

```text
0A 03 60 00 AC E6
0A 03 60 AC AC 3A
```

O 0A responde em RUN e STOP; nas respostas observadas, RUN adiciona `0x40` no primeiro byte.

**IMPORTANTE:** não assumir que 0A lê o ladder. O escopo exato da memória lida continua diferente/mais amplo que o fluxo 34 e não está completamente caracterizado.

## 15. Comando 14

```text
TX: 14 00 EB
STOP RX: 00 00 FF
RUN  RX: 40 00 BF
```

Sua semântica exata permanece desconhecida. É uma evidência independente do bit `0x40` associado a RUN.

## 16. Mapa estático de escrita

A análise estática do `pc12.exe` encontrou a primitiva de escrita **0x09** como espelho do 0A de leitura:

```text
leitura: 0A [n=3] [end_hi] [end_lo] [qtd]              chk
escrita: 09 [n=5] [end_hi] [end_lo] [qtd] [d0] [d1]    chk
```

Também existe escrita em blocos maiores, incluindo quadros `09 11 ...` com 14 bytes de dados.

A rotina de escrita de registradores `V/D/WC/FILE` usa o comando 09.

**NÃO RESOLVIDO:** o laço completo de download de programa e a confirmação de que o download inteiro usa 09. A primitiva está mapeada, mas a orquestração não.

Este conhecimento é apenas documental. Nenhum código de transmissão de escrita foi habilitado.

## 17. Comando destrutivo bloqueado

```text
0F 00 F0
```

**RESOLVIDO POR ANÁLISE ESTÁTICA:** corresponde a **Clear All Memory**.

Permanece explicitamente bloqueado e não deve ser transmitido pelo PG Lab.

## 18. Fluxo READ-ONLY atual do PG Lab 1.16

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

O fluxo não envia 0A, 14, 09 nem qualquer operação de escrita.

## 19. Evolução do PG Lab

| Motor | Mudança principal |
|---|---|
| 1.13 | sessão limpa HELLO -> F0 -> 38 -> 34 |
| 1.14 | até 6 HELLOs por abertura da COM |
| 1.15 | aceitação dos dois vetores 38 então conhecidos |
| 1.16 | validação estrutural do 38 após surgir o terceiro valor `04` |

O motor permanece 1.16 nesta sincronização. A release v1.04 atualiza documentação, rastreabilidade e versionamento, sem alterar o protocolo transmitido pelo laboratório.

## 20. Arquivos centrais

```text
docs/TP02_PG_ESTADO_DA_ARTE.md
docs/tp02-pg-leitura-programa-emulacao.md
docs/tp02-pg-escrita-mapa-estatico.md
docs/data/tp02_pg_observations.tsv
docs/data/tp02_function_map_normalized.csv
scripts/emulate_pc12_readprog.py
src/OpenLadderStudio.Core/Tp02TargetCompiler.cs
src/OpenLadderStudio.Desktop/Tp02PgLab.cs.in
src/OpenLadderStudio.Desktop/TP02-PG-Tests.json
src/OpenLadderStudio.Desktop/PreparePgLab38StructuralV26.ps1
```

A análise offline de geometria/protocolo roda em CI e deve falhar se as conclusões verificáveis divergirem do `pc12.exe` usado como referência.

## 21. O que está resolvido e o que continua aberto

### Resolvido

- perfil serial funcional conhecido;
- regra de checksum;
- HELLO RUN/STOP;
- bit `0x40` fortemente associado a RUN;
- resposta conhecida do F0;
- estrutura variável do 38;
- primeiro quadro 34;
- geometria de 240 bytes em 80 passos/2 planos;
- paginação 34 por contador de passos dependente do conteúdo;
- limite de 4000 passos para TP02-40/60MR(T);
- opcodes STR/STR NOT/AND/AND NOT/OR/OR NOT/OUT/TMR/CNT;
- palavras fixas vazio/AND STR/OR STR;
- byte HIGH como número da função F-xx no caminho de funções;
- F-00 como End;
- uso dos bits 4–6 da Região B no endereço;
- `0x20` de p002 como HIGH do passo da bobina nos casos observados;
- primitiva 09 de escrita de memória/registradores;
- `0F 00 F0` como Clear All Memory.

### Em aberto

- semântica exata do F0;
- semântica exata de `38 PAYLOAD[1]`;
- bits 0–3 e 7 da Região B;
- origem completa da classe do dispositivo no caminho booleano;
- família de opcodes 0x08–0x0D;
- fluxo B de leitura que passa por diálogo OWL;
- orquestração completa do download de programa;
- confirmação física em X0009 da reconstrução de endereço;
- captura 34 de dois contatos em série;
- representação de ramificações/paralelo em bancada.

## 22. Próximos experimentos — ordem recomendada

### 1. Teste discriminante X0009

Gravar:

```text
X0009 aberto -- (Y0003)
```

PLC em STOP, PG Lab 1.16. Este é o teste mais barato e informativo para validar em hardware o modelo de endereço recuperado do PC12.

### 2. Captura do programa em série

Manter exatamente:

```text
X0001 aberto -- X0002 aberto -- (Y0003)
```

Executar PG Lab 1.16 e capturar o 34.

O resultado esperado conceitualmente é uma sequência compatível com `STR X0001`, `AND X0002`, `OUT Y0003`, seguida de `End`, mas a comparação deve ser feita byte a byte antes de promover qualquer interpretação.

### 3. Comparação em paralelo

Construir:

```text
      +-- X0001 aberto --+
------|                  |------(Y0003)
      +-- X0002 aberto --+
```

Comparar com a captura em série para estudar OR/ramificação e estrutura de rung.

### 4. Validações posteriores

- testar X0001, X0002, X0004, X0008, X0009 e X0016;
- repetir aberto/fechado em diferentes posições do rung;
- testar Y/C como contato;
- testar mais de um rung e mais de uma bobina;
- capturar a sequência completa do `Read PLC/Upload` do PC12 original para confrontar com a emulação;
- só depois formalizar decoder completo e qualquer decisão futura sobre escrita.

## 23. Regras de segurança permanentes

- usar STOP para o fluxo 34 conhecido;
- fechar completamente o PC12 antes de abrir a COM no PG Lab;
- não enviar bytes arbitrários;
- não testar escrita, download, erase, firmware ou RUN/STOP remoto durante esta fase;
- `0F 00 F0` permanece bloqueado;
- 38 somente após F0 válido na mesma sessão;
- 34 somente após 38 estruturalmente válido;
- se F0 falhar, fechar a COM e iniciar nova sessão;
- toda nova regra deve nascer de comparação controlada com uma única variável alterada.

## 24. Como retomar no futuro

Contexto mínimo para uma nova sessão:

```text
Leia docs/TP02_PG_ESTADO_DA_ARTE.md.
Hardware: WEG TP02-60MR / TP02-40/60MR(T) V2.2.4K.
Software de bancada: PG Lab 1.16.
Serial conhecido: 19200 8O1, DTR on, RTS off.
Fluxo READ-ONLY: HELLO -> F0 -> 38 -> 34.
34: paginação por contador de passos, não offset fixo.
Payload 34: 80 passos, dois planos A/B.
Próximo teste prioritário: X0009 aberto -> Y0003.
Depois: capturar X0001 + X0002 em série -> Y0003 e comparar com paralelo.
Não promover hipóteses a fatos e não habilitar escrita.
```

Consultar também `docs/data/tp02_pg_observations.tsv` e `docs/tp02-pg-leitura-programa-emulacao.md`.
