# TP02 — diagnóstico offline do estabelecimento do enlace SAFE

Este documento registra **somente estatísticas agregadas** de duas capturas físicas da v1.61, feitas em 16/09/2026. Os ZIPs, quadros brutos e conteúdo de memória do PLC **não** são armazenados no repositório.

## Evidências observadas

| Métrica | 09:58 | 10:23 |
|---|---:|---:|
| Perfil | COM1, 19200 8O1, DTR/RTS OFF | mesmo perfil |
| Sessão qualificada | 8ª | 7ª |
| HELLO transmitidos / respondidos | 44 / 2 | 41 / 2 |
| HELLO sem bytes de resposta | 42 | 39 |
| Respostas HELLO, em ms | 229 / 229 | 229 / 230 |
| Tentativas HELLO silenciosas, mediana em ms | 1803 | 1804 |
| F0 transmitidos / respondidos | 2 / 1 | 2 / 1 |
| F0 respondido, em ms | 241 | 251 |
| F0 silencioso, em ms | 1600 | 1606 |
| Tempo até HELLO+F0, em s | 105,253 | 97,221 |
| Tempo acumulado de espera sem bytes HELLO/F0, em s | 77,344 | 71,964 |
| PG38/PG34/0A com resposta | 110 / 110 | 110 / 110 |
| Páginas de programa iguais entre as capturas | sim | sim |

**Interpretação limitada:** o silêncio ocorre predominantemente antes da qualificação, enquanto as operações de leitura observadas após a qualificação responderam. Duas medições não identificam a causa física: cabo, conversor, driver, estado do PLC e temporização ainda não foram isolados. A redução de uma sessão não prova melhora causal.

## Como repetir sem conectar ao PLC

A ferramenta `scripts/diagnose_tp02_link.py` utiliza apenas `responses.csv` e os marcadores temporais de `session.log` dentro do ZIP; não abre COM nem transmite comandos. Exige Python 3 no computador que executa a análise.

```bash
python3 scripts/diagnose_tp02_link.py primeira-captura.zip segunda-captura.zip --out comparacao.json
```

Também aceita um único ZIP ou pasta extraída. Saídas: medianas e limites de latência HELLO/F0/leitura, tempo até qualificação, sessões e tentativas, latência perdida em espera sem resposta, identidade das páginas PG34 e resultado do auditor SAFE. **Nunca exporta quadros de memória, dados de registradores ou o texto integral do log.**

A validação sintética é executada por `.github/workflows/audit-tp02-safe-capture.yml`. Nenhum teste de CI abre uma porta serial. O auditor e os tempos de leitura não validam PG33 no PLC físico.

## Próxima investigação controlada

Preservar v1.61 e as configurações comprovadas. Investigar no computador a identidade e o driver da COM1, a topologia do cabo/conversor e eventuais aplicativos concorrentes pela porta, sem trocar vários parâmetros ao mesmo tempo. Uma captura adicional com **uma única condição física documentadamente diferente** pode ajudar a comparar aquisição; não realizar testes de escrita, RUN/STOP, Clear ou BIOS.
