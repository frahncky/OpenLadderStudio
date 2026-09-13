# TP02 PG — SET/RESET, registradores, EEPROM e relógios

Data: 2026-09-12. Continuação da identificação do PC12 v2.1 após a v1.41.

Atualização posterior: o campo auxiliar do RTC foi identificado como V1022,
dia da semana. As variantes de memória e o alcance atualizado estão no
[relatório seguinte](tp02-pg-memory-variants-2026-09-12.md). Os resultados
abaixo preservam o estado e as entradas sintéticas da primeira rodada.

Binário analisado: `pc12.exe` extraído do ZIP enviado. SHA-256:
`05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0`.
O executável incluído no repositório tem o mesmo hash.

## Resultados e correções

| Comando ou variante | Identificação no PC12 | Evidência desta rodada |
|---|---|---|
| `35 03 ... CHK` | SET/RESET de um bit X, Y ou C | Diálogo 41, eventos OWL, codificador e handler originais emulados |
| `09 05 ... CHK` | Escrita de um registrador V, D ou WC, com valor de 16 bits | Diálogo 43 e handler original emulado |
| `12 00 ED` | EEPROM PACK → PLC | Diálogo 30, seleção nativa e construtor original emulados |
| `13 00 EC` | PLC → EEPROM PACK | Diálogo 30, seleção nativa e construtor original emulados |
| `14 00 EB` | Etapa enviada após a comparação local de senha | Cinco construtores; ramo igual/diferente emulado em um deles |
| `0A 03 60 00 06 8C` | Leitura dos tempos de varredura | Construtor, parser e distribuição na tela Scan Time |
| `0A 03 53 F9 0E 98` | Leitura do relógio de tempo real | Construtor, parser e distribuição na tela Real Time Clock |
| `09 11 53 F9 0E ... CHK` | Escrita do relógio de tempo real | Construtor original e formato dos sete campos emulados |

**Correções da classificação anterior:** `13` é transferência para EEPROM,
não senha. `35` é SET/RESET, não apenas troca genérica de dados do monitor.
`12` faltava no inventário. A proximidade de um comando com a checagem de senha
não determina sua função: o handler EEPROM também contém o ramo de senha.

Esta entrega contém pesquisa, scripts e resultados. Não altera o catálogo
compilado da v1.41, permissões de transmissão, versão, release ou GitHub.

## 35 — campos completamente decodificados para X/Y/C

Formato de seis bytes:

```text
35 03 A_H A_L B CHK
```

Para o endereço textual de número `n`, com numeração iniciando em 1:

```text
indice_byte = (n - 1) // 8
indice_bit  = (n - 1) % 8
A_H = banco OR (indice_byte >> 8)
A_L = indice_byte AND FFh
B   = indice_bit OR (80h se SET; 00h se RESET)
CHK = (FFh - soma dos cinco bytes anteriores) AND FFh
```

| Área | Banco do PG35 | Faixa oferecida pelo diálogo 41 |
|---|---|---|
| X | `50h` | X0001–X0384 |
| Y | `10h` | Y0001–Y0384 |
| C | `30h` | C0001–C2048 |

| Entrada | Quadro montado pelo PC12 |
|---|---|
| SET X0001 | `35 03 50 00 80 F7` |
| SET X0008 | `35 03 50 00 87 F0` |
| SET X0009 | `35 03 50 01 80 F6` |
| SET Y0001 | `35 03 10 00 80 37` |
| RESET C2048 | `35 03 30 FF 07 91` |

O recurso 41 contém os botões 104 `SET(ON)` e 105 `RESET(OFF)`.
As entradas OWL em `004E104C`/`004E1064` apontam para
`00473932`/`00473CF6`. Eles produzem respectivamente valor textual 1/0
e a mensagem para `004C2141`. Esse handler chama `004C27C7`, que converte
endereço e valor, e monta o quadro em `004C2275`.

Foram testados **todos os 2.816 endereços dessa tela, em SET e RESET**:
5.632 quadros. A rotina original de conversão hexadecimal também foi executada,
sem substituir os campos do protocolo por resultados do modelo.

Isso comprova a codificação e a intenção do PC12. Não determina persistência
do SET/RESET durante a execução Ladder nem aceitação de cada endereço pelo
hardware. As faixas da interface não representam a quantidade de bornes do TP02.
Não estender este mapeamento a S/SC apenas por existirem caminhos no helper.

## 09 — escrita de um registrador pelo monitor

```text
09 05 A_H A_L 02 V_H V_L CHK
offset = n - 1
A_H = banco OR (offset >> 8)
A_L = offset AND FFh
```

`V_H V_L` é o valor de 16 bits, byte mais significativo primeiro.
O campo `02` acompanha os dois bytes do valor. O endereço avança por índice
de registrador neste construtor; o PC12 não multiplica `n - 1` por 2.

| Área | Banco do PG09 | Faixa do diálogo 43 e de suas checagens |
|---|---|---|
| V | `50h` | V0001–V1024 |
| D | `90h` | D0001–D2048 |
| WC | `70h` | WC001–WC912 |

Os bancos são específicos do comando: não confundir V/PG09 com X/PG35.

Exemplos nativos:

```text
V0001 = 1234h : 09 05 50 00 02 12 34 59
D0257 = 8000h : 09 05 91 00 02 80 00 DE
WC912 = FFFFh : 09 05 73 8F 02 FF FF EF
```

O handler `004C240F` lê as strings do monitor, monta o quadro em `004C2502`
e chama TX em `004C26CE`. Foram confrontados 168 cenários de endereço/valor,
incluindo a passagem de `00FFh` para `0100h`, o bit de sinal e `FFFFh`.
As quatro guardas adicionais confirmaram ausência de TX com estado/mensagem
diferentes dos esperados pelos dois handlers.

## 12/13 — os dois sentidos da EEPROM

O menu 306 `EEPROM` está ligado ao handler `004BBEDB` pela entrada OWL
`004F06A4`. O handler abre o diálogo 30.

| Controle do diálogo | Texto original | Handler do controle | Seleção | Quadro |
|---|---|---|---|---|
| 102 | `EEPRON PACK ---> PLC` | `00474515` | 1 | `12 00 ED` |
| 103 | `PLC ---> EEPROM PACK` | `00474540` | 2 | `13 00 EC` |

O campo de seleção em `dialogo+19h` é lido em `004BC3C3`.
Os dois ramos de montagem são `004BC3C9` e `004BC3E0`. A emulação executou
primeiro o handler de cada opção e depois esse bloco até a entrada de TX.
Não executou a autenticação, a confirmação modal ou a transferência completa.

A varredura antiga agrupava escritas próximas em um único quadro. Como esses
dois ramos escrevem nas mesmas posições, o `13` ocultava o `12` nesse resumo.
Uma nova enumeração das escritas imediatas no primeiro byte encontrou
**17 valores de opcode**, além do handshake textual já conhecido:

```text
01 02 03 04 09 0A 0F 11 12 13 14 33 34 35 37 38 F0
```

Foram encontrados 10 locais de construção imediata de `09` e 16 de `0A`.
São contagens de locais no binário, não de funções diferentes nem uma prova
de que todos os comandos possíveis do firmware foram descobertos.

## 14 — etapa condicional de senha

Os cinco locais de montagem são `004AF763`, `004B19E8`, `004B687F`,
`004BBD3B` e `004BC238`. No trecho testado, a comparação local final em
`004BC220` permite montar `14 00 EB` apenas quando as strings conferem;
o ramo de divergência chega a `004BC2FA` sem transmitir.

Foram usadas strings sintéticas após a transformação local; não foram extraídas
senhas nem reproduzido o fluxo de autenticação inteiro. O quadro não contém
senha. A intenção de habilitar a continuação da operação é inferida do controle
de fluxo. Seu efeito interno no firmware continua sem confirmação nesta rodada.

## 0A — tempos de varredura

Pedido: `0A 03 60 00 06 8C`. Construtor em `004BF443`.
O parser a partir de `004BF5C7` interpreta seis bytes de dados assim:

| Posição no payload | Tipo | Conteúdo exibido |
|---|---|---|
| 0–1 | inteiro de 16 bits, byte alto primeiro | Tempo atual |
| 2–3 | inteiro de 16 bits, byte alto primeiro | Tempo mínimo |
| 4–5 | inteiro de 16 bits, byte alto primeiro | Tempo máximo |

A ordem no quadro é **atual, mínimo, máximo**. A tela apresenta atual, máximo,
mínimo. Essa troca foi verificada no caminho nativo que preenche os controles
102, 107 e 108 do diálogo 45. A unidade `ms` vem dos rótulos da interface;
não houve medição física da temporização.

## 0A/09 — relógio de tempo real

Leitura: `0A 03 53 F9 0E 98`. São 14 bytes de dados, interpretados como
sete inteiros de 16 bits com o byte mais significativo primeiro.

| Posição no payload | Campo ligado à tela 33 |
|---|---|
| 0–1 | Segundo |
| 2–3 | Minuto |
| 4–5 | Hora |
| 6–7 | Dia do mês |
| 8–9 | Dia da semana (V1022), identificado na etapa seguinte |
| 10–11 | Mês |
| 12–13 | Ano, editado com dois algarismos |

O parser está em `004BC6A7`; os campos são distribuídos para a tela em
`0047210D`. O campo auxiliar é lido e reenviado, mas não tem controle de edição
no diálogo. A etapa seguinte identificou V1022 como dia da semana, de 0 a 6;
veja [variantes de memória e dia da semana](tp02-pg-memory-variants-2026-09-12.md).
O valor sintético 7 usado abaixo testa preservação do campo pelo PC12 e não
constitui uma recomendação de escrita.

Escrita: `09 11 53 F9 0E`, seguida dos sete pares `00 valor` e checksum,
totalizando 20 bytes. O construtor `004BC811` usa valores numéricos binários,
não BCD: 59 segundos vira `00 3B`. O byte alto de cada campo escrito é zero.

Exemplo sintético com segundo=59, minuto=34, hora=12, dia=25, auxiliar=7,
mês=8 e ano=26:

```text
09 11 53 F9 0E 00 3B 00 22 00 0C 00 19 00 07 00 08 00 1A E0
```

Não interpretar automaticamente 26 como 2026: a convenção de século do firmware
não foi estabelecida. A identificação também não prova que todo modelo TP02
possui RTC instalado.

## Reprodução e alcance dos testes

Na raiz do repositório, com Python e as dependências instaladas:

```bash
python3 -m pip install unicorn==2.1.4 pefile==2024.8.26
python3 scripts/emulate_pc12_monitor_commands.py --exhaustive
python3 scripts/emulate_pc12_eeprom_commands.py
python3 scripts/emulate_pc12_clock_commands.py
```

| Script | Cenários aprovados |
|---|---:|
| Monitor SET/RESET e registradores | 5.804 |
| EEPROM e comparação final PG14 | 4 |
| Scan time e RTC | 11 |
| **Total** | **5.819** |

Os scripts conferem o hash do binário e os nomes das funções importadas usadas
como stubs. Executam as instruções originais dos blocos identificados; apenas
conversões de strings, relógio e chamadas visuais declaradas são substituídas.
A emulação para antes de executar a primeira instrução da rotina TX.

Os testes de RX começam no parser após as validações de transporte, com dados
sintéticos. Eles comprovam interpretação e mapeamento na UI, não ACK, timeout
ou resposta real de firmware. Não foi aberta nenhuma porta serial.

Resultados completos:

- [Monitor](data/pc12-monitor-commands-emulation.txt)
- [EEPROM/PG14 e inventário de locais](data/pc12-eeprom-commands-emulation.txt)
- [Tempos de varredura e RTC](data/pc12-clock-commands-emulation.txt)

Pendências desta rodada e sua evolução: ver o
[relatório de variantes 09/0A](tp02-pg-memory-variants-2026-09-12.md).
A contagem de testes aprovados não é percentual de compatibilidade com o TP02.
