# TP02 — campanha de validação física READ-ONLY (v1.48)

## Objetivo

Transformar a reconstrução offline do protocolo PG do PC12 em evidência física reproduzível no WEG TP02, começando apenas por operações de leitura.

## Ligação e perfil serial

- TP02 conectado pelo cabo/conversor usado para PG/TP-232PG;
- 19200 bit/s;
- 8 bits;
- paridade ímpar (8O1);
- 1 stop bit;
- DTR=OFF;
- RTS=OFF;
- handshake de hardware desativado.

## Ferramenta

Execute `OpenLadderTP02PhysicalValidator.exe` ou use o atalho **Validação física TP02 (READ-ONLY)** instalado com o OpenLadder Studio.

Selecione a COM e clique em **Executar campanha READ-ONLY**.

## Sequência transmitida

A ferramenta contém uma allowlist; somente os quadros abaixo podem chegar a `SerialPort.Write`:

1. `CON-ICB<CR>`;
2. `F0 00 0F`;
3. `38 00 C7`;
4. `34 03 00 00 A0 28` — PG34 página inicial;
5. `34 03 00 50 A0 D8` — PG34 START=80;
6. PG0A para V001;
7. PG0A para D001;
8. PG0A para WC001;
9. PG0A para FL001;
10. PG0A para RTC;
11. PG0A para tempos de varredura.

Qualquer quadro fora da allowlist provoca exceção antes da escrita serial.

## Operações explicitamente proibidas nesta campanha

- `09` — escrita de memória/RTC/arquivos;
- `33` — Write Program;
- `35` — SET/RESET / ramos ainda sensíveis;
- RUN;
- STOP remoto;
- Clear System/Data/Program/All;
- `14` — gate protegido;
- EEPROM;
- qualquer opcode não listado acima.

## Arquivos gerados

A sessão é salva em:

`Documentos\OpenLadder Studio\TP02 Physical Validation\AAAAMMDD-HHMMSS-mmm\`

Arquivos principais:

- `session.log` — sequência cronológica;
- `hardware-validation-summary.txt` — PASS/FAIL por operação;
- `*-tx.hex` — quadro transmitido;
- `*-rx.hex` — burst bruto recebido;
- `*-frame.hex` — quadro válido extraído quando aplicável.

## Critérios de evidência

### HELLO/F0/38

Conta como confirmação física quando a resposta esperada é localizada no burst real, usando o mesmo perfil serial registrado na sessão.

### PG34 página 0

Conta como confirmação física quando chega resposta `LEN=F0` com soma de 8 bits igual a `FF`.

### PG34 START=80

Uma resposta válida confirma que o PLC aceita o segundo endereço de página. Para provar de forma conclusiva a paginação do programa, o PLC deve conter um programa com mais de 80 passos, de modo que a página 80 contenha dados efetivamente pertencentes ao trecho seguinte do programa ou o `F-00 END` apareça com índice global >= 80.

### PG0A

Cada leitura deve retornar quadro completo com comprimento esperado e checksum válido. Para V/D/WC o relatório também mostra o valor de 16 bits decodificado. FL preserva os 20 bytes brutos. RTC e scan-time são decodificados em campos.

## Como fechar a evidência

Depois da campanha, preservar a pasta inteira sem editar os arquivos. A comparação deve usar TX, RX bruto, frame extraído, timestamp e estado informado pelo HELLO.

Somente depois dessa etapa os comandos que podem modificar estado devem ser considerados para uma campanha física separada e controlada.
