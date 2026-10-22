# TP02 — diagnóstico offline do estabelecimento do enlace SAFE

Este documento registra **somente estatísticas agregadas** de duas capturas físicas da v1.61, feitas em 16/09/2026. Os ZIPs, quadros brutos e conteúdo de memória do PLC **não** são armazenados no repositório.

## Evidências observadas

| Métrica | 09:58 | 10:23 |
|---|---:|---:|
| Perfil | COM1, 19200 8O1, DTR/RTS OFF | mesmo perfil |
| Sessão qualificada | 8ª | 7ª |
| HELLO transmitidos / respondidos | 44 / 2 | 41 / 2 |
| HELLO sem bytes de resposta | 42 | 39 |
| Tempo registrado do HELLO respondido, ms | 229 / 229 | 229 / 230 |
| HELLO silenciosos, mediana em ms | 1803 | 1804 |
| F0 transmitidos / respondidos | 2 / 1 | 2 / 1 |
| Tempo registrado do F0 respondido, ms | 241 | 251 |
| Tempo do F0 silencioso, ms | 1600 | 1606 |
| Tempo até HELLO+F0, s | 105,253 | 97,221 |
| Tempo acumulado sem bytes HELLO/F0, s | 77,344 | 71,964 |
| PG38/PG34/0A com resposta | 110 / 110 | 110 / 110 |
| Páginas de programa iguais nas capturas | sim | sim |

**Precisão da medição:** `ExchangeRaw` inicia o cronômetro antes da escrita e `ReadBurst` continua até haver **220 ms de silêncio depois do último byte** durante HELLO/F0. Portanto, 229–251 ms são *tempo total da troca incluindo a janela de silêncio*, não latência até o primeiro byte. A captura v1.61 não registra timestamp por byte; não usar esses números para afirmar latência elétrica nem para encurtar timeouts sem testes específicos.

**Interpretação limitada:** o silêncio ocorre predominantemente antes da qualificação, enquanto as operações de leitura observadas após a qualificação responderam. Duas medições não identificam a causa física: cabo, conversor, driver, estado do PLC e temporização ainda não foram isolados. A redução de uma sessão não prova melhora causal.

## Como repetir a análise sem conectar ao PLC

A ferramenta `scripts/diagnose_tp02_link.py` utiliza apenas `responses.csv` e os marcadores temporais de `session.log` dentro do ZIP; não abre COM nem transmite comandos. Exige Python 3 no computador que executa a análise.

```bash
python3 scripts/diagnose_tp02_link.py primeira-captura.zip segunda-captura.zip --out comparacao.json
```

Aceita um único ZIP ou pasta extraída. Saídas: duração registrada das trocas HELLO/F0/leitura, tempo até qualificação, sessões e tentativas, tempo perdido aguardando respostas ausentes, comparação de páginas PG34 e resultado do auditor SAFE. **Nunca exporta quadros de memória, dados de registradores ou o texto integral do log.** A validação sintética é executada por `.github/workflows/audit-tp02-safe-capture.yml`.

## Identificar a COM1 sem abrir a porta

No Windows 7 ou posterior, execute `scripts/windows/ExecutarDiagnosticoCOM1.bat` com `Tp02DriverProbe.ps1` na mesma pasta. O script usa `Get-WmiObject` apenas para consultar metadados (`Win32_PnPEntity`, `Win32_SerialPort`, `Win32_PnPSignedDriver`). Não instancia `SerialPort`, não abre a COM e não transmite nada ao TP02.

A saída é `TP02-Porta-COM1-diagnostico.txt`, na mesma pasta do script. Contém descrição, fabricante, serviço, versão/fornecedor do driver e indicação de USB/PCI/ACPI *se* a enumeração do Windows permitir. **Não exporta PNPDeviceID, VID/PID, número de série, nome do usuário ou programa do PLC.** Se a WMI não localizar o dispositivo, o resultado é inconclusivo; confira manualmente o Gerenciador de Dispositivos. O resultado só identifica a porta do computador, não comprova pinagem nem saúde do cabo.

Para outra porta: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tp02DriverProbe.ps1 -Port COM2`. O teste de CI usa WMI *simulada*, sem acesso a COM física.

## Próxima investigação controlada

Preservar v1.61, 19200 8O1, DTR/RTS OFF e os intervalos atuais. Examinar primeiro o metadado da COM1 e a topologia do cabo/conversor. Se novos testes físicos forem necessários, variar **uma única condição documentada** por vez e permanecer em SAFE: sem escrita, RUN/STOP, Clear ou BIOS. O auditor e os tempos de leitura não validam PG33 no PLC físico.
