# TP02 PG — mapa offline dos comandos de escrita do PC12

Data: 2026-09-10/11 · Método: análise estática + emulação Unicorn exclusivamente offline do `pc12.exe` · **Nenhum byte transmitido a um PLC**

## Aviso de escopo

Este documento mapeia o que o `pc12.exe` monta e como ele percorre parte do caminho de escrita
do programa do TP02. Nada aqui habilita download no OpenLadder Studio. A política do projeto
continua estritamente READ-ONLY na bancada: `0F 00 F0` bloqueado, nenhuma escrita, apagamento,
firmware ou RUN/STOP remoto.

O objetivo é saber o que a escrita **seria** sem executá-la em hardware. Os emuladores novos
rodam o código de máquina original do PC12 dentro do Unicorn e interceptam os pontos de TX antes
de qualquer I/O do host.

## Inventário resumido

| CMD | classe | papel atual |
|---:|---|---|
| `0xF0` | leitura/preflight | semântica exata ainda desconhecida |
| `0x38` | leitura | metadado estrutural do programa |
| `0x34` | leitura | bloco do programa |
| `0x0A` | leitura | memória/registradores |
| `0x14` | leitura | consulta |
| `0x09` | **escrita** | memória/registradores; espelho estrutural do `0A` |
| `0x33` | **escrita de programa — confirmada dinamicamente offline no PC12** | quadro dedicado no caminho `Write PLC Program` |
| `0x0F` | **escrita destrutiva** | `0F 00 F0` = Clear All Memory |
| `0x01`–`0x04`, `0x11`, `0x12`, `0x13`, `0x35`, `0x37` | — | controle/sessão, ainda não classificados |

> “Confirmada dinamicamente offline” significa que o construtor real do PC12 foi executado no
> Unicorn e produziu exatamente os quadros previstos. **Não significa confirmação física no PLC.**

## Descoberta principal: `Write PLC Program` monta um quadro dedicado `0x33`

A hipótese antiga de que o download completo provavelmente usaria a primitiva `0x09` deixou de
ser a hipótese principal. A análise do caminho exato `Write PLC Program...` isolou um construtor
dedicado que grava `0x33` no primeiro byte do buffer TX e, imediatamente depois, chega à rotina
de transmissão do PC12 (`0x0046F5E6`).

### Correlação direta com o menu

A string `Write PLC Program...` tem um único xref no código, em `0x004B6B08`, dentro da região
que leva ao construtor. No mesmo caminho, o PC12 monta o quadro em `0x004B7958` e alcança
chamadas diretas da rotina TX a partir de `0x004B7A1A`.

Foram localizados 15 `CALL rel32` para `0x0046F5E6` entre `0x004B7A1A` e `0x004B7C4A`.
Esses pontos **não são 15 quadros diferentes**. Entre eles o PC12 testa timeout, erro e checksum
(`0x4FA8B7..0x4FA8B9`) e repete a mesma chamada quando alguma flag está ativa. A leitura estática
mais consistente é uma cadeia de retentativas do mesmo quadro já montado.

## Estrutura do quadro `0x33`

O construtor em `0x004B7958` mostra diretamente:

```text
TX[0] = 33
TX[1] = (+0x56) + ((+0x56)/2) + 4
TX[2] = 00
TX[3] = byte em +0x6A
TX[4] = byte em +0x6E
TX[5] = byte em +0x56
TX[last] = FF - soma(TX[0..last-1])
TX_LEN = last + 1
```

A análise dos campos internos e a execução do construtor real no Unicorn fecham a geometria.

### Inicialização de cada bloco

Em `0x004B7D22` o PC12 inicia um novo bloco com:

```text
+0x7A = 0        # contador de registros do bloco
+0x5E = 6        # próximo índice no TX, logo após o cabeçalho
+0x62 = 0        # quantidade de bytes EXTERNAL acumulados
+0x56 = 0        # quantidade de bytes HIGH/LOW acumulados
+0x7E = +0x76    # endereço de passo inicial do bloco
```

O cursor `+0x76` é o endereço real de programa e avança em **1, 2, 3 ou 4 passos**, conforme a
classe da instrução. Isso é independente do contador de registros do bloco.

### Formação de cada registro

Os ramos de codificação mostram o mesmo padrão. Um exemplo em `0x004B6CCB..0x004B6D08`:

```text
TX[+0x5E] = HIGH
+0x5E++
TX[+0x5E] = LOW
+0x5E++
(+0xE0)[+0x62] = EXTERNAL
+0x62++
+0x76 += tamanho_em_passos_da_instrução
+0x7A++
+0x56 += 2
```

Portanto, para `N` **registros de código de máquina**:

```text
+0x56 = 2*N                 # HIGH/LOW já gravados em TX[6..]
+0x62 = N                   # EXTERNAL acumulados em +0xE0
+0x5E = 6 + 2*N             # primeiro byte após o plano HIGH/LOW
```

No construtor final, os `N` bytes EXTERNAL são copiados de `+0xE0` para o TX a partir de
`+0x5E`. O corpo é:

```text
[2*N bytes HIGH/LOW][N bytes EXTERNAL]
```

Logo:

```text
TX[1] = 3*N + 4
TX[5] = 2*N
```

### Endereço inicial do bloco

`+0x7E` recebe o valor do cursor de passo `+0x76` no início do bloco. Mais adiante o PC12 formata
essa posição em quatro dígitos hexadecimais e converte os dois primeiros nibbles para `+0x6A`
e os dois últimos para `+0x6E`. O construtor copia esses bytes para:

```text
TX[3] = step_hi
TX[4] = step_lo
```

### Limite do bloco: 20 registros, não 80

Em `0x004B7869` o PC12 compara o contador `+0x7A` com `0x14`; ao atingir 20 registros ele encerra
a coleta e segue para a montagem do `0x33`.

Portanto, o limite observado no caminho de escrita é **20 registros por quadro**. Ele não deve
ser confundido com os 80 registros por página do comando de leitura `0x34`.

O quadro é:

```text
33 [3*N+4] 00 [step_hi] [step_lo] [2*N]
   [2*N bytes HIGH/LOW]
   [N bytes EXTERNAL]
   [checksum]

1 <= N <= 20
```

Para o bloco máximo de 20 registros:

```text
33 40 00 [step_hi] [step_lo] 28
   [40 bytes HIGH/LOW]
   [20 bytes EXTERNAL]
   [checksum]
```

Total: **67 bytes** incluindo checksum.

É importante distinguir **registro** de **passo de endereço**. Uma instrução pode ocupar 1–4
passos no programa, mas ainda ser contada uma vez pelo contador `+0x7A`. Portanto o próximo
endereço **não** é `startStep + N`; ele é o cursor real `+0x76` após os avanços de cada instrução.

## Confirmação dinâmica offline do construtor

O script `scripts/emulate_pc12_pg33_builder.py` executa o código original a partir de
`0x004B7958` dentro do Unicorn. Ele injeta um objeto sintético com HIGH/LOW e EXTERNAL, intercepta
`0x0046F5E6` antes de qualquer I/O e compara o buffer do PC12 com o modelo independente.

Foram validados quatro casos, todos com correspondência byte a byte:

```text
N=1, start=0000
PC12 = modelo = 33 07 00 00 00 02 00 10 01 B2

N=2, start=0050
PC12 = modelo = 33 0A 00 00 50 04 00 10 20 41 01 07 F5

N=3, start=0E34
PC12 = modelo = 33 0D 00 0E 34 06 02 11 21 40 00 39 04 07 0C B3

N=20, start=0F00
LEN=40, HIGH/LOW=28h bytes, EXTERNAL=20 bytes, total=67 bytes
PC12 = modelo
```

Resultado do emulador: **PASS**. Assim, cabeçalho, endereço, contagens, disposição dos dois
planos e checksum deixam de ser apenas reconstrução estática: estão também confirmados pela
execução offline do próprio código de máquina do PC12.

## Caminho de sucesso após o `0x33`

Depois da cadeia de tentativas, o caminho em `0x004B7C50` testa, nesta ordem lógica, as três
flags genéricas da rotina de comunicação:

```text
0x4FA8B7  timeout/falha de comunicação
0x4FA8B9  checksum inválido
0x4FA8B8  resposta marcada como erro
```

Quando as três estão zeradas, o PC12 entra em `0x004B7D22`. Nesse ponto ele reinicializa o
estado do bloco e usa **o cursor real de passos** como início do bloco seguinte.

Isso foi confirmado dinamicamente por `scripts/emulate_pc12_pg33_success_tail.py`, também no
Unicorn e sem executar qualquer rotina TX. Casos validados:

```text
cursor=0123h, program_size=4000
  -> próximo bloco
  +56=0, +5E=6, +62=0, +7A=0
  +76=0123h, +7E=0123h

cursor=0FA0h (=4000), program_size=4000
  -> conclusão

cursor=0FA1h (>4000), program_size=4000
  -> conclusão
```

Resultado: **PASS**. Portanto a transição de bloco está confirmada offline: o próximo quadro
parte de `+0x76`, não de `start + N`, e o fluxo termina quando `cursor >= tamanho do programa`.

### O que isso diz — e o que ainda não diz — sobre a resposta do PLC

O chamador do `0x33` aceita o caminho de sucesso quando as três flags genéricas acima estão
zeradas. Até aqui, **não foi identificado no chamador PG33 um teste adicional de conteúdo do
payload RX** antes da transição para o próximo bloco.

Isso não permite inventar um ACK. A rotina de comunicação `0x0046F5E6` pode derivar essas flags
a partir de bytes recebidos, e a forma exata da resposta física ao `0x33` ainda precisa ser
recuperada. Logo:

- condição de sucesso do **chamador**: confirmada;
- payload/ACK físico de sucesso do **PLC**: ainda desconhecido.

## Grau de confirmação atual

**Confirmado estaticamente + dinamicamente offline no código original do PC12:**

- comando de programa `0x33` no caminho `Write PLC Program`;
- geometria `33 [3*N+4] 00 step_hi step_lo [2*N] ... checksum`;
- planos HIGH/LOW e EXTERNAL;
- limite de 20 registros por quadro;
- checksum;
- endereço inicial do bloco;
- transição após sucesso usando o cursor real de passos;
- término quando `cursor >= tamanho do programa`.

**Ainda não confirmado fisicamente no TP02:**

- aceitação do `0x33` pelo PLC real;
- payload/ACK exato retornado pelo PLC;
- sequência completa de sessão/handshake de escrita observada no fio.

A bancada permanece READ-ONLY.

## Apoio independente do manual TP02

O manual TP02 descreve `RBP` (Read Boolean Program) e `WBP` (Write Boolean Program) como
operações sobre código de máquina e apresenta a representação de programa em componentes
**high byte, low byte e external byte**. Isso é compatível com os três bytes por registro que o
binário do PC12 organiza. O manual não documenta o byte binário `0x33`; essa associação vem da
engenharia reversa do executável PC12.

## O papel do `0x09`

O `09` continua confirmado estaticamente como primitiva de escrita de memória/registradores e
espelho estrutural do `0A`:

```text
leitura   0A [n=3] [end_hi] [end_lo] [qtd]              chk
escrita   09 [n=5] [end_hi] [end_lo] [qtd] [d0] [d1]    chk
```

Há também escrita de bloco maior em `0x4BC811`, que monta `09 11 …`, e a rotina `0x4B7DCE`
trata escrita de registros `Vxxxx / Dxxxx / WCxxx / FILE Register` com `09`.

As duas famílias devem permanecer separadas:

- `09`: dados/registradores;
- `33`: blocos do programa no caminho `Write PLC Program`.

## Comando destrutivo: `0F 00 F0`

`0x46F0F0` monta `0F 00 F0` e o transmite como Clear All Memory. Continua bloqueado no PG Lab
e não existe motivo para expô-lo no OpenLadder.

## Implementação segura no repositório

`Tp02Pg33DryRunFrame` somente constrói em memória o quadro confirmado offline e valida geometria
e checksum. Seu limite é **20 registros por quadro**. A classe não tenta calcular o próximo
endereço a partir do número de registros, pois instruções podem consumir 1–4 passos.

Os emuladores `emulate_pc12_pg33_builder.py` e `emulate_pc12_pg33_success_tail.py`:

- não abrem COM;
- não usam `SerialPort`;
- não executam `WriteFile` do host;
- interceptam/evitam a rotina TX;
- não são chamados pelo PG Lab;
- não implementam Clear All Memory, RUN ou STOP remoto.

## O que ainda falta fechar

1. Recuperar **offline** a resposta esperada ao `0x33` dentro da rotina de comunicação, separando
   claramente bytes RX de flags derivadas.
2. Emular o fluxo mais amplo do `Write PLC Program`, desde a coleta/codificação de instruções até
   dois ou mais blocos consecutivos, para observar a orquestração completa sem I/O.
3. Correlacionar instruções de 1–4 passos com a divisão em blocos de 20 registros e validar casos
   que cruzem exatamente o limite do quadro.
4. Manter qualquer captura física de escrita como decisão separada e explícita. Nenhum TX de
   escrita deve ser habilitado automaticamente como consequência desta pesquisa.

## Como reproduzir a análise offline

```bash
python3 scripts/analyze_pc12_writeprog.py src/OpenLadderStudio.Desktop/pc12.exe
python3 scripts/analyze_pc12_write_tx.py src/OpenLadderStudio.Desktop/pc12.exe
python3 scripts/analyze_pc12_write_fields.py src/OpenLadderStudio.Desktop/pc12.exe
python3 scripts/emulate_pc12_pg33_builder.py src/OpenLadderStudio.Desktop/pc12.exe
python3 scripts/emulate_pc12_pg33_success_tail.py src/OpenLadderStudio.Desktop/pc12.exe
```

O workflow `Analyze PC12 protocol offline` executa essas análises sem hardware, persiste os
relatórios em `docs/data/` e mantém os artefatos de CI para auditoria.

## Segurança

Mapear e emular não é habilitar escrita. O `0x33` está **confirmado no caminho de escrita do
PC12 e confirmado dinamicamente offline quanto à montagem do quadro**, mas **não está fisicamente
confirmado no TP02**. A bancada continua READ-ONLY até uma decisão explícita em contrário.
