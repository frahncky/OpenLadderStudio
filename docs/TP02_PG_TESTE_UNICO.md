# Teste Unico PG - WEG TP02

> **DOCUMENTO HISTÓRICO.** Este arquivo descreve uma fase anterior do laboratório e contém estratégias que já foram substituídas. Para o estado técnico atual, vetores confirmados, hipóteses, codificação parcial do payload 34 e ponto exato de retomada, use **[`TP02_PG_ESTADO_DA_ARTE.md`](TP02_PG_ESTADO_DA_ARTE.md)**. O snapshot canônico atual é OpenLadder Studio v1.03 / TP02 PG Lab 1.16.

O Teste Unico concentra a campanha de descoberta do protocolo PG do TP02 em uma unica execucao. O operador seleciona a porta COM e pressiona `EXECUTAR TESTE UNICO TP02`. Nao e necessario rodar F0, 38, leitura de programa ou leitura de sistema separadamente.

## Fluxo

1. abre a porta em `19200 8O1` e prioriza `DTR=on`, `RTS=on`;
2. escuta passivamente antes do primeiro TX;
3. envia `CON-ICB<CR>` e procura HELLO conhecido;
4. se nao houver RX, rearma DTR/RTS e reabre a COM automaticamente;
5. se necessario, repete o processo com as demais combinacoes DTR/RTS;
6. quando encontra o enlace, preserva a mesma sessao serial;
7. executa a matriz pos-handshake do F0 na mesma porta aberta;
8. se o F0 conhecido for confirmado, libera automaticamente o `38 00 C7` na mesma sessao;
9. executa os demais probes READ-ONLY allowlisted mesmo quando algum deles fica sem resposta;
10. salva um unico relatorio TXT e JSON.

## Consultas do Teste Unico

| Ordem | TX | Origem / uso | Regra |
|---|---|---|---|
| 1 | `43 4F 4E 2D 49 43 42 0D` | HELLO `CON-ICB<CR>` | handshake protegido |
| 2 | `F0 00 0F` | status/preflight PC12 | READ_ONLY_VERIFIED; matriz pos-handshake |
| 3 | `38 00 C7` | preambulo de Read PLC Program | somente apos F0 conhecido na mesma sessao |
| 4 | `34 03 00 00 A0 28` | fluxo Read PLC Program recuperado do PC12 | READ_ONLY_PROBE |
| 5 | `0A 03 60 00 AC E6` | Read PLC System - parte 1 | READ_ONLY_PROBE |
| 6 | `0A 03 60 AC AC 3A` | Read PLC System - parte 2 | READ_ONLY_PROBE |
| 7 | `14 00 EB` | ramo de consulta de senha observado no PC12 | READ_ONLY_PROBE |

Os `READ_ONLY_PROBE` so podem sair quando coincidem byte a byte com a lista interna compilada no executavel e tambem aparecem na `readOnlyAllowlist` do pacote.

## Recuperacao automatica do enlace

Cada perfil executa ate seis tentativas de HELLO. Depois de duas falhas, o motor rearma DTR/RTS sem TX adicional. Depois de quatro falhas, fecha e reabre a mesma COM. As tentativas 5 e 6 verificam o estado depois da reabertura.

A bancada da v0.91 encontrou o enlace automaticamente no fallback `19200 8O1`, `DTR=on`, `RTS=off`, depois da reabertura da COM. O HELLO STOP foi:

```text
80 01 09 75
```

com soma modulo 256 igual a `FF`.

## Matriz pos-handshake do F0

A captura da v0.91 trouxe uma diferenca importante: o HELLO foi recuperado com `RTS=off`, mas o `F0 00 0F` ficou silencioso nessa mesma sessao. Em uma captura fisica anterior, o F0 respondeu com `DTR=on`, `RTS=on`:

```text
00 02 10 22 CB
```

com soma modulo 256 igual a `FF`.

O motor PG Lab 1.7, introduzido na v0.92, preserva a porta aberta depois do HELLO e testa automaticamente seis variantes do mesmo F0:

1. estado RTS vencedor, atraso de 120 ms;
2. estado RTS vencedor, atraso de 600 ms;
3. RTS on, atraso de 120 ms;
4. RTS on, atraso de 600 ms;
5. TX com RTS on e RX com RTS off;
6. TX com RTS off e RX com RTS on.

DTR permanece no estado do perfil que encontrou o HELLO. A COM nao e reaberta durante essa matriz, porque a validacao do F0 precisa pertencer a mesma sessao que autoriza o `38 00 C7`.

A matriz nao introduz nenhum opcode novo: transmite somente `F0 00 0F`, que ja e uma consulta READ_ONLY_VERIFIED. Se alguma variante devolver `00 02 10 22 CB`, o motor registra a variante vencedora, preserva o RTS correspondente e segue automaticamente para o 38.

## Bloqueios permanentes

O Teste Unico nao envia escrita de memoria/programa, WBP/download, RUN ou STOP remoto, apagamento, firmware nem comandos classificados como `BLOCKED`. `0F 00 F0`, associado no PC12 a Clear All Memory, permanece explicitamente bloqueado.

## Resultado esperado

O relatorio unico deve mostrar o perfil de enlace vencedor, o HELLO, cada variante F0 e seu estado RTS, a eventual confirmacao `00 02 10 22 CB`, a resposta ou silencio do 38, 34, dois quadros 0A e 14, alem de todos os subquadros cuja soma modulo 256 fecha em `FF`.

A partir desse unico relatorio, a proxima versao pode fixar a transicao pos-handshake correta e avancar na leitura do programa sem novos ensaios manuais por etapa.
