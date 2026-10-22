# Evidencia de ressincronizacao PG usada pela v0.94

A captura fisica da v0.92 mostrou um comportamento relevante durante a matriz F0: na variante em que o TX ocorreu com RTS ligado e a recepcao passou para RTS desligado, o TP02 retornou novamente o HELLO-STOP conhecido:

```text
80 01 09 75
```

Esse quadro possui soma modulo 256 igual a `FF` e ja havia sido observado no handshake.

A Pesquisa Continua IA recebe esse fato no contexto tecnico da sessao. Se um probe produzir novamente um HELLO conhecido, o motor o classifica como ressincronizacao do enlace, zera a validacao F0 da sessao e continua a investigacao sem considerar o quadro uma resposta valida do probe.

O objetivo e permitir que a mesma sessao serial explore automaticamente a relacao entre DTR/RTS, temporizacao, HELLO e F0 sem exigir uma nova versao do executavel para cada hipotese.
