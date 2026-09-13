# TP02 PG — variantes de memória 09/0A e identificação do dia da semana

Data: 2026-09-12. Continuação da pesquisa do PC12 v2.1 no OpenLadder.

Executável: `pc12.exe` do ZIP enviado, idêntico ao incluído no repositório.
SHA-256: `05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0`.

**Resultado: 691 novos quadros montados por código original e conferidos.**
Foram identificados lotes de escrita, leituras múltiplas, paginação de V/D/WC/FL,
escritas de WS/SC e o campo de dia da semana do RTC. Foi reproduzida uma
inconsistência na escrita hexadecimal de FL do próprio PC12.

Script: [emulate_pc12_memory_variants.py](../scripts/emulate_pc12_memory_variants.py).
Saída: [pc12-memory-variants-emulation.txt](data/pc12-memory-variants-emulation.txt).
Esta etapa atualizou a pesquisa no repositório. A integração dos montadores,
decodificadores e paginação está descrita nas [notas da v1.42](releases/v1.42.md).
A validação física continua sendo uma etapa distinta.

## 09 — várias escritas no mesmo quadro

Os três construtores de V, D e WC repetem um registro de cinco bytes:

```text
09 LEN [A_H A_L 02 VAL_H VAL_L] ... CHK
LEN = 5 × quantidade de registros no quadro
CHK = (FFh - soma dos bytes anteriores) AND FFh
```

Cada registro contém **seu próprio endereço**, inclusive quando os endereços não
são consecutivos. Para o número de registrador `n`, o offset é `n-1`, sem
multiplicação por dois. O valor de 16 bits tem o byte alto primeiro.

| Área | Banco | Limite de registros por quadro | Tamanho máximo do quadro |
|---|---|---:|---:|
| V | `50h` | 40 | 203 bytes |
| D | `90h` | 40 | 203 bytes |
| WC | `70h` | 40 | 203 bytes |

O PC12 divide 41 registros em 40+1, 80 em 40+40 e 81 em 40+40+1.
As retomadas foram testadas preservando a lista, o índice e as flags produzidas
pelo construtor anterior. A execução foi retomada explicitamente depois do
trecho de comunicação; não foi fornecido ACK fictício.

Exemplo: D0001=1234h, D0257=8000h e D2048=FFFFh:

```text
09 0F 90 00 02 12 34 91 00 02 80 00 97 FF 02 FF FF 66
```

## 0A — endereço e quantidade usam unidades diferentes

| Área | Banco | Passo de endereço entre páginas | Dados por pedido | Páginas no original |
|---|---|---:|---:|---:|
| V | `50h` | 64 registradores | 128 bytes | 16 |
| D | `90h` | 64 registradores | 128 bytes | 32 |
| WC | `70h` | 57 registradores | 114 bytes | 16 |
| FL | `80h` | 10 arquivos | 200 bytes | 13 |

Todos os índices de página dessas quatro rotinas foram fornecidos aos
construtores nativos. Os limites dos laços foram conferidos na desmontagem.
Não foi emulada a recepção de todas as páginas nem a gravação dos arquivos.

| Área | Primeiro quadro | Último quadro |
|---|---|---|
| V | `0A 03 50 00 80 22` | `0A 03 53 C0 80 5F` |
| D | `0A 03 90 00 80 E2` | `0A 03 97 C0 80 1B` |
| WC | `0A 03 70 00 72 10` | `0A 03 73 57 72 B6` |
| FL | `0A 03 80 00 C8 AA` | `0A 03 80 78 C8 32` |

**Consequência para o OpenLadder:** um incremento genérico igual ao número de
bytes pode pular dados. Em V/D/WC, o endereço é índice de registrador; em FL,
o construtor avança por arquivo. A quantidade solicitada representa bytes.

A leitura de sistema é diferente: o original monta `6000/AC` e `60AC/AC`.
Esses dois quadros foram reproduzidos, mas isso não prova que o espaço WS seja
linear em bytes. Preservar essa particularidade enquanto seu layout é estudado.
A inferência de contiguidade universal do
[relatório de 09/09](tp02-pg-leitura-0a-2026-09-09.md) não deve ser estendida
a V/D/WC/FL.

## Leitura de bits e leituras múltiplas do monitor Ladder

| Área | Banco no `0A` | Offset | Banco no `35` SET/RESET já identificado |
|---|---|---|---|
| X | `20h` | `(n-1)//8` | `50h` |
| Y | `00h` | `(n-1)//8` | `10h` |
| C | `10h` | `(n-1)//8` | `30h` |
| SC | `A0h` | `(n-1)//8` | Não estabelecido nesta pesquisa |
| V | `50h` | `n-1` | Não se aplica ao caminho X/Y/C do PG35 |
| D | `90h` | `n-1` | Não se aplica ao caminho X/Y/C do PG35 |
| WC | `70h` | `n-1` | Não se aplica ao caminho X/Y/C do PG35 |

O helper do monitor em `004C1BF2` foi executado através dos três callers:
dois blocos de 16 bytes e o item individual de 2 bytes. Foram testadas
fronteiras de byte e registrador, limites de área e os dois estados do flag
interno `objeto+14Dh`. Nos bits com flag zero, o índice retornado é `(n-1)%8`.
Isso testa codificação; não comprova que leituras junto ao fim da área sejam
aceitas pelo PLC com a quantidade fixa solicitada pelo monitor.

Os dois construtores de monitoramento Ladder também repetem descritores:

```text
0A LEN [A_H A_L Q] ... CHK
LEN = 3 × numero de descritores
dados esperados pelo PC12 = soma de Q
```

Nos caminhos de contatos testados, X/Y/C/SC solicitam um byte; S solicita
dois bytes de V. S0101 aponta para `5000`, S0816 para `5007`.
Os caminhos de grade e de listagem produziram os mesmos quadros para as
mesmas entradas. Por exemplo, X0008, X0009 e Y0384:

```text
0A 09 20 00 01 20 01 01 00 2F 01 79
```

As rotinas contêm outros ramos, incluindo pedidos de quatro bytes. A seleção
por todas as instruções Ladder e a distribuição de suas respostas não foram
esgotadas pelos oito cenários desta etapa.

## WS, SC e saída de Remote I/O

A escrita de sistema em `004B9C9E` seleciona **45 números WS**. Cada um gera
`09 05 60 (n-1) 02 VAL_H VAL_L CHK`. A lista, na ordem do original, é:

```text
4,18,19,20,21,22,23,24,25,41,42,43,44,45,46,47,49,58,59,60,61,62,
12,63,64,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,82,83,84,85,86
```

Isso descreve a seleção desta rotina, não uma lista universal dos únicos WS
graváveis. Os valores testados foram 0000h, 1234h, 8000h e FFFFh.

O caminho SC lê valores indexados pelo número SC e empacota duas escritas:

| Valores de origem | Endereço | Bits usados | Formato |
|---|---|---|---|
| SC001–SC008 | `A000` | 0–7 | `09 04 A0 00 01 VAL CHK` |
| SC017–SC023 | `A002` | 0–6 | `09 04 A0 02 01 VAL CHK` |

Somente o inteiro **1** liga o bit neste construtor. Foram testados bits
isolados, todos ligados, todos desligados, valor 2 e entradas fora desses
dois grupos. Não extrapolar essa operação para um SET/RESET individual de SC.

Outros quadros nativos confirmados:

| Caminho | Quadro ou formato |
|---|---|
| Leitura de WS039 | `0A 03 60 26 02 6A` |
| Leitura de WS040 | `0A 03 60 27 02 69` |
| Escrita nos mesmos dois endereços | `09 05 60 26/27 02 VAL_H VAL_L CHK` |
| Leitura no handler de erro A | `0A 03 60 05 02 8B` |
| Leitura no handler de erro B | `0A 03 60 04 02 8C` |
| Sair de Remote I/O | `09 05 60 2A 02 00 00 65` |
| RTC somente hora/minuto/segundo | `0A 03 53 F9 06 A0` |

Remote I/O corresponde a zerar WS043 nesse construtor. A intenção também é
indicada pelas strings de confirmação e conclusão do handler. Nos caminhos
WS039/040, foram usados quatro bytes sintéticos já preparados pelo caller;
não foi executada a transformação anterior nem estabelecida aqui a semântica
completa desses campos. As duas leituras de erro não decodificam, por si,
todos os códigos de falha.

## FL — diferença entre ASCII e hexadecimal no original

O cabeçalho de cada FL é `80 (numero-1) 14`, com quantidade declarada de
20 bytes. O limite é **10 arquivos por quadro**, com endereço próprio para
cada arquivo.

| Entrada testada | Dados efetivamente acrescentados por FL | Quadro com 1 FL | Quadro com 10 FL |
|---|---:|---:|---:|
| 20 caracteres ASCII simples | 20 bytes | 26 bytes | 233 bytes |
| 40 caracteres hexadecimais | **21 bytes** | **27 bytes** | **243 bytes** |

No ramo hexadecimal, a comparação em `004B9240` mantém a iteração quando o
índice chega a 40. Essa iteração lê o terminador e o byte seguinte, gerando
um 21º byte, embora o campo de quantidade continue `14h`.

O teste alterou somente o byte imediatamente após o NUL: com zero ali, o
byte excedente foi `00`; com `A`, passou a `0A`. A conversão hexadecimal
foi executada pelo helper original `004C3570`. Isso demonstra dependência
da memória além do texto, não apenas uma divergência do modelo esperado.

**Não adotar esse byte excedente como requisito do protocolo.** A emulação
reproduz o que este executável faz; não demonstra como o PLC reage ao quadro
inconsistente. A versão ASCII também contém conversões de caracteres
especiais; esta rodada cobriu apenas ASCII simples de tamanho exato.

## RTC — campo auxiliar identificado como dia da semana

O codificador original associa V1018 a `53F9` e avança um endereço por
registrador. A leitura de 14 bytes percorre V1018–V1024. O mapa do
manual WEG TP02, seção de arquitetura de memória, identifica o quinto
registrador como semana. O manual TP02 em inglês, seção 11-2, esclarece
a codificação de domingo a sábado como 0 a 6.
Fontes: [manual WEG TP02, cópia pública](https://pt.scribd.com/document/205614770/Manual-de-operacao-de-plc-WEG-pdf),
[manual TP02 em inglês, seção 11-2](https://pdfcoffee.com/plc-teco-tp02manual-en-pdf-free.html).

| Registrador | Endereço PG | Campo nos sete pares de bytes |
|---|---|---|
| V1018 | `53F9` | Segundo |
| V1019 | `53FA` | Minuto |
| V1020 | `53FB` | Hora |
| V1021 | `53FC` | Dia do mês |
| **V1022** | **`53FD`** | **Dia da semana: 0=domingo … 6=sábado** |
| V1023 | `53FE` | Mês |
| V1024 | `53FF` | Ano |

Essa associação resulta do cruzamento da documentação com os endereços e o
parser emulados. O executável sozinho não dá nome ao quinto campo na tela.
O PC12 preserva esse campo na leitura/escrita, sem controle editável no diálogo
RTC analisado. A convenção de século permanece pendente.

O valor 7 usado na rodada anterior era uma entrada sintética para testar
transporte e preservação do campo; **não é um dia da semana documentado**.

## Inventário de cobertura dos locais de construção

Com esta etapa e os scripts de monitor/relógios anteriores, cada um dos
**10 locais imediatos de PG09 e 16 de PG0A** do inventário possui ao menos
um caminho de construção emulado. Isso não equivale a cobrir todos os ramos
de cada função ou todos os comandos do firmware.

| PG | Locais no executável | Caminho coberto |
|---|---|---|
| 09 | `4B801F`, `4B8526`, `4B8A2F` | Lotes V/D/WC |
| 09 | `4B9084` | FL ASCII e hexadecimal |
| 09 | `4B98DA` | Pares preparados para WS039/040 |
| 09 | `4B9C9E` | Seleção de 45 WS |
| 09 | `4BA20E` | Dois grupos de SC |
| 09 | `4BC811` | RTC — script de relógios anterior |
| 09 | `4BD5FE` | Saída de Remote I/O |
| 09 | `4C2502` | Registrador individual — script de monitor anterior |
| 0A | `4B2DD9`, `4B3234`, `4B36BC`, `4B3B93` | Páginas V/D/WC/FL |
| 0A | `4B413F` | WS039/040 |
| 0A | `4B4459` | Duas leituras de sistema |
| 0A | `4BC568` | RTC completo — script de relógios anterior |
| 0A | `4BD312`, `4BD45E` | Pedidos no handler de erro |
| 0A | `4BF443` | Scan time — script de relógios anterior |
| 0A | `4BF871`, `4C015F`, `4C0A54` | Dois blocos e item individual do monitor |
| 0A | `4C153C` | RTC somente horário |
| 0A | `4C37B3`, `4C40F3` | Leituras múltiplas de contatos na grade/listagem |

## Reprodução e limites

Na raiz do repositório:

```bash
python3 -m pip install unicorn==2.1.4 pefile==2024.8.26
python3 scripts/emulate_pc12_memory_variants.py -o docs/data/pc12-memory-variants-emulation.txt
```

| Grupo | Quadros conferidos |
|---|---:|
| Lotes V/D/WC | 34 |
| Páginas V/D/WC/FL | 77 |
| Escrita FL e inconsistência hexadecimal | 11 |
| Monitor 0A | 324 |
| Endereços RTC | 7 |
| Escrita WS | 180 |
| Escrita SC | 38 |
| Leitura múltipla Ladder | 8 |
| Leitura WS em páginas | 2 |
| WS039/040 e pedidos fixos | 10 |
| **Total desta etapa** | **691** |

A máquina verifica o hash do executável e os nomes dos imports substituídos.
Usa trechos permitidos de código e aborta ao entrar em qualquer outro endereço.
As instruções que calculam endereços, dados, quantidade e checksum são nativas.
Somente strings/conversões CRT, relógio do Windows e progresso visual usam
stubs. A rotina TX é interceptada antes de executar sua primeira instrução.

Continuam pendentes os ramos restantes de monitoramento por instrução,
caracteres especiais de FL, o layout completo de sistema, semântica dos
erros, convenção de século e aceitação/efeitos no hardware das novas variantes.
Os 691 resultados não são uma medição percentual de compatibilidade do TP02.
