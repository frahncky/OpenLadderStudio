# TP02 PG — paginação genérica do comando 34 (PG Lab 1.20)

## Estado

A paginação da leitura de programa foi generalizada no código, mantendo o laboratório estritamente **READ-ONLY**.

Há duas camadas de evidência que não devem ser confundidas:

- **físico confirmado:** a resposta `34` observada tem `LEN=F0`, 240 bytes de payload, Região A com 80 pares HIGH/LOW e Região B com 80 bytes BRAW; o contador do pedido acompanha passos do programa nas capturas já feitas;
- **reconstrução estática/implementação:** o PC12 monta `34 03 [step_hi] [step_lo] A0 chk` a partir do contador de passos. O leitor genérico assume páginas consecutivas de 80 passos e precisa de uma captura física `>80` passos para promover essa paginação completa a fato de bancada.

## Gerador de requisições

O novo `Tp02Pg34Pager` gera o quadro de leitura para qualquer início válido entre 0 e 3999 passos:

```text
34 03 [step_hi] [step_lo] A0 chk
```

com checksum tal que:

```text
sum(quadro) mod 256 = FF
```

A sequência de páginas é:

```text
página 0 -> start = 0000 -> 34 03 00 00 A0 28
página 1 -> start = 0050 -> 34 03 00 50 A0 D8
página 2 -> start = 00A0 -> 34 03 00 A0 A0 88
página 3 -> start = 00F0 -> 34 03 00 F0 A0 38
...
```

O incremento é sempre `0x0050 = 80` passos.

## Critério de parada

O comando `38` deixou de controlar a continuação da leitura. Seu byte variável continua registrado apenas como **hint/metadado**, porque a forma física observada contém somente um byte e não é suficiente, por si só, para representar um programa de até 4000 passos.

O critério primário é o próprio programa:

```text
F-00 END = 00 70
```

Após cada página válida, o leitor procura `00 70` nos 80 pares HIGH/LOW. Ao localizar END, calcula:

```text
passo_global = start_da_página + passo_local
```

e encerra a leitura.

Se END não aparecer, a próxima página é solicitada em `start + 80`. O limite absoluto é 4000 passos / 50 páginas. Se END não for encontrado até esse limite, a execução termina explicitamente como incompleta em vez de ultrapassar a memória de programa.

## Guardas de segurança

Cada quadro dinâmico passa por validação local antes de ser transmitido:

```text
comprimento = 6
byte[0] = 34
byte[1] = 03
byte[4] = A0
start codificado = start esperado
checksum = FF
```

Cada página é solicitada **uma única vez**. Não existe retentativa automática por página.

A cadeia continua condicionada à autorização manual READ-ONLY e ao fluxo já validado:

```text
HELLO-STOP -> F0 válido -> 38 estrutural válido -> página 0 válida -> páginas seguintes
```

Não foi acrescentado qualquer comando de escrita, download, apagamento, firmware ou RUN/STOP remoto. `0F 00 F0` continua bloqueado.

## Componentes de software

```text
src/OpenLadderStudio.Core/Tp02Pg34Pager.cs
src/OpenLadderStudio.Desktop/PreparePgLabPaginationV30.ps1
tests/OpenLadderStudio.Core.Tests/Tp02Pg34PagerSelfTest.cs
```

O autoteste verifica, sem PLC:

- pedidos das páginas 0, 1, 2 e 3;
- incremento de 80 passos;
- limite de 4000 passos;
- validação `LEN=F0` e checksum;
- localização de `F-00 END` e conversão de passo local para global;
- rejeição de quadro adulterado.

## Validação física ainda pendente

O programa de 91 passos documentado em `tp02-pg-pagination-34-v119.md` continua sendo o ensaio de bancada mais econômico para confirmar a fronteira 79/80.

A implementação 1.20 não depende mais do resultado desse ensaio para existir, mas a documentação deve continuar distinguindo:

```text
PAGINAÇÃO IMPLEMENTADA / ESTATICAMENTE FUNDAMENTADA
!=
PAGINAÇÃO >80 FISICAMENTE CONFIRMADA
```

Depois de uma única captura física válida cruzando a fronteira de 80 passos, a regra pode ser promovida de hipótese estática forte para evidência física.
