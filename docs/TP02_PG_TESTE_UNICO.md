# Teste Unico PG - WEG TP02

O Teste Unico concentra a campanha de descoberta do protocolo PG do TP02 em uma unica execucao. O operador seleciona a porta COM e pressiona `EXECUTAR TESTE UNICO TP02`. Nao e necessario rodar F0, 38, leitura de programa ou leitura de sistema separadamente.

## Fluxo

1. abre a porta no perfil fisicamente validado `19200 8O1`, `DTR=on`, `RTS=on`;
2. escuta passivamente antes do primeiro TX;
3. envia `CON-ICB<CR>` e procura HELLO conhecido;
4. se nao houver RX, rearma DTR/RTS e reabre a COM automaticamente;
5. se necessario, repete o mesmo processo com as demais combinacoes DTR/RTS, mantendo `19200 8O1` fixo;
6. quando encontra o enlace, preserva a mesma sessao serial;
7. executa automaticamente as consultas READ-ONLY allowlisted;
8. nao interrompe a campanha quando um probe nao responde ou retorna quadro ainda desconhecido;
9. salva um unico relatorio TXT e JSON com toda a sessao.

## Consultas do Teste Unico

| Ordem | TX | Origem / uso | Regra |
|---|---|---|---|
| 1 | `43 4F 4E 2D 49 43 42 0D` | HELLO `CON-ICB<CR>` | handshake protegido |
| 2 | `F0 00 0F` | status/preflight PC12 | READ_ONLY_VERIFIED |
| 3 | `38 00 C7` | preambulo de Read PLC Program | somente apos F0 conhecido na mesma sessao |
| 4 | `34 03 00 00 A0 28` | fluxo Read PLC Program recuperado do PC12 | READ_ONLY_PROBE |
| 5 | `0A 03 60 00 AC E6` | Read PLC System - parte 1 | READ_ONLY_PROBE |
| 6 | `0A 03 60 AC AC 3A` | Read PLC System - parte 2 | READ_ONLY_PROBE |
| 7 | `14 00 EB` | ramo de consulta de senha observado no PC12 | READ_ONLY_PROBE |

Os `READ_ONLY_PROBE` acima sao permitidos somente quando coincidem byte a byte com a lista interna do motor e tambem aparecem na `readOnlyAllowlist` do pacote. Isso impede que uma atualizacao remota do JSON habilite arbitrariamente outro quadro.

## Recuperacao automatica do enlace

Cada perfil executa ate seis tentativas de HELLO:

- apos 2 falhas consecutivas: rearme de DTR/RTS sem TX adicional;
- apos 4 falhas consecutivas: fecha e reabre a mesma COM;
- tentativas 5 e 6: verificam o enlace depois da reabertura.

O perfil comprovado em bancada continua sendo tentado primeiro. Os perfis seguintes alteram apenas DTR/RTS para diagnosticar o conversor/driver sem mudar baud, bits, paridade ou stop bit.

## Evidencias fisicas ja confirmadas

Em bancada, o HELLO STOP respondeu:

```text
80 01 09 75
```

com soma modulo 256 igual a `FF`.

O `F0 00 0F` respondeu na mesma sessao:

```text
00 02 10 22 CB
```

novamente com soma modulo 256 igual a `FF`.

O mesmo conjunto PLC/conversor tambem apresentou sessoes sem RX, motivo pelo qual o Teste Unico incorpora recovery e matriz DTR/RTS automaticamente.

## Bloqueios permanentes

O Teste Unico nao envia:

- escrita de memoria/programa;
- WBP/download;
- RUN ou STOP remoto;
- apagamento de memoria;
- firmware;
- comandos classificados como `BLOCKED`;
- qualquer quadro que nao esteja simultaneamente na lista interna do motor e na allowlist do pacote.

`0F 00 F0`, associado no PC12 a Clear All Memory, permanece explicitamente bloqueado.

## Resultado esperado da campanha

O objetivo nao e apenas obter `SUCCESS`, mas descobrir qual comando produz RX util. O relatorio deve permitir identificar rapidamente:

- perfil de enlace vencedor;
- HELLO recebido;
- resposta do F0;
- resposta ou silencio do 38;
- resposta ou silencio do 34;
- respostas dos dois quadros 0A;
- resposta do 14;
- todos os subquadros cuja soma modulo 256 fecha em `FF`.

A partir desse unico relatorio, a proxima versao do driver pode promover os quadros confirmados de `READ_ONLY_PROBE` para comandos documentados do protocolo PG.
