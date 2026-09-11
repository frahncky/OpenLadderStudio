# TP02 PG — mapa offline dos comandos de escrita do PC12

Data: 2026-09-10/11 · Método: análise estática + emulação Unicorn exclusivamente offline do `pc12.exe` · **Nenhum byte transmitido a um PLC**

## Aviso de escopo

Este documento descreve o que o `pc12.exe` monta no caminho `Write PLC Program`. Nada aqui habilita download no OpenLadder Studio. A bancada continua estritamente READ-ONLY: `0F 00 F0` permanece bloqueado e não há escrita, apagamento, firmware ou RUN/STOP remoto.

“Confirmado dinamicamente offline” significa que trechos do código de máquina original do PC12 foram executados no Unicorn com o ponto de TX interceptado. Isso **não** equivale a confirmação física do comando pelo TP02.

## Inventário resumido

| CMD | classe | papel atual |
|---:|---|---|
| `0xF0` | leitura/preflight | semântica exata ainda desconhecida |
| `0x38` | leitura | metadado estrutural do programa |
| `0x34` | leitura | páginas do programa |
| `0x0A` | leitura | memória/registradores |
| `0x09` | escrita | memória/registradores; família distinta da escrita de programa |
| `0x33` | escrita de programa | quadro dedicado encontrado em `Write PLC Program` |
| `0x0F` | escrita destrutiva | `0F 00 F0` = Clear All Memory |

## Comando dedicado `0x33`

A string `Write PLC Program...` leva ao caminho que monta o quadro em `0x004B7958` e, depois, chama a rotina de comunicação `0x0046F5E6`. O primeiro byte do buffer TX é fixado em `0x33`.

O construtor final usa estes campos do objeto:

```text
+0x56 = quantidade de bytes HIGH/LOW acumulados
+0x5E = próximo índice do plano HIGH/LOW no TX
+0x62 = quantidade de bytes EXTERNAL acumulados
+0x6A = byte alto do endereço inicial
+0x6E = byte baixo do endereço inicial
+0x76 = cursor real de passos do programa
+0x7A = contador de INSTRUÇÕES LÓGICAS do bloco
+0x7E = passo inicial do bloco
```

### Correção importante: instrução lógica != palavra de máquina

A análise inicial confundia `+0x7A` com quantidade de palavras/registros transmitidos. O rastreamento completo do helper `0x004BCA65` corrigiu essa interpretação.

O PC12 trabalha com duas unidades distintas:

```text
instrução lógica
    -> ocupa StepSpan = 1..4 passos
    -> produz 1..4 palavras de máquina HIGH/LOW/EXTERNAL

palavra de máquina
    -> 1 HIGH + 1 LOW + 1 EXTERNAL
    -> é a unidade efetivamente armazenada no corpo do PG33
```

A primeira palavra de uma instrução é emitida pelo chamador. Quando `StepSpan > 1`, o helper `0x004BCA65` é chamado uma vez para cada passo adicional. No caminho genérico aparecem chamadas sucessivas antes dos testes `StepSpan <= 2`, `<= 3` e antes do switch final.

No final comum do helper, o PC12 executa:

```text
TX[+0x5E] = HIGH
+0x5E++
TX[+0x5E] = LOW
+0x5E++
(+0xE0)[+0x62] = EXTERNAL
+0x62++
+0x56 += 2
```

O helper **não incrementa `+0x7A`**. Depois de toda a expansão da instrução, o caminho comum avança:

```text
+0x76 += StepSpan
+0x7A += 1
```

Portanto `+0x7A` conta instruções lógicas, enquanto `+0x56/+0x62` contam as palavras efetivamente destinadas ao quadro.

## Limite de bloco corrigido

Em `0x004B7869` existe:

```text
cmp +0x7A, 0x14
```

Logo, o limite é **20 instruções lógicas por bloco**.

Como cada instrução pode ocupar até quatro passos/palavras, um bloco pode conter:

```text
1..20 instruções lógicas
1..80 palavras de máquina
```

Esse resultado substitui a interpretação antiga “20 palavras por quadro”.

## Geometria do quadro `0x33`

Defina `W` como a quantidade total de **palavras de máquina já expandidas** no bloco.

O construtor em `0x004B7958` produz:

```text
TX[0] = 33
TX[1] = 3*W + 4
TX[2] = 00
TX[3] = step_hi
TX[4] = step_lo
TX[5] = 2*W

corpo:
[2*W bytes HIGH/LOW]
[W bytes EXTERNAL]

TX[last] = FF - soma(TX[0..last-1])
```

ou, de forma compacta:

```text
33 [3*W+4] 00 [step_hi] [step_lo] [2*W]
   [2*W bytes HIGH/LOW]
   [W bytes EXTERNAL]
   [checksum]
```

com:

```text
1 <= W <= 80
sum(quadro) mod 256 = FF
```

### Quadro máximo reconstruído

Vinte instruções de quatro passos podem gerar `W=80`:

```text
TX[1] = 3*80 + 4 = 244 = F4h
TX[5] = 2*80     = 160 = A0h
HIGH/LOW = 160 bytes
EXTERNAL = 80 bytes
quadro total = 247 bytes incluindo checksum
```

Portanto o maior quadro reconstruído é estruturalmente:

```text
33 F4 00 [step_hi] [step_lo] A0
   [160 bytes HIGH/LOW]
   [80 bytes EXTERNAL]
   [checksum]
```

## Endereço inicial e avanço

No início de um novo bloco, `+0x7E` recebe o cursor `+0x76`. Esse valor é convertido em `step_hi/step_lo` para `TX[3..4]`.

O cursor avança pelo número real de passos da instrução, isto é, 1, 2, 3 ou 4. Após sucesso do quadro anterior, o próximo bloco inicia no cursor resultante, não em `start + 20` nem em `start + W` calculado externamente.

A emulação offline da cauda de sucesso já confirmou que:

```text
cursor < program_size  -> inicia novo bloco nesse cursor
cursor >= program_size -> conclui o fluxo
```

## O que a emulação do construtor já confirma

`scripts/emulate_pc12_pg33_builder.py` executa o construtor original `0x004B7958` no Unicorn e intercepta a chamada TX. Casos sintéticos de `W=1`, `W=2`, `W=3` e `W=20` coincidiram byte a byte com o modelo independente.

Esses casos confirmam a fórmula do quadro para uma quantidade fornecida de palavras. O caso `W=20` **não prova um limite de 20 palavras**; o limite real de bloco é definido por `+0x7A`, agora classificado como contador de instruções lógicas.

O novo relatório:

```text
docs/data/pc12-pg33-encoder-fields-analysis.txt
```

rastreia o chamador, `StepSpan`, o helper `0x004BCA65`, as escritas adicionais em TX e a contabilidade de HIGH/LOW/EXTERNAL.

## Relação com as instruções multistep já conhecidas

A correção resolve a aparente incompatibilidade com o código de máquina observado na leitura `0x34`:

```text
F-23 SET   -> 2 palavras/passos
F-24 RST   -> 2 palavras/passos
F-13w ADD  -> 4 palavras/passos
TMR+preset -> 2 palavras/passos no trecho correspondente
CNT+preset -> múltiplos passos conforme a estrutura do programa
```

O PG33 não precisa comprimir uma função multistep em uma única palavra. O caminho de escrita expande a instrução e acrescenta as palavras seguintes por meio do helper.

Isso não significa que o byte `BRAW` da Região B do `0x34` seja o mesmo que `EXTERNAL` do PG33. São campos observados em caminhos diferentes e devem continuar separados epistemicamente.

## Resposta/ACK do PLC

Depois das tentativas de envio, o chamador testa as flags genéricas:

```text
0x4FA8B7  timeout/falha de comunicação
0x4FA8B9  checksum inválido
0x4FA8B8  resposta marcada como erro
```

O validador RX emulado offline confirmou que essas flags decorrem de regras genéricas de checksum/status. Um quadro como `00 00 FF` satisfazer o parser **não prova** que ele seja o ACK físico do `0x33`.

Permanece desconhecido:

- payload/ACK real retornado pelo TP02 ao `0x33`;
- sequência completa de sessão de escrita observada no fio;
- aceitação física do `0x33` no controlador.

## Implementação segura no repositório

O dry-run foi separado em duas camadas:

```text
Tp02Pg33DryRunFrame
  -> recebe palavras de máquina já expandidas
  -> aceita 1..80 palavras
  -> monta apenas a geometria do quadro

Tp02Pg33DryRunProgram
  -> recebe instruções lógicas de 1..4 palavras
  -> limita 20 instruções por bloco
  -> concatena as palavras expandidas
  -> avança o cursor pelo StepSpan real
```

Nenhuma dessas classes conhece `SerialPort` ou transmite bytes.

## `0x09` continua separado

O `0x09` permanece classificado como primitiva de escrita de memória/registradores. A associação principal para `Write PLC Program` é o quadro dedicado `0x33`; as duas famílias não devem ser misturadas.

## Comando destrutivo

`0F 00 F0` é Clear All Memory. Continua bloqueado no PG Lab e não foi utilizado em nenhuma análise ou emulação desta etapa.

## Próximas validações offline

1. Emular uma instrução real de 2 passos pelo caminho completo e capturar as duas palavras que chegam ao construtor.
2. Repetir para uma função de 4 passos, preferencialmente `F-13w ADD`, e comparar com o vetor já confirmado fisicamente pelo `0x34`.
3. Emular um bloco com 20 instruções de quatro passos e confirmar no construtor real `W=80`, `LEN=F4`, `TX[5]=A0`.
4. Emular 21 instruções lógicas para verificar a divisão `20 + 1` e o endereço real do segundo quadro.
5. Somente depois considerar qualquer estudo físico de escrita, mediante decisão explícita separada.

## Estado de segurança

Mapear e emular não é habilitar escrita. O `0x33` está confirmado no caminho de escrita do PC12 e sua montagem está confirmada dinamicamente offline para os casos já ensaiados, mas **não está fisicamente confirmado no TP02**. A bancada continua READ-ONLY.
