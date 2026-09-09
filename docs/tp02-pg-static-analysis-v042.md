# TP02 PG — análise estática v0.42

## Correção da classificação de `F0 00 0F`

A classificação anterior de `F0 00 0F` como **Clear All Memory** foi revisada.

No `pc12.exe` original, a rotina em torno de `0x0046F0F0` prepara no buffer de transmissão os três bytes:

`0F 00 F0`

A rotina genérica de comunicação em torno de `0x0046F5E6` repassa o endereço do buffer `0x004FA7A8` e o comprimento armazenado em `0x004FA8AC` diretamente à rotina de transmissão serial. Não foi observada inversão da ordem dos bytes nesse caminho.

Portanto, a evidência estática disponível não sustenta a afirmação de que o quadro de fio `F0 00 0F` seja o comando Clear All Memory. A partir da v0.42, `F0 00 0F` passa a ser tratado apenas como **quadro não classificado** e continua bloqueado por segurança.

## Handshake confirmado fisicamente

O único TX automático permitido permanece:

`43 4F 4E 2D 49 43 42 0D` = `CON-ICB<CR>`

Duas respostas HELLO foram observadas fisicamente no mesmo TP02 e ambas fecham checksum por soma módulo 256 igual a `FF`:

- `C0 01 09 35`
- `80 01 09 75`

A diferença de `0x40` no primeiro byte ainda não tem significado atribuído.

## Candidato de leitura de programa — ainda não transmitir

A análise da rotina que referencia a mensagem `Read PLC Program...` mostra um bloco que prepara seis bytes no buffer:

`34 03 00 00 A0 28`

A soma módulo 256 desse bloco também resulta em `FF`.

Entretanto, a mesma rotina executa outras trocas e verificações antes desse bloco. Por isso, `34 03 00 00 A0 28` é registrado apenas como **candidato estático de leitura de programa**, não como comando pronto para teste isolado.

## Regra operacional

Até que a sequência completa anterior à leitura seja reconstruída, a ferramenta PG continua transmitindo somente `CON-ICB<CR>`. `F0 00 0F`, `0F 00 F0`, comandos candidatos de leitura, RUN, STOP, escrita, download e apagamento permanecem bloqueados.
