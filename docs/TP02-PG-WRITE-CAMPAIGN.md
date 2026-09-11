# Campanha de descoberta da escrita PG do WEG TP02

Objetivo: descobrir de forma determinística a sequência de comandos usada pelo PC12 para gravar cada classe de dados no TP02, usando somente o emulador PG e portas COM virtuais.

## Regras da campanha

- PC12 original em uma ponta do par COM virtual.
- `OpenLadderTP02Emulator.exe` na outra ponta.
- Não conectar o emulador à COM física do PLC.
- Reiniciar o emulador antes de cada caso para gerar captura RAW independente.
- Marcar somente uma opção de `PLC > Write` por caso.
- Alterar apenas um valor entre A e B.
- Rodar `AnalyzeLatestTp02Capture.bat` após cada sessão.
- Preservar os arquivos `*-raw.bin`, `*.frames.csv` e o log textual.

## Classes de escrita expostas pelo PC12

O PC12 permite transferir separadamente:

1. `Write Program Data` — programa executável.
2. `Write System Data` — memória de sistema `WSxxx`.
3. `Write Vxxx Data` — registradores `Vxxx`.
4. `Write Dxxx Data` — registradores `Dxxx`.
5. `Write WCxxx Data` — registradores `WCxxx`.
6. `Write FLxxx Data` — arquivos de texto `FL001..FL130`.

Isso permite identificar o protocolo por classe, sem misturar áreas de memória.

## Matriz mínima

| ID | Operação PC12 | Variação controlada | O que procurar |
|---|---|---|---|
| R0 | Read Program Data | nenhuma | sequência-base já conhecida |
| W1A | Write Program Data | programa mínimo A | primeiro opcode desconhecido e sequência de download |
| W1B | Write Program Data | mudar X000 para X001 | bytes de endereço do operando |
| W1C | Write Program Data | mudar X000 para Y000 | código de tipo de dispositivo |
| W1D | Write Program Data | acrescentar 1 rung | tamanho/paginação/END |
| W2A | Write System Data | WS sem alteração adicional | opcode da área WS |
| W2B | Write System Data | alterar somente 1 WS | endereço e formato da System Memory |
| W3A | Write Vxxx Data | V001=1 | opcode/endereço de V |
| W3B | Write Vxxx Data | V001=2 | posição e endianess do valor |
| W4A | Write Dxxx Data | D001=1 | opcode/endereço de D |
| W4B | Write Dxxx Data | D001=2 | posição e endianess do valor |
| W5A | Write WCxxx Data | WC001=1 | opcode/endereço de WC |
| W5B | Write WCxxx Data | WC001=2 | posição e endianess do valor |
| W6A | Write FLxxx Data | FL001="A" | opcode/formato de texto |
| W6B | Write FLxxx Data | FL001="B" | byte de caractere e tamanho |

## Ordem recomendada

Executar primeiro:

```text
R0
W1A
W1B
W1C
W1D
```

Com isso deve ser possível fechar o fluxo de `Write Program Data`, que é a prioridade do OpenLadder.

Depois executar:

```text
W2A/W2B
W3A/W3B
W4A/W4B
W5A/W5B
W6A/W6B
```

Esses casos fecham as demais áreas de dados do PC12.

## Programa mínimo recomendado

Caso A:

```text
STR X000
OUT Y000
END
```

Caso B:

```text
STR X001
OUT Y000
END
```

Caso C:

```text
STR Y000
OUT Y001
END
```

O objetivo não é testar a lógica, e sim gerar diferenças pequenas e localizadas no payload de gravação.

## Comparação

Exemplo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\AnalyzeTp02Capture.ps1 `
  -Path .\captures\W1A-raw.bin `
  -Compare .\captures\W1B-raw.bin
```

Interpretação:

- mesmo `CMD`, mesmo `LEN`, poucos bytes diferentes: provavelmente payload/endereço;
- `CMD` diferente entre classes: provável opcode específico da área;
- `LEN` cresce com o número de rungs: provável bloco de programa variável;
- quantidade de frames cresce: provável paginação;
- frame curto somente no início/fim: provável comando de abertura/finalização;
- resposta genérica `00 00 FF` aceita e PC12 continua: forte candidato a ACK simples, ainda não prova a resposta real do PLC.

## Estado STOP

O PC12 documenta que o aplicativo só pode ser transferido quando o controlador está em STOP. No emulador, a resposta padrão do handshake deve portanto ser o estado observado correspondente ao TP02 parado:

```text
CON-ICB<CR>
<- 80 01 09 75
```

A resposta `C0 01 09 35` deve ser usada apenas em testes específicos de estado.

## Fases posteriores

Somente depois de fechar `Write Program Data` estudar, ainda contra o emulador:

- RUN;
- STOP;
- Password;
- EEPROM -> PLC;
- PLC -> EEPROM;
- Compare Program;
- Clear System;
- Clear Data;
- Clear Program;
- Clear All Memory.

Os comandos `Clear*` permanecem proibidos contra o PLC físico durante a engenharia reversa.

## Critério para considerar Write Program Data fechado

O fluxo será considerado suficientemente fechado para implementação no OpenLadder quando conhecermos:

1. pré-condições e estado exigido;
2. comando de início;
3. formato de endereço e tamanho;
4. formato de cada bloco de programa;
5. paginação;
6. checksum;
7. resposta esperada a cada etapa;
8. indicação de erro/NAK;
9. comando de finalização;
10. forma de validar a gravação por leitura/compare.
