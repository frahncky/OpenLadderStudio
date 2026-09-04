# Laboratório PG TP02 — arquitetura v0.43

A partir da v0.43, o diagnóstico do protocolo PG deixa de depender de um executável novo para cada hipótese de engenharia reversa.

## Motor permanente

O executável `OpenLadderTP02PgLab.exe` contém o motor de teste, responsável por:

- varrer perfis seriais, DTR e RTS;
- repetir ciclos e rearmar as linhas seriais sem transmitir dados;
- registrar TX, RX bruto, RX sem eco, soma módulo 256 e tempos;
- localizar automaticamente subquadros cuja soma módulo 256 fecha em `FF`;
- parar a sequência quando surge uma resposta inesperada, quando o pacote assim determina;
- executar captura passiva após o handshake;
- salvar relatório completo em TXT e JSON em `%LOCALAPPDATA%\OpenLadderStudio\PG-Lab\Reports`;
- atualizar o pacote de testes sem reinstalar o OpenLadder Studio.

## Pacote atualizável

O arquivo `TP02-PG-Tests.json` define perfis, respostas conhecidas e etapas. O laboratório tenta obter automaticamente a cópia mais recente do arquivo na branch `main` e mantém cache local. Se a atualização falhar, utiliza o último pacote válido ou a cópia instalada.

Assim, novas respostas, tempos, perfis e etapas de leitura podem ser adicionados ao pacote sem publicar uma nova versão do aplicativo.

## Safety Gate

O motor possui regras próprias, além das regras do pacote:

- `HANDSHAKE`: só aceita como TX automático `43 4F 4E 2D 49 43 42 0D` (`CON-ICB<CR>`);
- `PASSIVE`: nunca transmite;
- `CANDIDATE`: apenas documenta a hipótese, nunca transmite;
- `BLOCKED`: nunca transmite;
- `READ_ONLY_VERIFIED`: só pode transmitir quando o usuário marca explicitamente a autorização e o quadro também consta em `readOnlyAllowlist` do pacote.

Os quadros `0F 00 F0` e `F0 00 0F` permanecem bloqueados internamente no motor neste estágio.

## Pacote inicial

O pacote inicial reconhece como respostas de HELLO fisicamente observadas:

- `C0 01 09 35`;
- `80 01 09 75`.

Ambas fecham soma módulo 256 igual a `FF`.

Também registra, sem transmitir:

- `F0 00 0F` como quadro ainda não classificado;
- `34 03 00 00 A0 28` como candidato estático relacionado à rotina `Read PLC Program`;
- `0F 00 F0` como bloqueado, associado à rotina `Clear All Memory` na análise estática do PC12 original.

## Fluxo de trabalho daqui para frente

Quando surgir nova evidência, deve-se preferir atualizar `TP02-PG-Tests.json`. Uma nova versão do OpenLadder Studio só será necessária quando o próprio motor precisar de uma capacidade que ainda não exista.
