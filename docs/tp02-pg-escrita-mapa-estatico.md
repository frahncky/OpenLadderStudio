# TP02 PG — mapa estático dos comandos de escrita do PC12

Data: 2026-09-10/11 · Método: análise estática e emulação exclusivamente offline do `pc12.exe` · **Nenhum byte transmitido a um PLC**

## Aviso de escopo

Este documento **apenas mapeia** o que o `pc12.exe` monta para escrever no TP02. Nada aqui é
transmitido ou habilitado como download no OpenLadder Studio. A política do projeto continua
estritamente READ-ONLY na bancada: `0F 00 F0` bloqueado, nenhuma escrita, apagamento, firmware
ou RUN/STOP remoto.

O objetivo é saber o que a escrita **seria**, sem executá-la. O código associado ao `0x33` é
propositalmente **dry-run** e não possui caminho para porta serial.

## Inventário resumido

| CMD | classe | papel atual |
|---:|---|---|
| `0xF0` | leitura/preflight | semântica exata ainda desconhecida |
| `0x38` | leitura | metadado estrutural do programa |
| `0x34` | leitura | bloco do programa |
| `0x0A` | leitura | memória/registradores |
| `0x14` | leitura | consulta |
| `0x09` | **escrita** | memória/registradores; espelho estrutural do `0A` |
| `0x33` | **escrita de programa — reconstrução estática forte** | quadro dedicado no caminho `Write PLC Program` |
| `0x0F` | **escrita destrutiva** | `0F 00 F0` = Clear All Memory |
| `0x01`–`0x04`, `0x11`, `0x12`, `0x13`, `0x35`, `0x37` | — | controle/sessão, ainda não classificados |

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
(`0x4FA8B7..0x4FA8B9`) e repete a mesma chamada quando alguma flag está ativa. Portanto, a
leitura estática mais consistente é uma cadeia de retentativas do mesmo quadro já montado.

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

A análise dos campos internos fechou a geometria que antes era apenas inferida.

### Inicialização de cada bloco

Em `0x004B7D22` o PC12 inicia um novo bloco com:

```text
+0x7A = 0        # contador de registros do bloco
+0x5E = 6        # próximo índice no TX, logo após o cabeçalho
+0x62 = 0        # quantidade de bytes EXTERNAL acumulados
+0x56 = 0        # quantidade de bytes HIGH/LOW acumulados
+0x7E = +0x76    # endereço de passo inicial do bloco
```

O cursor `+0x76` é o endereço de programa e avança em **1, 2, 3 ou 4 passos**, conforme a classe
da instrução. Isso é independente do contador de registros do bloco.

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

Outros ramos repetem a mesma organização. Portanto, para `N` **registros de código de máquina**:

```text
+0x56 = 2*N                 # HIGH/LOW já gravados em TX[6..]
+0x62 = N                   # EXTERNAL acumulados em +0xE0
+0x5E = 6 + 2*N             # primeiro byte após o plano HIGH/LOW
```

No construtor final, os `N` bytes EXTERNAL são copiados de `+0xE0` para o TX a partir de
`+0x5E`. Isso fecha estaticamente o corpo como:

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
e os dois últimos para `+0x6E`. O construtor copia esses bytes para `TX[3]` e `TX[4]`.

Assim, há forte reconstrução estática de:

```text
TX[3] = step_hi
TX[4] = step_lo
```

### Limite do bloco: 20 registros, não 80

Uma descoberta importante corrige a primeira hipótese. Em `0x004B7869` o PC12 compara o
contador `+0x7A` com `0x14`; ao atingir 20 registros ele encerra a coleta e segue para a montagem
do `0x33`.

Portanto, o limite observado no caminho de escrita é **20 registros por quadro**. Ele não deve
ser confundido com os 80 passos por página do comando de leitura `0x34`.

O quadro reconstruído é:

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
passos no programa, mas ainda ser contada uma vez pelo contador `+0x7A` neste caminho. Por isso,
não é correto inferir o próximo endereço como `startStep + N`; o avanço deve usar o tamanho real
de cada instrução.

## Grau de confirmação

A estrutura `0x33`, o cabeçalho, a separação HIGH/LOW + EXTERNAL, o limite de 20 registros, o
endereço inicial, o checksum e o caminho até a rotina TX estão **reconstruídos estaticamente no
`pc12.exe`**. Isso é mais forte do que simples correspondência de bytes, mas ainda **não é
confirmação física de bancada** do comando.

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

Com o isolamento do `0x33`, as duas famílias devem permanecer separadas:

- `09`: dados/registradores;
- `33`: forte candidato estático para blocos do programa.

## Comando destrutivo: `0F 00 F0`

`0x46F0F0` monta `0F 00 F0` e o transmite como Clear All Memory. Continua bloqueado no PG Lab
e não existe motivo para expô-lo no OpenLadder.

## Implementação segura no repositório

`Tp02Pg33DryRunFrame` somente constrói em memória o quadro reconstruído e valida geometria e
checksum. Após o rastreio dos campos, seu limite foi corrigido para **20 registros por quadro**.
A classe não tenta calcular o próximo endereço a partir do número de registros, pois instruções
podem consumir 1–4 passos.

Ela:

- não abre COM;
- não conhece `SerialPort`;
- não é chamada pelo PG Lab;
- não é chamada por qualquer botão de download;
- não contém Clear All Memory, RUN ou STOP remoto.

## O que ainda falta fechar

1. Emular offline o fluxo completo do `Write PLC Program` com Unicorn, interceptando a rotina TX
   e registrando os quadros `0x33` sem qualquer I/O do host.
2. Determinar a resposta que o PC12 espera após um `0x33` bem-sucedido e confirmar como as flags
   `0x4FA8B7..0x4FA8B9` são preenchidas pela rotina de comunicação.
3. Confirmar dinamicamente, ainda offline, a passagem de um bloco ao seguinte e o avanço do
   endereço por instruções de 1–4 passos.
4. Só depois disso decidir se vale fazer uma captura física controlada. Nenhum TX de escrita deve
   ser habilitado automaticamente como consequência desta pesquisa.

## Como reproduzir a análise offline

```bash
python3 scripts/analyze_pc12_writeprog.py src/OpenLadderStudio.Desktop/pc12.exe
python3 scripts/analyze_pc12_write_tx.py src/OpenLadderStudio.Desktop/pc12.exe
python3 scripts/analyze_pc12_write_fields.py src/OpenLadderStudio.Desktop/pc12.exe
```

O workflow `Analyze PC12 protocol offline` executa essas análises sem hardware, persiste os
relatórios em `docs/data/` e mantém os artefatos de CI para auditoria.

## Segurança

Mapear não é habilitar. O `0x33` foi promovido de “comando não classificado” para
“reconstrução estática forte do caminho de escrita de programa”, mas **não** para “comando
fisicamente confirmado”. A bancada continua READ-ONLY até uma decisão explícita em contrário.
