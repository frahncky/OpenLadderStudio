# TP02 PG — mapa estático dos comandos de escrita do PC12

Data: 2026-09-10/11 · Método: análise estática e emulação exclusivamente offline do `pc12.exe` · **Nenhum byte transmitido a um PLC**

## Aviso de escopo

Este documento **apenas mapeia** o que o `pc12.exe` monta para escrever no TP02. Nada aqui é
transmitido ou habilitado como download no OpenLadder Studio. A política do projeto continua
estritamente READ-ONLY na bancada: `0F 00 F0` bloqueado, nenhuma escrita, apagamento, firmware
ou RUN/STOP remoto.

O objetivo é uma decisão informada: saber o que a escrita **seria**, sem executá-la. O código
novo associado ao `0x33` é propositalmente **dry-run** e não possui caminho para porta serial.

## Inventário resumido

| CMD | classe | papel atual |
|---:|---|---|
| `0xF0` | leitura/preflight | semântica exata ainda desconhecida |
| `0x38` | leitura | metadado estrutural do programa |
| `0x34` | leitura | bloco do programa |
| `0x0A` | leitura | memória/registradores |
| `0x14` | leitura | consulta |
| `0x09` | **escrita** | memória/registradores; espelho estrutural do `0A` |
| `0x33` | **escrita de programa — forte evidência estática** | quadro dedicado no caminho `Write PLC Program` |
| `0x0F` | **escrita destrutiva** | `0F 00 F0` = Clear All Memory |
| `0x01`–`0x04`, `0x11`, `0x12`, `0x13`, `0x35`, `0x37` | — | controle/sessão, ainda não classificados |

## Descoberta principal: o download de programa usa um quadro dedicado `0x33`

A hipótese antiga de que o download completo provavelmente usaria a primitiva `0x09` não se
sustenta mais como hipótese principal. A análise do caminho exato `Write PLC Program...` isolou
um construtor dedicado que grava `0x33` no primeiro byte do buffer TX e, imediatamente depois,
chega à rotina de transmissão do PC12 (`0x0046F5E6`).

### Correlação direta com o menu `Write PLC Program...`

A string `Write PLC Program...` tem um único xref no código, em `0x004B6B08`, dentro da região
que leva ao construtor. No mesmo caminho, o PC12 monta o quadro em `0x004B7958` e alcança
chamadas diretas da rotina TX a partir de `0x004B7A1A`.

Foram localizados 15 `CALL rel32` para `0x0046F5E6` entre `0x004B7A1A` e `0x004B7C4A`.
Esses 15 pontos **não significam 15 quadros diferentes**: entre eles o PC12 testa as flags de
timeout, resposta de erro e checksum (`0x4FA8B7..0x4FA8B9`). A estrutura é compatível com
retentativas do mesmo envio conforme o resultado da comunicação. A sequência dinâmica ainda
deve ser fechada por emulação offline do fluxo completo.

### Estrutura do quadro `0x33`

O construtor em `0x004B7958` mostra:

```text
TX[0] = 33
TX[1] = função de (+0x56):  (+0x56) + ((+0x56)/2) + 4
TX[2] = 00
TX[3] = byte em +0x6A
TX[4] = byte em +0x6E
TX[5] = byte em +0x56
TX[6..] = buffer interno em +0xE0
TX[last] = FF - soma(TX[0..last-1])
TX_LEN = last + 1
```

A interpretação geométrica mais consistente com o `0x34` é a seguinte. Para `N` passos:

```text
+0x56 = 2*N                  # bytes HIGH/LOW (Região A)
TX[1] = 3*N + 4              # bytes após LEN e antes do checksum
TX[5] = 2*N
corpo  = 2*N bytes HIGH/LOW + N bytes externos = 3*N bytes
```

Portanto, o quadro candidato fica:

```text
33 [3*N+4] 00 [step_hi] [step_lo] [2*N]
   [2*N bytes HIGH/LOW]
   [N bytes EXTERNAL]
   [checksum]
```

Para um bloco de 80 passos, a geometria candidata é:

```text
33 F4 00 [step_hi] [step_lo] A0
   [160 bytes HIGH/LOW]
   [80 bytes EXTERNAL]
   [checksum]
```

Total: 247 bytes incluindo checksum.

Essa reconstrução é **forte evidência estática**, não confirmação física. O que está provado no
binário é o comando `0x33`, os campos acima, a cópia do corpo, o cálculo do checksum e o caminho
até TX. O significado de cada campo é inferido pela geometria e pela correspondência com o
formato de programa já recuperado no `0x34`.

### Apoio independente do manual TP02

O manual TP02 descreve `RBP` (Read Boolean Program) e `WBP` (Write Boolean Program) como
operações sobre código de máquina e apresenta cada passo do programa com os três componentes
**high byte, low byte e external byte**. Isso é compatível com os três bytes por passo que a
análise binária do PC12 mostra. O manual não documenta o número binário `0x33`; essa associação
vem do executável PC12.

## O papel do `0x09`

O `09` continua confirmado como primitiva de escrita de memória/registradores e espelho
estrutural do `0A`:

```text
leitura   0A [n=3] [end_hi] [end_lo] [qtd]              chk
escrita   09 [n=5] [end_hi] [end_lo] [qtd] [d0] [d1]    chk
```

- **byte 1 (`n`)** é o número de bytes que seguem antes do checksum;
- **`qtd`** é o número de bytes de dados;
- **checksum** fecha a soma do quadro em `0xFF`.

Há também escrita de bloco maior — `0x4BC811` monta `09 11 …`, com `n = 0x11`. A rotina
`0x4B7DCE` trata escrita de registros `Vxxxx / Dxxxx / WCxxx / FILE Register` com `09`.

Com o isolamento do `0x33`, `09` e `33` devem ser tratados como famílias distintas:
**`09` para dados/registradores; `33` como candidato forte para blocos do programa**.

## Comando destrutivo: `0F 00 F0`

`0x46F0F0` monta `0F 00 F0` e o transmite como Clear All Memory. Continua bloqueado no PG Lab
e não existe motivo para expô-lo no OpenLadder.

## Implementação segura no repositório

Foi adicionada `Tp02Pg33DryRunFrame`, que somente constrói em memória o quadro candidato acima,
valida geometria e checksum e possui autoteste. Ela:

- não abre COM;
- não conhece `SerialPort`;
- não é chamada pelo PG Lab;
- não é chamada por qualquer botão de download;
- não contém Clear All Memory, RUN ou STOP remoto.

O objetivo é permitir comparar a saída do compilador com o formato reconstruído antes de qualquer
decisão futura sobre escrita física.

## O que ainda falta fechar

1. Confirmar por emulação do próprio PC12 quais valores reais entram em `+0x56`, `+0x5E`,
   `+0x62`, `+0x6A` e `+0x6E` durante um `Write PLC Program` completo.
2. Registrar a ordem dinâmica de preparação/handshake e dos quadros `0x33`, interceptando a
   rotina TX **dentro do Unicorn**, sem qualquer I/O do host.
3. Determinar a resposta esperada ao `0x33` e a regra exata de avanço do endereço.
4. Só depois disso decidir se vale fazer uma captura física controlada. Nenhum TX de escrita deve
   ser habilitado como consequência automática desta pesquisa.

## Como reproduzir a análise offline

```bash
python3 scripts/analyze_pc12_writeprog.py src/OpenLadderStudio.Desktop/pc12.exe
python3 scripts/analyze_pc12_write_tx.py src/OpenLadderStudio.Desktop/pc12.exe
```

O workflow `Analyze PC12 protocol offline` executa essas análises sem hardware, persiste os
relatórios em `docs/data/` e mantém os artefatos de CI para auditoria.

## Segurança

Mapear não é habilitar. O `0x33` foi promovido de “comando não classificado” para
“forte candidato estático de escrita de programa”, mas **não** para “comando fisicamente
confirmado”. A bancada continua READ-ONLY até uma decisão explícita em contrário.
