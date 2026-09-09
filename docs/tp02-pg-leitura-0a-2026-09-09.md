# TP02 PG — primeira leitura com dados: comando 0A (2026-09-09)

Data: 2026-09-09, ~18:13 · Motor PG Lab 1.10 (v0.96) · Pacote `2026.09.09.7` · CLP em STOP
Relatório: `TP02-PG-Lab-20260909-181259.txt`, perfil `Fallback - DTR on RTS off`, `19200 8O1`.

Esta é a primeira sessão em que um comando de **leitura** do TP02 devolveu **dados**.

## Fatos observados

| Consulta | TX | Resultado |
|---|---|---|
| Read PLC System parte 1 | `0A 03 60 00 AC E6` | `RX []` nas 5 tentativas |
| **Read PLC System parte 2** | `0A 03 60 AC AC 3A` | tentativa 1 muda; **tentativas 2–5 → `00 AC 00 00 00 …`** |
| **Ramo de senha 14** | `14 00 EB` | **`00 00 FF` nas 3 tentativas** (t≈261–270 ms) |

As retentativas da v0.96 foram decisivas de novo: a parte 2 só respondeu a partir da 2ª
tentativa; o `14` que na v0.95 respondeu uma vez agora respondeu **3/3**.

## Interpretação

**1. O enquadramento de resposta `CMD=00 · LEN · payload · CHECKSUM` se generaliza.**

| Pedido | Resposta | Segmentação |
|---|---|---|
| `F0 00 0F` | `00 02 10 22 CB` | CMD=00, LEN=2, payload=`10 22`, chk=CB |
| `14 00 EB` | `00 00 FF` | CMD=00, LEN=0 (resposta vazia/ACK), chk=FF |
| `0A 03 60 AC AC 3A` | `00 AC 00 00 …` | CMD=00, LEN=0xAC (172 bytes), payload=`00 …` |

O primeiro byte da resposta é sempre `00` (bit de erro `0x80` limpo). O segundo byte é o
comprimento do payload.

**2. O `0A` é um comando de LEITURA parametrizado por endereço e quantidade.**

O pedido tem a forma `0A 03 [end_hi] [end_lo] [qtd] chk`:

- `end_hi end_lo` — endereço de 16 bits;
- `qtd` — número de bytes a ler; **bate com o LEN da resposta** (`0xAC` = 172).

As duas consultas conhecidas são exatamente **duas leituras contíguas de 172 bytes**:

```text
0A 03 60 00 AC E6  -> endereco 0x6000, ler 0xAC bytes   (mudo nesta sessao)
0A 03 60 AC AC 3A  -> endereco 0x60AC, ler 0xAC bytes   (devolveu 00 AC 00 00 ...)
```

O payload lido em `0x60AC` veio quase todo `00` — coerente com área de memória vazia.

Tudo acima é interpretação forte a partir de captura física; a semântica byte a byte do
conteúdo lido continua a confirmar.

## Consequência: varredura de leitura (v0.97)

Como o `0A` lê `qtd` bytes por endereço, dá para **varrer endereços contíguos e baixar a
memória**. A v0.97 (motor 1.11) acrescenta o tipo de etapa `read_sweep_0a`: a partir de um
quadro `0A` base (endereço inicial + `qtd`), o motor monta novos quadros `0A` com checksum,
avança pelo passo `qtd` e captura cada resposta, com retentativa por endereço e parada após
6 endereços silenciosos seguidos.

**Segurança:** a montagem é intrínseca a `CMD=0A` (leitura) — o motor não consegue produzir
escrita, apagamento ou firmware por essa via. A varredura só roda no modo READ-ONLY e é
limitada em número de leituras.

## Próximos ensaios

1. Rodar o Teste Único v0.97 e observar as linhas `VARREDURA` / `RX 0x60xx`: mapear quais
   endereços respondem e montar o dump da faixa a partir de `0x6000`.
2. Cruzar o dump com a faixa de programa documentada do RBP (`0000–4000`) para localizar o
   programa do usuário.
3. Verificar se o topo do log da sessão mostra o F0 validando com as rodadas da v0.96 (o que
   liberaria também o `38`).
