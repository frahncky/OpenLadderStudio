# TP02 PG — primeira execução da varredura completa (v0.95) em hardware

Data: 2026-09-09, 13:45–13:47 · Motor PG Lab 1.9 · Pacote `2026.09.09.7` · COM1 · CLP em STOP

Registro da primeira execução da bateria READ-ONLY única (v0.95) em um TP02 físico.
Relatório de origem: `TP02-PG-Lab-20260909-134710.txt`.

## Resultado por etapa

| Etapa | TX | Resultado observado |
|---|---|---|
| HELLO | `43 4F 4E 2D 49 43 42 0D` | `RX []` nas 6 tentativas do perfil DTR=on/RTS=on; **`80 01 09 75` (HELLO-STOP, 239 ms)** na 5ª tentativa do perfil **DTR=on/RTS=off**, após reabertura da porta |
| F0 (matriz, 6 variantes) | `F0 00 0F` | **silêncio nas 6 variantes** (inclusive as com RTS=on) → **F0 não validado** |
| 38 ×5 | `38 00 C7` | **BLOQUEIO** — "exige F0 validado na mesma sessão"; nunca transmitido |
| 34 ×5 | `34 03 00 00 A0 28` | `RX []` (≈4009 ms cada) |
| 0A parte 1 ×5 | `0A 03 60 00 AC E6` | `RX []` |
| 0A parte 2 ×5 | `0A 03 60 AC AC 3A` | `RX []` |
| 14 ×3 | `14 00 EB` | `RX []` nas tentativas 1 e 2; **`00 00 FF` (soma FF, 269 ms) na 3ª** |
| Escutas passivas | — | nenhum byte espontâneo em nenhuma janela |

Resultado final da run: `SUCCESS`, perfil `Fallback - DTR on RTS off`, `19200 8O1`.

## Fatos

1. O TP02 estava em estado pouco responsivo: o HELLO só saiu após rearme de DTR/RTS e
   reabertura da porta, na 5ª tentativa do segundo perfil.
2. O enlace subiu em **DTR=on / RTS=off** com `80 01 09 75` (STOP), latência 239 ms.
3. O `F0 00 0F` não respondeu em nenhuma das 6 variantes da matriz, incluindo as que
   colocam RTS=on no par TX/RX. Portanto o F0 **não** foi validado nesta sessão.
4. Por consequência direta e correta da trava, os cinco `38 00 C7` foram **bloqueados**
   pelo motor, não transmitidos.
5. `34` e ambos os `0A` ficaram em `RX []` em todas as tentativas.
6. O `14 00 EB` recebeu, apenas na 3ª tentativa, o quadro **`00 00 FF`** (soma módulo
   256 = FF), latência 269 ms.

## Interpretação (hipóteses, não fatos)

- **Enquadramento de resposta `CMD=00 / LEN=n` parece se generalizar.** O F0, em captura
  anterior, respondeu `00 02 10 22 CB` = `CMD 00, LEN 2`. Agora o `14 00 EB` devolveu
  `00 00 FF` = `CMD 00, LEN 0` — uma resposta vazia/ACK. Mesma família, latência da mesma
  ordem (~250–270 ms). Ressalva: 1 ocorrência em 3; reproduzir antes de fixar. A latência
  de 269 ms (não é ruído de borda, que apareceria em t≈0) pesa a favor de emissão real.
- **O F0 mudo é provavelmente falta de tentativas, não canal morto.** O `14` respondeu no
  mesmo enlace RTS=off, então o canal não estava inerte. Somado ao padrão de "ignora
  sequências" já visto (HELLO ignorado ~9 vezes; `14` ignorado 2 vezes), 6 disparos únicos
  do F0 são poucos. Sem F0 validado, o `38`/leitura de programa nunca abre.
- O silêncio de `34`/`0A` é coerente com "sessão não avançou além do preflight" e/ou com
  um gate de senha; não há evidência suficiente para separar as duas causas aqui.

## Resposta na v0.96

A v0.96 (motor 1.10) passa a **repetir a matriz F0 em até 3 rodadas** na mesma sessão,
parando assim que uma variante devolver `00 02 10 22 CB`. É o mesmo princípio já aplicado
às leituras: dar tentativas suficientes contra as sequências de "ignora" do TP02. Nenhum
opcode novo é introduzido — cada rodada transmite apenas `F0 00 0F`, já
`READ_ONLY_VERIFIED`.

## Próximo ensaio recomendado

1. Rodar o Teste Único 2–3 vezes, de preferência com o TP02 reiniciado, buscando o enlace
   e observando se alguma rodada do F0 devolve `00 02 10 22 CB`.
2. Se o F0 validar, os `38` disparam automaticamente na mesma sessão e a leitura de
   programa passa a ser caracterizável.
3. Reproduzir o `14 00 EB → 00 00 FF` para confirmar se é resposta estável (ACK vazio) ou
   evento isolado.
