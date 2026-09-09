# Teste Unico PG - WEG TP02

O Teste Unico concentra a campanha de descoberta do protocolo PG do TP02 em uma unica execucao. O operador seleciona a porta COM e pressiona `EXECUTAR TESTE UNICO TP02`. Nao e necessario rodar F0, 38, leitura de programa ou leitura de sistema separadamente.

## Fluxo

1. abre a porta em `19200 8O1` e prioriza `DTR=on`, `RTS=on`;
2. escuta passivamente antes do primeiro TX;
3. envia `CON-ICB<CR>` e procura HELLO conhecido;
4. se nao houver RX, rearma DTR/RTS e reabre a COM automaticamente;
5. se necessario, repete o processo com as demais combinacoes DTR/RTS;
6. quando encontra o enlace, preserva a mesma sessao serial;
7. executa a matriz pos-handshake do F0 na mesma porta aberta;
8. se um HELLO conhecido reaparecer durante F0, trata o evento como ressincronizacao e tenta F0 novamente sem reiniciar a campanha;
9. se o F0 conhecido for confirmado, libera automaticamente `38 00 C7` na mesma sessao;
10. executa os demais probes READ-ONLY allowlisted mesmo quando algum deles fica sem resposta;
11. salva um unico relatorio TXT e JSON.

## Consultas do Teste Unico

| Ordem | TX | Origem / uso | Regra |
|---|---|---|---|
| 1 | `43 4F 4E 2D 49 43 42 0D` | HELLO `CON-ICB<CR>` | handshake protegido |
| 2 | `F0 00 0F` | status/preflight PC12 | READ_ONLY_VERIFIED; matriz pos-handshake + ressincronizacao |
| 3 | `38 00 C7` | preambulo de Read PLC Program | somente apos F0 conhecido na mesma sessao |
| 4 | `34 03 00 00 A0 28` | fluxo Read PLC Program recuperado do PC12 | READ_ONLY_PROBE |
| 5 | `0A 03 60 00 AC E6` | Read PLC System - parte 1 | READ_ONLY_PROBE |
| 6 | `0A 03 60 AC AC 3A` | Read PLC System - parte 2 | READ_ONLY_PROBE |
| 7 | `14 00 EB` | ramo de consulta de senha observado no PC12 | READ_ONLY_PROBE |

Os `READ_ONLY_PROBE` so podem sair quando coincidem byte a byte com a lista interna compilada no executavel e tambem aparecem na `readOnlyAllowlist` do pacote.

## Evidencia fisica do enlace

A bancada encontrou o enlace automaticamente no fallback `19200 8O1`, `DTR=on`, `RTS=off`, depois da reabertura da COM. O HELLO STOP observado foi:

```text
80 01 09 75
```

com soma modulo 256 igual a `FF`.

Uma captura fisica anterior confirmou tambem:

```text
F0 00 0F  ->  00 02 10 22 CB
```

novamente com soma modulo 256 igual a `FF`.

## Matriz pos-handshake do F0

O motor 1.7 introduziu seis variantes automaticas do mesmo F0 na porta ja aberta:

1. estado RTS vencedor, atraso de 120 ms;
2. estado RTS vencedor, atraso de 600 ms;
3. RTS on, atraso de 120 ms;
4. RTS on, atraso de 600 ms;
5. TX com RTS on e RX com RTS off;
6. TX com RTS off e RX com RTS on.

DTR permanece no estado do perfil que encontrou o HELLO. A COM nao e reaberta durante essa matriz.

## Ressincronizacao descoberta na v0.92

A captura fisica da v0.92 mostrou que a variante 5 (`TX RTS on -> RX RTS off`) pode fazer o TP02 devolver novamente:

```text
80 01 09 75
```

Isso nao e ruido nem um quadro arbitrario: e o mesmo HELLO-STOP conhecido e com checksum valido. O motor PG Lab 1.8 da v0.93 passa a classificar esse retorno como `RESYNC`.

Quando isso ocorre, o Teste Unico preserva a mesma COM, preserva DTR e repete exclusivamente `F0 00 0F` em quatro tentativas automaticas, usando atrasos de 80, 220, 450 e 700 ms e apenas os estados RTS ja investigados. Se `00 02 10 22 CB` aparecer, o F0 e validado e o `38 00 C7` segue automaticamente na mesma sessao. Se outro HELLO aparecer, a sequencia continua sem reiniciar toda a busca de enlace.

## Bloqueios permanentes

O Teste Unico nao envia escrita de memoria/programa, WBP/download, RUN ou STOP remoto, apagamento, firmware nem comandos classificados como `BLOCKED`. `0F 00 F0`, associado no PC12 a Clear All Memory, permanece explicitamente bloqueado.

## Resultado esperado

O relatorio unico deve mostrar o perfil de enlace vencedor, o HELLO, cada variante F0, os eventos `RESYNC`, a eventual confirmacao `00 02 10 22 CB`, a resposta ou silencio do 38, 34, dois quadros 0A e 14, alem de todos os subquadros cuja soma modulo 256 fecha em `FF`.
