# TP02 PG — emulação dos comandos ainda não classificados

Data: 2026-09-12. Binário: `pc12.exe` v2.1, SHA-256
`05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0`.

Toda a análise foi offline com Unicorn e desmontagem cruzada. A rotina serial foi
interceptada; nenhuma porta COM foi aberta e nenhum quadro chegou a um PLC.

## Resultado reproduzido

A varredura alcançou 182 chamadas da rotina TX em 32 funções. O conjunto de
opcodes permaneceu: `01`, `02`, `03`, `04`, `09`, `0A`, `0F`, `11`, `13`, `14`,
`33`, `34`, `35`, `37`, `38` e `F0`, além de `CON-ICB<CR>`.

### Comando 03

O helper `0x0046F07A` monta `03 00 FC`. Foram encontrados cinco chamadores:

- uma ação independente em `0x004AEA95`, protegida por “PLC Must Be Stop !” e
  “Are You Sure !!”;
- quatro tentativas consecutivas no fluxo `Write PLC Program`, imediatamente
  antes da construção/transmissão dos blocos `33`.

Conclusão: `03` é uma preparação da área de programa, possivelmente limpeza ou
abertura da sessão de gravação. O efeito exato continua dependente do TP02 físico.

### Comandos 04 e 11

`04 00 FB` e `11 00 EE` possuem handlers adjacentes e estruturalmente simétricos.
Ambos exigem PLC em STOP, usam resposta curta e repetem em falha. Nenhuma string
no executável distingue de forma confiável a função final. Permanecem descritos
como operações de modo/serviço A e B.

### Famílias 09 e 0A

Foram encontrados oito construtores `09`, com payloads de 4, 5 ou 17 bytes, em
fluxos de escrita/configuração, senha e acesso a áreas variáveis. O `0A` aparece
em 14 construtores ligados explicitamente às leituras V, D, WC, FILE e sistema.
Endereço e quantidade são campos dinâmicos.

### Comando 35

O construtor com LEN 3 foi confirmado novamente, mas o caminho emulado não
produziu contexto suficiente para atribuir uma função. Continua desconhecido.

## Limite técnico

O emulador revela o comportamento do PC12, não o firmware do TP02. Portanto,
nenhuma dessas novas classificações libera transmissão. `03`, `04`, `09`, `11`
e `35` continuam bloqueados.
