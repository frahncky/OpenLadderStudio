# TP02 PG — nota de segurança v0.40

## Descoberta

A análise estática do executável original `pc12.exe` (PC12 2.1) mostrou que o quadro `F0 00 0F` **não é uma etapa genérica de handshake**.

No recurso de menu do PC12, o comando de ID `0x142` é **PLC → Clear All Memory**. A tabela de eventos associa esse ID ao handler em `0x004AEB65`, que chama a rotina em `0x0046F0F0`. Essa rotina prepara um quadro de 3 bytes com os valores internos `0F 00 F0`; a captura física realizada no TP02 confirmou que o envio no fio aparece como `F0 00 0F`.

A mesma sequência real de bancada mostrou:

- HELLO: `43 4F 4E 2D 49 43 42 0D` (`CON-ICB<CR>`)
- resposta válida: `C0 01 09 35` — soma módulo 256 = `FF`
- quadro anteriormente enviado: `F0 00 0F`
- resposta observada: `40 02 10 22 8B` — soma módulo 256 = `FF`

A resposta `40 02 10 22 8B` fica registrada como evidência histórica, mas **não deve ser provocada novamente automaticamente**.

## Regra de segurança a partir da v0.40

A ferramenta de diagnóstico PG pode transmitir somente `CON-ICB<CR>` durante a validação do link. O quadro `F0 00 0F` fica bloqueado no código. Também permanecem bloqueados RUN, STOP, escrita, download, Clear Program, Clear All Memory e demais operações que alterem o PLC.

A próxima etapa de engenharia reversa deve partir de análise estática adicional do PC12 original e/ou captura passiva do tráfego real, sem adivinhar comandos.
