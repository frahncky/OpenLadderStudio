# TP02 PG — mapa de menus, handlers e comandos

Data: 2026-09-12. Análise offline do `pc12.exe` v2.1.

## Método

O recurso `RT_MENU` 1 foi decodificado diretamente. Em seguida, a tabela de
respostas OWL foi localizada na imagem e cruzada com os handlers que chamam os
construtores curtos PG. Nenhum código do PC12 foi executado fora do Unicorn e
nenhuma porta serial foi aberta.

## Correspondências fechadas

| Menu | ID | Handler | Helper | Quadro |
|---|---:|---:|---:|---|
| Clear System | 309 / `0x135` | `0x004AE346` | `0x0046F1DC` | `04 00 FB` |
| Clear Data | 310 / `0x136` | `0x004AE491` | `0x0046F166` | `11 00 EE` |
| Clear Program | 321 / `0x141` | `0x004AEA95` | `0x0046F07A` | `03 00 FC` |
| Clear All Memory | 322 / `0x142` | `0x004AEB65` | `0x0046F0F0` | `0F 00 F0` |

Os quatro handlers exigem STOP. Clear Program e Clear All pedem confirmação
explícita. O helper `03` também é chamado quatro vezes no preflight de Write PLC
Program, fechando a relação entre limpeza/preparação do programa e os blocos 33.

## Comando 35

O quadro é construído no handler `0x004C2141`, acionado por uma mensagem interna
de comunicação (`0x22C`). O payload possui exatamente três bytes copiados do
objeto de sessão, seguido de checksum. O handler trata timeout, checksum e
repetição e pertence ao fluxo assíncrono de monitoramento/comunicação. Isso
permite classificá-lo como troca de dados do monitor, mas não determina ainda a
semântica individual dos três bytes.

## Consequência

`03`, `04` e `11` deixam de ser comandos sem nome. Continuam bloqueados porque
são operações destrutivas. O `35` permanece bloqueado por ter semântica apenas
parcial.
