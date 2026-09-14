# OpenLadder Studio v1.53 — readback PG34 multipágina

## Evidência física que motivou a correção

Na primeira gravação real do projeto de 323 machine words pela v1.52, o TP02 aceitou todos os **17 blocos PG33** com o ACK físico conhecido:

```text
00 00 FF
```

A falha ocorreu somente depois da escrita, durante o readback automático. O leitor canônico legado ainda procurava `F-00 END` apenas na primeira página PG34 (80 passos). Como o projeto longo possui `END` global no passo 322, a verificação terminava incorretamente com a mensagem de END ausente na primeira página.

Essa evidência separa duas conclusões:

- a sequência PG33 multibloco foi aceita pelo PLC bloco a bloco;
- o verificador pós-gravação ainda não estava preparado para programas com mais de 80 passos.

## Correção v1.53

`ReadCanonicalSnapshotOnOpenPort` passa a ler o programa por páginas PG34 sucessivas na mesma sessão PG já qualificada:

```text
start 0   -> 34 03 00 00 A0 28
start 80  -> 34 03 00 50 A0 D8
start 160 -> 34 03 00 A0 A0 88
start 240 -> 34 03 00 F0 A0 38
start 320 -> 34 03 01 40 A0 E7
```

Cada página transporta até 80 passos. O leitor concatena os planos HIGH/LOW/BRAW em ordem global e somente encerra quando encontra `F-00 END` (`00 70`).

Para o projeto físico atual de 323 words, o resultado esperado é:

- páginas: 5;
- starts: 0, 80, 160, 240 e 320;
- palavras: 323;
- END: passo 322;
- comparação final: idêntica ao projeto compilado.

## Segurança

A correção v1.53 é de leitura. O readback multipágina:

- não transmite PG33;
- não retransmite os 17 blocos já gravados;
- não executa restore;
- não envia RUN/STOP remoto;
- não envia Clear All, `0x09` ou WBP.

Se o END não for encontrado em até 4000 passos, a leitura é abortada e o PLC deve permanecer em STOP para diagnóstico.

## Fluxos beneficiados

A troca é feita no leitor canônico compartilhado. Assim, passam a aceitar programa longo:

- verificação automática após PG33 multibloco;
- botão **VERIFICAR GRAVAÇÃO** (read-only);
- leituras canônicas usadas pelos fluxos PG que já dependiam desse método.

O caminho legado de gravação de bloco único mantém suas restrições anteriores, inclusive os guardrails de restore para programas pequenos.

## Procedimento após instalar v1.53

Como os 17 blocos do projeto de 323 words já receberam ACK na sessão física da v1.52, **não é necessário regravar** para testar esta correção. Mantenha o TP02 em STOP e use **VERIFICAR GRAVAÇÃO**. O objetivo é obter o readback das cinco páginas e confirmar as 323 words até END=0322.
