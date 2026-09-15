# TP02 Full Protocol Capture

Ferramenta de campanha única para coletar TX/RX físico do protocolo PG do WEG TP02 sem misturar operações destrutivas com o validador READ-ONLY.

Executável: `OpenLadderTP02FullCapture.exe`

## Modos

`SAFE` transmite somente `CON-ICB`, `F0`, `38`, `34` e leituras `0A` conhecidas. Faz leitura multipágina do programa até `F-00 END`, varredura completa de X/Y/C/SC/V/D/WC/FL/WS, RTC e scan time.

`LAB` executa tudo do SAFE e acrescenta, somente após confirmação `LAB-ISOLADO`: `01` STOP, `02` RUN, `14` security preflight, escrita `09` reversível em `V1024`, regravação do mesmo RTC e `35` Set/Reset reversível em `C2048`. O valor original de V1024 e o estado original de C2048 são restaurados e verificados. O RUN é seguido imediatamente por STOP e confirmação por HELLO. Use somente com saídas/cargas fisicamente desconectadas.

`DESTRUCTIVE` exige confirmação `APAGAR-TP02`. Além do LAB, captura `11` Clear Data, `04` Clear System, `03` Clear Program e `0F` Clear All, salvando snapshots depois de cada operação. Com `--eeprom`, também executa `13` PLC -> EEPROM PACK e `12` EEPROM PACK -> PLC. Este modo deixa o controlador alterado e não executa restore automático.

## Bloqueios permanentes

- `37` BIOS Refresh nunca pode ser enviado por esta ferramenta.
- `33` PG33 não é retransmitido pela campanha; já existe evidência física separada e não há retry cego.
- SAFE não aceita nenhum comando de escrita/controle.
- LAB não aceita clears nem EEPROM.
- EEPROM exige simultaneamente modo DESTRUCTIVE e `--eeprom`.

## Uso

Modo interativo:

```bat
OpenLadderTP02FullCapture.exe
```

SAFE direto:

```bat
OpenLadderTP02FullCapture.exe --port COM1 --mode SAFE
```

LAB isolado:

```bat
OpenLadderTP02FullCapture.exe --port COM1 --mode LAB
```

DESTRUCTIVE, sem EEPROM:

```bat
OpenLadderTP02FullCapture.exe --port COM1 --mode DESTRUCTIVE
```

DESTRUCTIVE com EEPROM PACK instalado:

```bat
OpenLadderTP02FullCapture.exe --port COM1 --mode DESTRUCTIVE --eeprom
```

## Evidências geradas

Cada sessão é salva em `Documentos\OpenLadder Studio\TP02 Full Protocol Capture\<timestamp>` e contém:

- `session.log`;
- TX e RX de cada operação em `.bin` e `.hex`;
- páginas PG34 completas do backup de programa;
- snapshots PG0A das áreas de memória;
- `responses.csv` com latência, TX, RX e frames válidos encontrados;
- `protocol-report.txt` consolidado;
- `RESTORE_REQUIRED.txt` quando o modo DESTRUCTIVE é executado.

A campanha usa primeiro o perfil COM/DTR/RTS já aprendido pelo OpenLadder e, se necessário, tenta as quatro combinações DTR/RTS. Perfil serial: 19200, 8O1.

## Objetivo

Uma única sessão LAB deve fechar as respostas físicas ainda pendentes de `01`, `02`, `09`, `14` e `35`, além de registrar efeitos antes/depois. Uma sessão DESTRUCTIVE em PLC sacrificial pode fechar `03`, `04`, `0F`, `11` e, quando houver EEPROM PACK, `12/13`. O `37` permanece deliberadamente fora da campanha devido ao risco de corrupção de firmware.
