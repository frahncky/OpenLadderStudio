# TP02 PG — experimento de paginação do comando 34 (PG Lab 1.19)

## Objetivo

Validar fisicamente se o pedido:

```text
34 03 [step_hi] [step_lo] A0 chk
```

usa `[step_hi][step_lo]` como contador inicial de passos do programa e se uma nova página de 80 passos pode ser lida iniciando em `0x0050` (80 decimal).

A página base já é fisicamente conhecida:

```text
34 03 00 00 A0 28
```

A sonda experimental desta etapa é:

```text
34 03 00 50 A0 D8
```

Checksum:

```text
34 + 03 + 00 + 50 + A0 + D8 = FF (mod 256)
```

## Segurança

O PG Lab continua em modo READ-ONLY.

A página 1 só é consultada quando todas as condições abaixo forem satisfeitas:

1. operador marcou a autorização READ-ONLY;
2. HELLO confirmou STOP;
3. F0 devolveu o vetor físico conhecido;
4. 38 devolveu quadro estrutural válido;
5. a página 0 do 34 devolveu LEN/checksum válidos;
6. `38.payload[1] >= A0`, condição usada apenas como indicação experimental de que o programa pode ultrapassar 80 passos.

A sonda `34 03 00 50 A0 D8` é enviada no máximo uma vez por execução. Se ficar silenciosa ou devolver quadro inesperado, não há retentativa automática da página 1.

Nenhum comando de escrita, download, apagamento, firmware ou RUN/STOP remoto é acrescentado.

## Programa de bancada recomendado

Usar 18 rungs iguais de ADD word, variando somente o contato de entrada:

```text
RUNG 01: X0001 -> F-13w ADD D0002, D0001, 10
RUNG 02: X0002 -> F-13w ADD D0002, D0001, 10
...
RUNG 16: X0016 -> F-13w ADD D0002, D0001, 10
RUNG 17: X0017 -> F-13w ADD D0002, D0001, 10
RUNG 18: X0018 -> F-13w ADD D0002, D0001, 10
END
```

Cada rung ocupa, conforme a captura física anterior:

```text
STR Xnnnn       = 1 passo
F-13w ADD       = 4 passos
-------------------------
total/rung      = 5 passos
```

Logo:

```text
18 rungs x 5 passos = 90 passos
END                  =  1 passo
TOTAL                = 91 passos
```

Essa distribuição foi escolhida porque a fronteira fica limpa:

```text
passos 00..79 = rungs 1..16 completos
passo 80      = STR X0017
passos 80..84 = rung 17 completo
passos 85..89 = rung 18 completo
passo 90      = END
```

Assim, se a hipótese de paginação estiver correta, a página iniciada em `0x0050` deverá começar exatamente no `STR X0017` e conter 11 passos ativos.

## Previsão do comando 38

Até agora, todos os programas físicos observados obedeceram a:

```text
38.payload[1] = 2 * (N - 1)
```

Para `N = 91` passos, a previsão é:

```text
2 * (91 - 1) = 180 = B4h
```

Portanto, **se a relação continuar válida acima de 80 passos**, o quadro esperado será:

```text
00 02 00 B4 49
```

Isto é uma previsão experimental, não um fato confirmado.

## Previsão da página 0

Se a paginação for realmente por contador de passos, a página 0 conterá exatamente 80 passos ativos: os 16 primeiros rungs completos.

Para a geometria já observada (`FLAGS=00`, `LEN=F0`) e as codificações confirmadas, o checksum previsto da página 0 é:

```text
CF
```

Essa previsão também depende de a geometria do quadro permanecer a mesma quando o programa ultrapassa 80 passos.

## Previsão da página 1

Sob a hipótese atual, a resposta a:

```text
34 03 00 50 A0 D8
```

deverá ter `FLAGS=00`, `LEN=F0` e iniciar a Região A com:

```text
02 10   ; step global 80: STR X0017
0D 77   ; F-13w ADD
F0 01   ; D0002
F0 00   ; D0001
80 0A   ; K10
02 11   ; step global 85: STR X0018
0D 77   ; F-13w ADD
F0 01   ; D0002
F0 00   ; D0001
80 0A   ; K10
00 70   ; step global 90: F-00 END
```

Os demais pares HIGH/LOW da página deverão ser zero.

A Região B prevista para esses 11 passos é:

```text
03 0B 00 0F 02 04 0B 00 0F 02 07
```

seguida de zeros até completar os 80 bytes da Região B.

Se todos esses pressupostos forem verdadeiros, o checksum final previsto do quadro da página 1 é:

```text
56
```

Novamente: os bytes acima são **predições para validar**, não evidência física ainda.

## Critérios de confirmação

A paginação será considerada fortemente confirmada se a mesma captura apresentar:

```text
1. 38 coerente com programa >80 passos;
2. página 0 válida e preenchida até o passo local 79;
3. resposta válida ao pedido 34 com start=0050;
4. início da página 1 em STR X0017;
5. sequência completa dos rungs 17 e 18;
6. END na posição local 10 / passo global 90;
7. Região B coerente com os pares HIGH/LOW observados.
```

Se a página 1 responder com outra geometria, ela deve ser preservada integralmente e analisada antes de qualquer nova sonda.

## Próximo passo após a bancada

Somente depois da confirmação física:

- generalizar o decoder para `baseStep`/páginas múltiplas;
- decidir a regra de parada para programas maiores;
- testar a página seguinte com base calculada, sem hard-code;
- manter escrita/download fora de escopo até etapa específica e deliberada.
