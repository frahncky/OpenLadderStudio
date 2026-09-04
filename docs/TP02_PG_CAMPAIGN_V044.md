# Laboratório PG TP02 — campanha de caracterização v0.44

A v0.44 é uma atualização estrutural única do motor do Laboratório PG. O objetivo é evitar novas versões do aplicativo para cada ensaio de bancada.

## O que muda

Após localizar uma resposta HELLO conhecida, o motor 1.1 executa uma campanha configurada pelo arquivo `TP02-PG-Tests.json`.

O pacote atual executa 12 ciclos no mesmo perfil serial validado, com rearme de DTR/RTS entre os ciclos e até 8 tentativas de `CON-ICB<CR>` por ciclo.

A campanha registra:

- quantidade de respostas `C0 01 09 35`;
- quantidade de respostas `80 01 09 75`;
- quantidade de ciclos sem resposta;
- qualquer resposta desconhecida;
- número de mudanças entre as duas variantes conhecidas;
- tentativa em que cada resposta apareceu;
- latência mínima, média e máxima;
- captura passiva curta após cada ciclo;
- relatório TXT e JSON com todas as amostras.

## Parada segura

A campanha é interrompida imediatamente quando recebe um quadro diferente das variantes conhecidas ou qualquer byte espontâneo durante a janela passiva configurada. O dado é preservado no relatório para análise.

Nenhum comando adicional de controle do PLC é liberado. O único TX automático da campanha permanece:

`43 4F 4E 2D 49 43 42 0D` = `CON-ICB<CR>`

Os quadros `F0 00 0F`, `0F 00 F0`, candidatos de leitura, RUN, STOP, escrita, download e apagamento continuam bloqueados.

## Atualizações futuras

Depois da instalação da v0.44, a quantidade de ciclos, tempos, perfis, respostas conhecidas e futuras etapas de leitura verificadas devem ser alteradas preferencialmente apenas em `TP02-PG-Tests.json`, que o Laboratório PG atualiza automaticamente sem reinstalar o OpenLadder Studio.
