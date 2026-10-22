# TP02 PG — confirmação OFFLINE do F-13w no caminho PG33

Data: 2026-09-11

## Escopo e segurança

Esta validação executa código original do `pc12.exe` dentro do Unicorn. Não abre porta COM, não chama a rotina de TX e não acessa um PLC. Portanto, confirma a geração do quadro no lado do PC12, mas **não confirma aceitação física do comando `0x33` pelo TP02**.

A bancada e o PG Lab continuam READ-ONLY. `0F 00 F0` (Clear All Memory) permanece bloqueado.

## Programa sintético

O registro interno de 48 bytes do PC12 foi preenchido com quatro slots de 12 bytes:

```text
slot 0: 513      -> internal do F-13w ADD
slot 1: D0002
slot 2: D0001
slot 3: 00010
```

Equivalente lógico:

```text
F-13w ADD D0002,D0001,10
```

A emulação começa em `0x004B758D`, imediatamente antes da chamada ao parser `0x004BA69E`, e para em `0x004B7869`, antes da decisão de montar/enviar o quadro.

Para evitar que o PE recém-mapeado interprete o programa sintético como terminado, foram inicializadas apenas as guardas estruturais:

```text
program_size      = 4000
last_program_step = 3999
```

## Resultado

Workflow:

```text
Analyze PC12 PG33 encoder fields
run #12 / 34559256817
```

Resultado: **PASS**.

O coletor original do PC12 produziu:

```text
HIGH/LOW:
0D 77 | F0 01 | F0 00 | 80 0A

EXTERNAL:
00 | 00 | 00 | 00

StepSpan = 4
cursor   = 4
logical instructions = 1
mode = 2
```

O parser identificou `internal=513`, correspondente ao `F-13w`.

## Correlação com a captura física PG34

A captura física anterior do mesmo ADD, lida do TP02 pelo comando `0x34`, mostrou exatamente:

```text
0D 77    F-13w ADD
F0 01    D0002
F0 00    D0001
80 0A    constante 10
```

Logo, para esse caso, o caminho de escrita do PC12 e a representação fisicamente lida do PLC coincidem **byte a byte nos quatro pares HIGH/LOW**.

Isso é uma evidência forte de que o PG33 transporta a mesma representação expandida de palavras de máquina usada pelo programa armazenado. Ainda assim, a equivalência física de `EXTERNAL` e a aceitação do quadro `0x33` só podem ser declaradas após observação/validação física separada.

## Estado após esta validação

Confirmado OFFLINE no PC12:

```text
F-13w internal = 513
primeira palavra = 0D 77 / EXTERNAL 00
D0002 = F0 01 / EXTERNAL 00
D0001 = F0 00 / EXTERNAL 00
K10   = 80 0A / EXTERNAL 00
StepSpan = 4
uma instrução lógica gera quatro palavras de máquina
```

Ainda pendente:

```text
ACK físico do 0x33
aceitação física do 0x33 pelo TP02
sequência completa de sessão de escrita no fio
paginação física do 0x34 além de 80 passos
```

Arquivo de evidência:

```text
docs/data/pc12-pg33-f13w-full-emulation.txt
```
