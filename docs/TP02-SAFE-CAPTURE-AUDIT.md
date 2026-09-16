# Auditoria offline das capturas TP02 SAFE

A captura fisica de 16/09/2026, produzida pela v1.61 em COM1, separa dois problemas que nao devem ser confundidos: a aquisicao inicial da sessao PG e a leitura apos a qualificacao HELLO + F0.

## Evidencia da captura de referencia (metadados apenas)

- Perfil de captura: 19200 8O1, DTR=OFF, RTS=OFF; modo SAFE.
- 8 sessoes abertas; 44 requisicoes HELLO, com 2 respostas reconhecidas.
- 2 requisicoes F0, com 1 resposta reconhecida; sessao qualificada: 8.
- Sessao 3: HELLO respondeu, mas o unico F0 permaneceu sem resposta.
- Depois do F0 confirmado na sessao 8: 110/110 requisicoes de leitura com resposta (PG38: 1; PG34: 5; memoria/RTC/scan: 104).
- 113/113 respostas com comprimento e checksum validos.
- PG34: paginas 0, 80, 160, 240 e 320; primeiro END no passo 322 (323 palavras logicas).

**Conclusao limitada:** nesta captura, a intermitencia ocorreu antes da qualificacao. Nao houve perda de resposta de leitura depois dela. Isso nao prova que a causa seja DTR, RTS, temporizacao, cabo ou driver; uma unica captura nao isola esses fatores.

O ZIP bruto pode conter memoria e dados do PLC. **Nao envie a captura bruta ao repositório publico**; mantenha-a localmente. Este documento registra apenas estatisticas agregadas.

## Executar o auditor

No computador com Python 3 instalado, a partir da raiz do repositorio:

```text
python scripts/analyze_tp02_full_capture.py "caminho/para/captura.zip"
python scripts/analyze_tp02_full_capture.py "captura-1.zip" --compare "captura-2.zip"
```

O analisador le somente `responses.csv` dentro do ZIP ou de uma pasta extraida. Ele nao abre COM, nao envia bytes, nao escreve no PLC e nao imprime os dados brutos de memoria. O resultado `PASS` atesta consistencia dos quadros da captura, **nao confiabilidade total do enlace ou validacao de comandos de escrita**. Retorna codigo 1 se encontrar anomalias e 2 para arquivo invalido.

Campos principais: `sessions_opened`, `hello_attempts`, `hello_replies`, `f0_attempts`, `f0_replies`, `qualified_session`, `read_without_reply`, `invalid_response_frames`, `pg34_pages`, `end_step` e `issues`.

## Proximo teste em bancada

Somente se for seguro e conveniente, repita uma captura SAFE com o mesmo cabo, porta e configuracao, sem alterar parametros ou gravar no PLC. Compare as contagens de sessao, HELLO e F0 com o auditor. Se a variabilidade persistir, examine alimentacao, interface serial, conexoes e registros do driver antes de experimentar outras temporizacoes. Nao use os comandos 33/37, escrita, clear ou RUN/STOP remoto para diagnosticar falhas de aquisicao.
