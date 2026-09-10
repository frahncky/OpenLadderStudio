# WEG TP02-60MR — estado da arte da engenharia reversa do protocolo PG

> **Documento canônico de recuperação da pesquisa.**
>
> Snapshot técnico: **2026-09-09 23:42 BRT**. Estado do software neste ponto: **OpenLadder Studio v1.03**, **TP02 PG Lab 1.16**, `main` no commit `840738c34928d2f534f26c42b3adb74d8dff3692`.
>
> Este arquivo deve ser atualizado sempre que uma hipótese for confirmada, refutada ou refinada. Ele separa explicitamente fatos observados, inferências fortes e pontos ainda desconhecidos para evitar que uma retomada futura confunda hipótese com protocolo confirmado.

## 1. Objetivo da pesquisa

O objetivo é compreender suficientemente o protocolo serial PG do PLC **WEG TP02-60MR** para permitir, inicialmente, leitura segura do programa armazenado no PLC e, posteriormente, decodificação do ladder. Escrita/download, RUN/STOP remoto, apagamento e firmware permanecem fora do escopo enquanto não houver evidência experimental e validação específica de segurança.

A investigação usa o **PC12 Design Center v2.1** como referência histórica e o **OpenLadder Studio / TP02 PG Lab** como ferramenta experimental. O PLC de bancada apresentou identificação **TP02-40/60MR(T) V2.2.4K**.

## 2. Convenção epistemológica

As anotações usam quatro níveis:

- **CONFIRMADO** — observado fisicamente de forma direta e reproduzível, ou validado por checksum/estrutura em bancada.
- **EVIDÊNCIA FORTE** — vários testes controlados apontam para a mesma interpretação, mas ainda faltam pontos de validação para chamar de regra geral.
- **HIPÓTESE** — interpretação útil para orientar testes, ainda não demonstrada.
- **DESCONHECIDO** — sem evidência suficiente para atribuir significado.

Nenhuma hipótese deve virar regra automática de escrita ou compilação apenas por parecer coerente.

## 3. Camada serial conhecida

### 3.1 Perfil funcional

**CONFIRMADO em sessões de bancada:**

```text
Baud:       19200
Data bits:  8
Parity:     Odd
Stop bits:  1
DTR:        ON
RTS:        OFF
```

Esse perfil já produziu HELLO, F0, 38 e 34 válidos. Isso prova que o caminho PC ↔ conversor ↔ TP02 pode operar nessa configuração. Não significa que seja a única combinação possível em todo cenário.

### 3.2 Intermitência observada

O enlace PG é fortemente intermitente. É comum várias tentativas de `CON-ICB\r` retornarem silêncio e uma tentativa posterior, sem mudança de programa, retornar o handshake correto.

A estratégia que se mostrou prática no PG Lab atual é:

1. abrir a COM em 19200 8O1, DTR on, RTS off;
2. manter a mesma COM aberta;
3. tentar o HELLO até 6 vezes;
4. se nenhum HELLO-STOP válido aparecer, fechar a COM;
5. aguardar 1500 ms;
6. iniciar nova sessão limpa;
7. quando houver HELLO-STOP, enviar F0 **uma única vez naquela abertura da COM**.

O PG Lab usa até 12 sessões limpas por execução.

## 4. Checksum e formato geral

### 4.1 Checksum

**CONFIRMADO:** todos os quadros binários conhecidos fecham com:

```text
sum(todos os bytes) mod 256 = 0xFF
```

Para construir o último byte a partir dos bytes anteriores:

```text
checksum = (0xFF - (sum(data) & 0xFF)) & 0xFF
```

### 4.2 Estrutura de respostas com comprimento

Há forte evidência de uma família de respostas no formato:

```text
[FLAGS/STATE] [LEN] [PAYLOAD de LEN bytes] [CHECKSUM]
```

Tamanho total esperado:

```text
LEN + 3 bytes
```

Esse formato é confirmado para respostas do 34 e é compatível com 38, 0A e 14 em suas formas observadas.

## 5. HELLO / estabelecimento de sessão

### 5.1 Requisição

**CONFIRMADO:**

```text
ASCII: CON-ICB\r
HEX:   43 4F 4E 2D 49 43 42 0D
```

### 5.2 Resposta em STOP

**CONFIRMADO:**

```text
80 01 09 75
```

Checksum: `0xFF`.

### 5.3 Resposta em RUN

**CONFIRMADO:**

```text
C0 01 09 35
```

Checksum: `0xFF`.

### 5.4 Bit de estado RUN

**EVIDÊNCIA FORTE:** a diferença `0x40` no primeiro byte acompanha RUN em mais de uma família de respostas:

```text
STOP HELLO: 80 ...
RUN  HELLO: C0 ...
             ^ +0x40

STOP cmd 14: 00 00 FF
RUN  cmd 14: 40 00 BF
             ^ +0x40
```

Portanto, o bit `0x40` do primeiro byte está fortemente associado ao estado RUN. O significado do bit/base `0x80` do HELLO continua **DESCONHECIDO** e não deve ser nomeado sem evidência adicional.

## 6. Comando F0

### 6.1 Requisição

**CONFIRMADO:**

```text
TX: F0 00 0F
```

### 6.2 Resposta conhecida

**CONFIRMADO:**

```text
RX: 00 02 10 22 CB
```

Checksum: `0xFF`.

### 6.3 Comportamento

O F0 frequentemente fica silencioso mesmo após HELLO-STOP válido. Em outras sessões, responde imediatamente com o vetor acima. Portanto:

- **CONFIRMADO:** STOP é necessário para o fluxo conhecido de leitura de programa.
- **CONFIRMADO:** STOP, sozinho, não garante resposta do F0.
- **DESCONHECIDO:** semântica exata de `F0 00 0F`.
- **IMPORTANTE:** F0 **não deve ser chamado de comando STOP**. Não existe evidência para isso.
- **HIPÓTESE operacional:** repetir F0 várias vezes na mesma sessão pode não ajudar e pode alterar o estado interno da sessão. Isso não foi provado como fenômeno do protocolo; por segurança experimental, o PG Lab atual usa um único F0 por abertura da COM.

## 7. Comando 38

### 7.1 Requisição

**CONFIRMADO:**

```text
TX: 38 00 C7
```

O comando só é enviado no fluxo atual depois de F0 conhecido e válido na mesma sessão serial.

### 7.2 Respostas observadas

**CONFIRMADO em bancada:**

```text
00 02 00 0A F3
00 02 00 02 FB
00 02 00 04 F9
```

Todos fecham checksum `0xFF`.

Os três compartilham:

```text
FLAGS      = 00
LEN        = 02
PAYLOAD[0] = 00
PAYLOAD[1] = variável
CHECKSUM   = fecha em FF
```

### 7.3 Interpretação atual

**EVIDÊNCIA FORTE:** `PAYLOAD[1]` varia em função do programa carregado e não deve ser tratado como uma constante de protocolo.

**DESCONHECIDO:** significado exato do byte variável. Pode representar tamanho, quantidade de unidades, metadado de programa ou outro parâmetro, mas nenhuma dessas interpretações está confirmada.

O **PG Lab 1.16** deixou de enumerar vetores fixos e valida estruturalmente o quadro 38, exigindo:

```text
FLAGS = 00
LEN = 02
PAYLOAD[0] = 00
checksum válido
```

O byte `PAYLOAD[1]` é apenas registrado.

## 8. Comando 34 — leitura do bloco de programa

### 8.1 Requisição base conhecida

**CONFIRMADO:**

```text
TX: 34 03 00 00 A0 28
```

### 8.2 Resposta

**CONFIRMADO:** o TP02 retorna, nos programas mínimos testados:

```text
00 F0 [240 bytes de payload] [checksum]
```

Tamanho total: **243 bytes**.

O PG Lab valida o quadro por `LEN + checksum FF` e salva:

```text
TP02-PG-34-frame-<timestamp>.bin
TP02-PG-34-payload-<timestamp>.bin
```

### 8.3 O que o 34 representa

**EVIDÊNCIA FORTE:** o payload retornado por 34 contém informação diretamente relacionada ao programa ladder, porque mudanças controladas de endereço e tipo de contato alteraram somente bytes específicos e previsíveis desse payload.

**DESCONHECIDO:** semântica completa dos campos, paginação, fim do programa, endereços internos e existência de blocos seguintes.

Não assumir ainda que `00 00 A0` seja um endereço linear ou que solicitações seguintes sejam `00F0`, `01E0`, `02D0` etc. Essa paginação foi apenas uma hipótese de trabalho anterior e **não está confirmada**.

## 9. Experimentos controlados do payload 34

Os testes abaixo alteraram uma única propriedade do ladder por vez. Isso permitiu isolar campos relacionados ao operando X, ao operando Y e ao tipo do contato.

### 9.1 Capturas consolidadas

| Teste | Ladder mínimo | `payload[0x001]` | `payload[0x002]` | `payload[0x003]` | `payload[0x0A0]` | `payload[0x0A1]` | Interpretação |
|---|---|---:|---:|---:|---:|---:|---|
| A | X0001 **fechado** → Y0002 | `18` | `20` | `41` | `09` | `07` | baseline inicial |
| B | X0002 **fechado** → Y0002 | `19` | `20` | `41` | `0A` | `07` | mudou somente X |
| C | X0002 **fechado** → Y0003 | `19` | `20` | `42` | `0A` | `08` | mudou somente Y |
| D | X0002 **aberto** → Y0003 | `11` | `20` | `42` | `02` | `08` | mudou somente tipo do contato |
| E | X0001 **aberto** → Y0003 | `10` | `20` | `42` | `01` | `08` | mudou somente X |

Nos programas mínimos acima, os demais bytes do payload permaneceram zero, exceto os campos listados.

### 9.2 Endereço da entrada X

Comparações controladas:

```text
X0001 aberto:  payload[0x001] = 10 ; payload[0x0A0] = 01
X0002 aberto:  payload[0x001] = 11 ; payload[0x0A0] = 02

X0001 fechado: payload[0x001] = 18 ; payload[0x0A0] = 09
X0002 fechado: payload[0x001] = 19 ; payload[0x0A0] = 0A
```

**EVIDÊNCIA FORTE:** o endereço de X é linear nesses dois pontos consecutivos e aparece em pelo menos duas regiões do bloco.

Fórmulas candidatas, válidas apenas para os pontos já observados:

```text
Contato aberto Xn:
  payload[0x001] ≈ 0x0F + n
  payload[0x0A0] ≈ n

Contato fechado Xn:
  payload[0x001] ≈ 0x17 + n
  payload[0x0A0] ≈ 0x08 + n
```

Essas fórmulas ainda devem ser validadas em endereços não adjacentes antes de serem consideradas regras gerais.

### 9.3 Tipo do contato aberto/fechado

Comparação mantendo exatamente `X0002` e `Y0003`:

```text
X0002 fechado: 19 / 0A
X0002 aberto:  11 / 02
                ^    ^
             diferença 0x08
```

**EVIDÊNCIA FORTE:** o tipo fechado adiciona o bit/valor `0x08` nas duas representações relacionadas ao contato X. Ainda é necessário testar outros endereços e outros contextos de instrução para saber se isso é um bit de opcode geral, um modificador NOT ou parte de uma codificação mais ampla.

### 9.4 Endereço da saída Y

Comparação mantendo X0002 fechado e alterando somente a bobina:

```text
Y0002: payload[0x003] = 41 ; payload[0x0A1] = 07
Y0003: payload[0x003] = 42 ; payload[0x0A1] = 08
```

**EVIDÊNCIA FORTE:** o endereço Y é linear nesses dois pontos consecutivos e também aparece em duas regiões.

Fórmulas candidatas, ainda não gerais:

```text
Yn:
  payload[0x003] ≈ 0x3F + n
  payload[0x0A1] ≈ 0x05 + n
```

### 9.5 Byte constante 0x20

Nos cinco programas mínimos de um contato + uma bobina, `payload[0x002]` permaneceu `0x20`.

**DESCONHECIDO:** significado do `0x20`. Pode pertencer ao opcode/estrutura da instrução de saída, separador, metadado ou outra codificação. Não atribuir significado ainda.

## 10. Novo caso: dois contatos abertos em série

Programa carregado no teste de 21:10:

```text
X0001 aberto -- X0002 aberto -- (Y0003)
```

No PG Lab 1.15, houve HELLO-STOP e F0 válidos. O 38 retornou de forma reproduzível:

```text
00 02 00 04 F9
```

Como a 1.15 aceitava apenas `...0A...` e `...02...`, ela interrompeu corretamente o teste antes do 34. Esse caso motivou a validação estrutural do 38 no PG Lab 1.16.

**PONTO EXATO DE RETOMADA:** atualizar/usar OpenLadder Studio v1.03 com PG Lab 1.16, manter esse mesmo ladder sem qualquer alteração, colocar o TP02 em STOP e repetir o Teste Único. O objetivo é obter o payload do 34 para o programa com dois contatos em série.

Depois disso, o próximo experimento recomendado é construir os mesmos dois contatos **em paralelo**, mantendo X0001, X0002 e Y0003, para comparar a representação de lógica série versus paralela e começar a separar AND/OR, sequência e estrutura de rung.

## 11. Comando 0A — leitura geral de memória/sistema

### 11.1 Formato de requisição observado

**CONFIRMADO para os probes usados:**

```text
0A 03 AH AL LEN CS
```

onde `AH AL` se comporta como endereço de 16 bits big-endian e `LEN` como quantidade solicitada nos testes realizados. O checksum fecha em FF.

Exemplos usados no histórico do laboratório:

```text
0A 03 60 00 AC E6
0A 03 60 AC AC 3A
```

### 11.2 Respostas RUN/STOP

**CONFIRMADO:** o comando 0A produz respostas tanto em STOP quanto em RUN. Nos quadros observados, o primeiro byte muda de `00` em STOP para `40` em RUN, reforçando a associação do bit `0x40` ao estado RUN.

### 11.3 Escopo semântico

**IMPORTANTE:** 0A deve ser tratado como leitura de memória/sistema genérica. Não existe evidência suficiente para dizer que 0A corresponde à memória do ladder ou ao mesmo conteúdo retornado por 34.

Em um teste STOP antigo, as primeiras consultas 0A chegaram a ficar silenciosas e depois voltaram a responder sem mudança física aparente, demonstrando que o enlace/protocolo possui estado ou temporização ainda não totalmente compreendidos.

## 12. Comando 14

**CONFIRMADO:**

```text
TX: 14 00 EB

STOP RX: 00 00 FF
RUN  RX: 40 00 BF
```

O comando foi observado no ramo histórico associado a consulta de senha/estado no PC12, mas sua semântica exata ainda é **DESCONHECIDA**. Ele é útil como evidência independente do bit `0x40` de RUN.

O 14 não faz parte do Teste Único focado atual do PG Lab 1.16.

## 13. Comando perigoso explicitamente bloqueado

```text
0F 00 F0
```

Está associado, na pesquisa do PC12, a **Clear All Memory**. Permanece classificado como `BLOCKED` e nunca deve ser transmitido pela campanha READ-ONLY.

## 14. Fluxo READ-ONLY atual do PG Lab 1.16

Fluxo canônico:

```text
ABRIR COM
  19200 8O1
  DTR=ON
  RTS=OFF
     |
     v
HELLO = CON-ICB\r
  até 6 tentativas na mesma COM
     |
     +--> C0 01 09 35 => PLC_RUN_NEEDS_STOP; parar
     |
     +--> sem STOP válido => fechar COM, 1500 ms, nova sessão
     |
     v
80 01 09 75
HELLO-STOP confirmado
     |
     v
F0 00 0F
  exatamente 1 vez nessa abertura da COM
     |
     +--> sem 00 02 10 22 CB => fechar COM, 1500 ms, nova sessão
     |
     v
00 02 10 22 CB
     |
     v
38 00 C7
     |
     +--> exigir estrutura 00 02 00 XX CS com checksum FF
     |
     v
34 03 00 00 A0 28
     |
     +--> exigir quadro LEN/checksum válido
     |
     v
salvar frame34 + payload34 em .bin
```

O fluxo atual não envia 0A nem 14 e não executa nenhuma escrita.

## 15. Histórico útil de versões do PG Lab

A evolução do laboratório é importante para compreender por que existem scripts de preparação sequenciais no build:

| Motor | Mudança principal |
|---|---|
| 1.7 | matriz pós-handshake do F0 |
| 1.8 | agente adaptativo com Safety Gate |
| 1.9 | pesquisa contínua/persistente |
| 1.10 | repetição de F0 na mesma sessão — estratégia posteriormente abandonada no fluxo focado |
| 1.11 | varredura READ-ONLY 0A |
| 1.12 | decodificação básica de respostas 0A |
| 1.13 | sessão limpa HELLO → F0 → 38 → 34; um F0 por abertura da COM |
| 1.14 | até 6 HELLOs mantendo a mesma COM aberta |
| 1.15 | aceitação conservadora dos dois vetores 38 conhecidos naquele momento |
| 1.16 | validação estrutural do 38, após aparecer o terceiro valor `04` |

No build atual, `BuildTp02Lab.bat` aplica os scripts V11–V26 em sequência. A preparação específica da 1.16 é `PreparePgLab38StructuralV26.ps1`.

## 16. Releases e commits que marcam a fase atual

Marcos importantes:

- PR #48 / PG Lab 1.13 — sessão limpa para leitura de programa.
- OpenLadder v1.00 — primeiro bump necessário para o updater reconhecer a nova versão após a integração do PG Lab 1.13.
- PG Lab 1.14 / OpenLadder v1.01 — múltiplos HELLOs por sessão, F0 único.
- PG Lab 1.15 / OpenLadder v1.02 — dois vetores 38 observados aceitos.
- PG Lab 1.16 / OpenLadder v1.03 — 38 validado estruturalmente.
- `main` de referência deste documento: `840738c34928d2f534f26c42b3adb74d8dff3692`.

A release **v1.03** contém `OpenLadder-Studio-Setup.exe` e corresponde ao PG Lab 1.16.

## 17. Regra do updater do OpenLadder

O updater compara a versão local de `version.txt` com o `tag_name` da última release do GitHub. Alterar somente o motor PG Lab sem aumentar a versão do aplicativo pode deixar o usuário sem perceber a atualização. Por isso, mudanças do PG Lab destinadas à bancada devem, quando apropriado, ser acompanhadas de bump de versão do OpenLadder e nova release.

## 18. Evidências de bancada e arquivos importantes

Logs que marcaram descobertas relevantes nesta campanha:

```text
TP02-PG-Lab-20260909-191330.txt  -> sessão STOP conhecida; F0/38/34 válidos
TP02-PG-Lab-20260909-192413.txt  -> PLC em RUN; F0 silencioso; 0A funcional
TP02-PG-Lab-20260909-194007.txt  -> STOP com F0 silencioso; recuperação posterior de 0A
TP02-PG-Lab-20260909-205418.txt  -> X0002 fechado, Y0003; 34 válido
TP02-PG-Lab-20260909-210008.txt  -> X0002 aberto, Y0003; isolou efeito aberto/fechado
TP02-PG-Lab-20260909-210518.txt  -> X0001 aberto, Y0003; confirmou linearidade local de X
TP02-PG-Lab-20260909-211032.txt  -> X0001 aberto + X0002 aberto em série; 38 = 00 02 00 04 F9
```

Arquivos binários especialmente úteis quando disponíveis:

```text
TP02-PG-34-frame-<timestamp>.bin
TP02-PG-34-payload-<timestamp>.bin
```

A comparação deve ser sempre feita byte a byte e com registro exato do ladder que estava no PLC naquele momento.

## 19. O que NÃO está confirmado

Não tratar como fato:

- que F0 seja STOP;
- que `0x80` no primeiro byte do HELLO tenha um significado específico;
- que 38 seja definitivamente tamanho de programa;
- que o byte variável do 38 conte contatos/instruções;
- que 0A leia o ladder;
- que o bloco 34 atual seja o programa inteiro;
- que a paginação de 34 use incrementos de `0xF0`;
- que os campos duplicados em `0x001/0x0A0` e `0x003/0x0A1` tenham já uma semântica estrutural conhecida;
- que as fórmulas candidatas de X/Y sejam válidas para toda a faixa de endereços;
- que `0x08` seja universalmente “NOT” em qualquer opcode/contexto;
- que silêncio do PLC signifique falha elétrica; sessões válidas demonstram intermitência de estado/temporização.

## 20. Próximos experimentos recomendados

### Experimento imediato

Manter exatamente:

```text
X0001 aberto -- X0002 aberto -- (Y0003)
```

Usar **PG Lab 1.16 / OpenLadder v1.03**, PLC em STOP, e capturar o 34. Não mudar nenhum elemento entre a gravação do programa e a leitura.

### Depois da captura série

Construir:

```text
      +-- X0001 aberto --+
------|                  |------(Y0003)
      +-- X0002 aberto --+
```

Comparar o 34 de série versus paralelo. Objetivo: identificar como o TP02 representa encadeamento booleano, ramificação e/ou opcodes equivalentes a AND/OR.

### Validações adicionais importantes

Depois de entender a estrutura de dois contatos:

1. testar X0001, X0002, X0004 e X0016 para validar linearidade e fronteiras de endereço;
2. repetir aberto/fechado em mais de um endereço e posição no rung;
3. testar família Y/C/SC como contato para separar família de operando de opcode;
4. testar duas saídas/rungs distintos para identificar delimitadores de rung/programa;
5. capturar no PC12 original a sequência completa de TX do comando “Read PLC/Upload” para descobrir a paginação real do 34;
6. somente depois formalizar um decoder de programa.

## 21. Regras de segurança para qualquer continuação

- PLC deve permanecer em **STOP** para o fluxo 34 conhecido.
- Fechar completamente o PC12 antes de abrir a COM no PG Lab.
- Não enviar bytes arbitrários.
- Não testar escrita, download, erase, firmware ou RUN/STOP remoto durante esta fase.
- `0F 00 F0` permanece bloqueado.
- 38 só depois de F0 válido na mesma sessão.
- 34 só depois de 38 estruturalmente válido.
- Se o F0 falhar, fechar a COM e começar nova sessão; não insistir com uma sequência de F0s na mesma abertura no fluxo focado.
- Toda nova regra de decodificação deve nascer de comparação controlada em que somente uma variável do ladder muda por vez.

## 22. Como retomar a pesquisa no futuro

Ao abrir uma nova conversa, issue ou sessão de desenvolvimento, fornecer como contexto mínimo:

```text
Leia docs/TP02_PG_ESTADO_DA_ARTE.md.
Estado de referência: OpenLadder v1.03, PG Lab 1.16.
Hardware: WEG TP02-60MR / TP02-40/60MR(T) V2.2.4K.
Serial conhecido: 19200 8O1, DTR on, RTS off.
Fluxo conhecido: HELLO -> F0 -> 38 -> 34, um F0 por abertura da COM.
Ponto de retomada: obter o 34 do ladder X0001 aberto em série com X0002 aberto -> Y0003.
Não promover hipóteses a fatos e manter a campanha estritamente READ-ONLY.
```

Depois, consultar também `docs/data/tp02_pg_observations.tsv` para uma visão estruturada dos vetores e capturas.

## 23. Arquivos do repositório relacionados

Arquivos centrais da implementação atual:

```text
src/OpenLadderStudio.Desktop/BuildTp02Lab.bat
src/OpenLadderStudio.Desktop/Tp02PgLab.cs.in
src/OpenLadderStudio.Desktop/TP02-PG-Tests.json
src/OpenLadderStudio.Desktop/PreparePgLabCleanSessionV23.ps1
src/OpenLadderStudio.Desktop/PreparePgLabHelloRetryV24.ps1
src/OpenLadderStudio.Desktop/PreparePgLab38VariantsV25.ps1
src/OpenLadderStudio.Desktop/PreparePgLab38StructuralV26.ps1
```

Existe um espelho de compatibilidade de `TP02-PG-Tests.json` em:

```text
PC12_v2.1_Windows7_v3_portatil/TP02-PG-Tests.json
```

Os dois JSONs devem permanecer sincronizados byte a byte quando o pacote for alterado.

## 24. Síntese técnica em uma página

O TP02 responde ao HELLO `CON-ICB\r` com `80 01 09 75` em STOP e `C0 01 09 35` em RUN. O bit `0x40` está fortemente associado a RUN. Em STOP, algumas sessões permitem `F0 00 0F -> 00 02 10 22 CB`. Depois disso, `38 00 C7` retorna um quadro `00 02 00 XX CS`, com `XX` variável conforme o programa e checksum FF. O PG Lab 1.16 valida essa estrutura sem assumir o significado de `XX`. Em seguida, `34 03 00 00 A0 28` retorna `00 F0 + 240 bytes + checksum`, conteúdo fortemente relacionado ao ladder.

Nos programas mínimos, mudanças controladas mostraram que X e Y aparecem em duas regiões do payload. Para X0001/X0002, o endereço varia linearmente; mudar o contato de aberto para fechado adiciona `0x08` nos dois bytes associados a X. Para Y0002/Y0003, os dois bytes associados à saída também variam linearmente. O próximo ponto experimental é capturar o 34 de dois contatos abertos em série, pois o teste anterior chegou até o 38 e revelou o novo valor `04`, mas a versão 1.15 bloqueou corretamente o 34. A versão 1.16 foi criada exatamente para permitir essa próxima captura de forma ainda READ-ONLY.