# TP02 — engenharia reversa do código de máquina do PC12 2.1

> Status: **experimental, reproduzível e ainda sem autorização para escrita real no PLC**.
>
> Esta pesquisa separa três níveis de evidência: documentação oficial do Host Protocol, análise estática do executável original `pc12.exe` e futura validação em hardware por leitura RBP. A presença de um encoder no código não habilita download de programa.

## Resultado principal

A análise estática do PC12 2.1 localizou a rotina que converte a representação Boolean/IL usada pelo editor em palavras de programa TP02. Cada passo ocupa **3 bytes**:

```text
HIGH LOW EXT
```

Na comunicação RBP/WBP esses três bytes aparecem como seis caracteres hexadecimais por passo.

O compilador experimental está em:

```text
src/OpenLadderStudio.Core/Tp02TargetCompiler.cs
```

O inventário completo das funções extraídas do PC12 está em:

```text
docs/data/tp02_function_map_normalized.csv
```

## Instruções Boolean básicas

Bases reconstruídas para o byte LOW:

| Instrução | Base LOW |
|---|---:|
| STR | `0x10` |
| STR NOT | `0x18` |
| AND | `0x20` |
| AND NOT | `0x28` |
| OR | `0x30` |
| OR NOT | `0x38` |
| OUT | `0x40` |

Palavras fixas:

| Instrução | Palavra |
|---|---|
| NOP | `00 00 00` |
| AND STR | `00 01 00` |
| OR STR | `00 02 00` |

### Endereçamento X/Y/C das instruções básicas

Bases do byte HIGH:

```text
X = 0x00
Y = 0x20
C = 0x40
```

Para um dispositivo de número `n`:

```text
k     = n - 1
bit   = k & 7
grupo = k >> 3

HIGH = BASE_DISPOSITIVO | (grupo & 0x1F)
LOW  = BASE_INSTRUCAO   | bit
EXT  = (grupo >> 1) & 0xF0
```

Vetores reconstruídos:

```text
STR     X0001 -> 00 10 00
STR NOT X0001 -> 00 18 00
AND     X0001 -> 00 20 00
AND NOT X0002 -> 00 29 00
OR      X0001 -> 00 30 00
OR NOT  X0001 -> 00 38 00
OUT     Y0001 -> 20 40 00
```

Exemplo de rung em série:

```text
STR X0001
AND X0002
OUT Y0001
```

vira:

```text
00 10 00
00 21 00
20 40 00
```

Payload contínuo:

```text
001000002100204000
```

## TMR e CNT

A primeira palavra de TMR/CNT codifica o número da instrução. O preset é outra palavra.

```text
index = n - 1

TMR:
HIGH = index & 0x7F
LOW  = 0x60 | ((index >> 7) & 0x07)
EXT  = 0x00

CNT:
HIGH = index & 0x7F
LOW  = 0x68 | ((index >> 7) & 0x07)
EXT  = 0x00
```

Exemplos:

```text
TMR 0001 -> 00 60 00
CNT 0001 -> 00 68 00
```

## Literais de 16 bits

Para um literal `N` entre 0 e 65535:

```text
H = (N >> 8) & 0xFF
L = N & 0xFF

HIGH = 0x80 | ((H & 0x0F) << 1) | ((L >> 7) & 1)
LOW  = L & 0x7F
EXT  = H & 0xF0
```

Exemplo:

```text
1000 decimal = 0x03E8
1000 -> 87 68 00
```

## Operandos normais das funções F-xx

Bases reconstruídas no encoder geral do PC12:

| Dispositivo | Base |
|---|---:|
| Y | `0xC0` |
| C | `0xC8` |
| X | `0xD0` |
| WY | `0xD8` |
| WX | `0xE0` |
| V | `0xE8` |
| D | `0xF0` |
| WC | `0xF8` |

Para X/Y/C, o encoder normal trabalha com grupos de 8. Para registradores word, usa o índice base 1 diretamente. A implementação está em `EncodeFunctionOperand`.

## Modo especial de bit X/Y/C em F-xx

Algumas funções operam diretamente sobre um relé individual. O PC12 usa uma forma diferente da anterior.

Bases:

```text
X = 0xC0
Y = 0xC8
C = 0xD0
```

Fórmula:

```text
index = n - 1
bit   = index & 0x07
grupo = index >> 3

HIGH = BASE | bit
LOW  = 0x80 | (grupo & 0x7F)
EXT  = ((grupo >> 7) & 0x0F) << 4
```

Exemplos:

```text
X0001 -> C0 80 00
Y0001 -> C8 80 00
C0001 -> D0 80 00
Y0009 -> C8 81 00
```

Funções em que o PC12 ativa esse modo:

| Função | Operando 1 | Operando 2 | Operando 3 |
|---|---|---|---|
| F-23 SET | bit | — | — |
| F-24 RST | bit | — | — |
| F-49 IORB | bit | — | — |
| F-50w STMR | bit | normal | normal |
| F-51 ZRST | bit | normal | — |
| F-52 TENK | normal | bit | bit |
| F-53 HEXK | normal | bit | bit |
| F-60 TSET | bit | normal | normal |
| F-61 TRST | bit | normal | normal |

## Funções F-xx

Foram normalizadas **91 formas internas** do PC12. O CSV registra:

- ID interno do PC12;
- nome F-xx;
- mnemônico;
- prefixo HIGH/LOW/EXT;
- quantidade de passos;
- endereço da rotina do encoder no executável analisado;
- endereço da rotina de interface correspondente;
- modos dos operandos;
- status da evidência.

Exemplos:

```text
F-00  End  -> 00 70 00
F-13  ADD  -> 0D 73 00
F-13w ADD  -> 0D 77 00
F-13d ADD  -> 0D F3 00
F-50w STMR -> 32 77 00
```

As variantes `w` e `d` não são apenas nomes de interface: elas chegam a códigos internos diferentes no PC12 e convergem para prefixos de máquina próprios.

### F-33 / TEXT

O PC12 possui duas formas de `F-33` e duas formas de `F-33w`. Por isso o compilador não deve escolher silenciosamente uma variante.

Formas normalizadas:

```text
F-33:2
F-33:3
F-33w:2
F-33w:3
```

O método `GetFunction` recusa o alias ambíguo `F-33`/`F-33w`.

### Labels e desvios

`F-42 LBxxx` embute o label na própria palavra.

Para `LB001`:

```text
2A 78 00
```

`F-43 JMP` e `F-44 CALL` usam duas palavras. Para label 1:

```text
F-43 JMP  -> 2B 71 00 / 80 00 00
F-44 CALL -> 2C 71 00 / 80 00 00
```

## Host Protocol e dry-run WBP

O checksum continua sendo calculado sobre os caracteres ASCII entre `:` e o checksum:

```text
CHK = (-soma_dos_bytes_ASCII) & 0xFF
```

Vetores oficiais usados como teste de regressão:

```text
:01?5SCSY00011F7<CR>
:01?5RBP00000324<CR>
```

Exemplo gerado pelo compilador para três passos:

```text
STR X0001
AND X0002
OUT Y0001
```

Quadro WBP montado em dry-run:

```text
:01?5WBP000003001000002100204000B5<CR>
```

**Nenhuma rotina do compilador transmite esse quadro.** Ela apenas devolve a string para inspeção/teste.

## Regras de segurança para integração

1. `Tp02TargetCompiler` fica no núcleo e não conhece `SerialPort`.
2. O driver TP02 continua responsável exclusivamente por transporte e monitoramento.
3. WBP real permanece desabilitado até comparação do encoder com programas conhecidos lidos por RBP no TP02 físico.
4. A promoção de um opcode para uso automático deve manter rastreabilidade entre PC12, dump RBP e teste de regressão.
5. Qualquer discrepância de byte EXT deve bloquear gravação.

## Próxima validação de bancada

O caminho de menor risco é:

```text
programa mínimo no PC12 original
        ↓
transferência oficial PC12 -> TP02
        ↓
leitura RBP pelo OpenLadder
        ↓
comparação byte a byte com Tp02TargetCompiler
        ↓
somente após coincidência: considerar WBP real
```

Casos mínimos recomendados:

```text
STR X0001
STR X0002
STR NOT X0001
AND X0001
AND NOT X0001
OR X0001
OR NOT X0001
OUT Y0001
OUT C0001
TMR 0001 + preset
CNT 0001 + preset
F-23 SET Y0001
F-50w STMR Y0001 V0001 0050
F-51 ZRST Y0001 00036
```

## Proveniência

A descoberta foi obtida por engenharia reversa estática do executável original do PC12 2.1 mantido no repositório, cruzada com o manual WEG TP02 para formato de RBP/WBP, checksum, limites e semântica das instruções. O arquivo `docs/data/tp02_function_map_normalized.csv` preserva os endereços das rotinas analisadas para tornar a pesquisa auditável e reproduzível.
