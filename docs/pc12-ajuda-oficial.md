# PC12 original — documentação oficial extraída dos arquivos de ajuda

Data: 2026-09-12

Registro da exploração do **programa original** distribuído como
`PC12_v2.1_Windows7_v3_portatil`. Ferramenta: `scripts/extract_pc12_help.py`.
Apenas leitura dos arquivos; nada é executado e nada é alterado.

## 1. O pacote portátil não traz binário novo

O pacote portátil v3 foi conferido arquivo por arquivo contra o que já está
versionado em `src/OpenLadderStudio.Desktop/`. Todos os binários e arquivos de
ajuda são **byte a byte idênticos** aos do repositório:

```text
pc12.exe      sha256 05c7d4f9bcc8c1307a1ea72c1d66e81741873c4f242ede0cc7d62b3fe91448b0
Tp022.hlp     sha256 de3aa9e7...  (idêntico a PC12HELP.HLP)
BDS52F.DLL / OWL52F.DLL / bwcc32.dll / cw3230.dll / HELPDLG.HLP — idênticos
```

Só existem no pacote, e não no repositório, os arquivos de conveniência do
próprio empacotamento: `INICIAR_PC12.bat`, `RESETAR_ULTIMO_ARQUIVO.bat`,
`LEIA-ME_WINDOWS_7.txt` e o estado `lastfile.cpu` / `lastfile.dir`.
`pc12help.CNT` difere apenas por fim de linha.

Consequência prática: **todos os endereços das análises estáticas e das
emulações anteriores continuam válidos**, e não há binário novo a analisar. O
`LEIA-ME` do pacote também documenta como aquele `pc12.exe` foi obtido — saída
de UPX e 236 referências a `C:\Program Files\Pc12 Design Center\` convertidas em
caminhos relativos —, o que explica por que ele difere do instalador original.

As 10 emulações offline arquivadas em `docs/data/` foram reexecutadas contra
esse binário e todas continuam `RESULT=PASS`.

## 2. O que nunca havia sido lido: os arquivos de ajuda

`Tp022.hlp` e `PC12HELP.HLP` (1,6 MB, idênticos entre si) e `HELPDLG.HLP` eram
usados apenas como arquivo de ajuda pelo menu clássico em `ModernPC12.cs`.
Nenhum script ou documento do projeto havia extraído o conteúdo. Eles são a
**única documentação do fabricante presente no repositório**.

Formato: WinHelp 3.x HC31, gerado em 04/01/2001, `|SYSTEM` com `flags=0` — sem
compressão LZ77 e sem compressão por frases. O texto ocupa 31 KB; os 1,5 MB
restantes são 75 hipergráficos `|bmNN` (DIB 24 bpp comprimido por RunLen).

Armadilha encontrada na extração: percorrer o `|TOPIC` linearmente recupera
apenas 7 dos 33 tópicos e passa a impressão falsa de que o arquivo é pequeno. As
posições do WinHelp são `TOPICPOS = (bloco << 14) | offset`, com o offset contado
desde o início do bloco de 4096 bytes, cabeçalho de 12 bytes incluído. O critério
de parada correto é a contagem de entradas do `|TTLBTREE` — 33, que é exatamente
o número de cabeçalhos de tópico encontrados pelo walk correto.

Reproduzir:

```bash
python3 scripts/extract_pc12_help.py -o docs/data/pc12-help-topics.txt
python3 scripts/extract_pc12_help.py src/OpenLadderStudio.Desktop/Tp022.hlp --bitmaps /tmp/pc12-bmp
```

Evidência arquivada: `docs/data/pc12-help-topics.txt`.

## 3. Fatos declarados pelo fabricante

Tudo nesta seção é **citação da documentação oficial**, não inferência nossa.

### Capacidade de programa por módulo base

```text
20/28 pontos: 1.5K words
40/60 pontos: 4K   words
```

Isso dá origem oficial ao limite de 4000 passos que o projeto já aplica em
`Tp02ComputerLinkProgramCodec`, `Tp02LadderTargetCompiler`, `Tp02Pg33DryRunFrame`
e `Tp02Pg33DryRunProgram`, e mostra que esse limite **não é universal**: vale
para o módulo base de 40/60 pontos, que é o alvo que o código assume — e o
compilador chega a dizê-lo na mensagem de erro. Não existe hoje a variante de
1,5K palavras do módulo de 20/28 pontos.

### Tempo de varredura

```text
O tempo de varredura máximo do TP02 é 200 ms.
Programa que exceda 200 ms leva o TP02 a entrar em Error Mode.
```

### Classes de memória citadas

```text
X, Y, C, S      pontos e relés
SC              bobinas especiais
WS              memória de sistema
V, D, WC        registradores
FL001~130       arquivos de texto / registradores de arquivo
RTC             somente nos módulos base de 40/60 pontos
```

### Inventário completo das operações do menu PLC

O menu PLC do PC12 tem exatamente 14 itens, e este é o conjunto de operações
que o programa original sabe executar contra o PLC:

```text
 1 Write                 8 Clear System
 2 Read                  9 Clear Data
 3 Run !                10 Clear Program
 4 Stop !               11 Clear All Memory
 5 Password             12 Compare Program
 6 EEPROM               13 COM Port
 7 Set RTC              14 Set TimeOut Value
```

Isso **fecha o espaço de comandos a investigar**: nenhuma outra operação de PG
existe no produto original. Também explica o contexto de string
`Compare PLC Program...` que a varredura de quadros já encontrara nos sítios do
`34` — comparar programa é uma leitura do `34` seguida de comparação local, não
um comando próprio.

### Senha

```text
Password define ou altera a senha do TP02.
Depois de definida, a senha passa a ser exigida para RUN!/STOP!/READ/WRITE
e para gravar o programa do TP02 no pacote EEPROM.
```

### Tempo de resposta (Set TimeOut Value)

```text
"o tempo de resposta depois que o PC12 envia um comando ao TP02"
TP02 Link direto ......... 1
Link por modem ........... 8 ou mais
TP02 ligado a OP05/06/36 . 10 ou mais
```

### EEPROM

```text
Disponível somente nos módulos base de 40/60 pontos.
Ao energizar o TP02, o programa do pacote EEPROM é carregado automaticamente.
```

### Comentários não moram no PLC

```text
"Ao ler um programa do TP02 e editá-lo em modo Boolean, o comentário é apagado."
```

Coerente com o que PG33/PG34 já mostram: o PLC guarda apenas palavras de
máquina. Comentário, símbolo e posição de fim de Ladder vivem só nos arquivos
do PC.

### Arquivos de um projeto PC12

```text
.PLC    programa do usuário          .reg3   registradores WCxxx
.sys1   memória de sistema WSxxx     .sym    símbolos
.sys2   bobinas especiais SCxxx      .file   registradores de arquivo
.cnt    posição do fim do Ladder     .cmt    comentários de programa
.reg1   registradores Vxxxx          .typ    módulo base configurado
.reg2   registradores Dxxxx
```

### Autodiagnóstico — 15 erros, com semântica

```text
Boolean: Stack Over · Stack Under · F08 ENDS Error · FUN. Double Used ·
         MCR Error · No Enough JCS · No Enough JCR · T/C Double Used ·
         FOR/NEXT Error · LABEL Not Exist · Double OUT
Ladder:  Short Circuit · Open Circuit · OUT Error · Grammar Error
```

Regras que a ajuda enuncia com exemplo:

```text
OUT  consome 1 STR          (mais STR que isso = Stack Over)
CNT  exige  2 STR           (menos que isso = Stack Under)
mesmo registrador V não pode servir a TMR e CNT     (T/C Double Used)
mesma bobina de saída não pode ter dois OUT         (Double OUT)
```

Pares obrigatórios:

```text
F-01 MCS  <-> F-02 MCR
F-03 JCS  <-> F-04 JCR
F-07 SKIP <-> F-08 ENDS
F-46 FOR  <-> F-47 NEXT
F-42 LABEL <- F-43 JMP   (o JMP exige que o LABEL exista)
```

Funções de instância única — duas ocorrências no programa dão `FUN. Double Used`:

```text
F-52  F-53  F-55  F-58  F-59
```

## 4. Cruzamento com o mapa de funções obtido por engenharia reversa

`docs/data/tp02_function_map_normalized.csv` foi construído do encoder do
`pc12.exe`, sem apoio de documentação. A ajuda oficial nomeia 11 funções, e
**todas as 11 conferem com o mapa**:

| Ajuda oficial | Mapa do repositório | Confere |
|---|---|---|
| F-00 End | `F-00 End` b0=0 b1=112 | sim |
| F-01 MCS | `F-01 MCS` b0=1 b1=112 | sim |
| F-02 MCR | `F-02 MCR` b0=2 b1=112 | sim |
| F-03 JCS | `F-03 JCS` b0=3 b1=112 | sim |
| F-04 JCR | `F-04 JCR` b0=4 b1=112 | sim |
| F-07 SKIP | `F-07 SKIP` b0=7 b1=112 | sim |
| F-08 ENDS | `F-08 ENDS` b0=8 b1=112 | sim |
| F-42 LABEL | `F-42 LB%03d` b0=42 b1=120 | sim |
| F-43 JMP | `F-43 JMP` b0=43 b1=113 | sim |
| F-46 FOR | `F-46 FOR` b0=46 b1=113 | sim |
| F-47 NEXT | `F-47 NEXT` b0=47 b1=112 | sim |

As cinco funções de instância única do fabricante também caem exatamente sobre
as que o mapa identificou como funções que tomam recurso de hardware:

```text
F-52 TENK   teclado de 10 teclas
F-53 HEXK   teclado hexadecimal
F-55 DSW    chave digital
F-58 RXD    recepção serial
F-59 TXD    transmissão serial
```

Essa coincidência é validação cruzada: duas derivações independentes — o
encoder do binário e o manual — chegaram ao mesmo conjunto.

## 5. O que a ajuda oficial não resolve

Ela é manual de operação, não especificação de protocolo. Continuam abertos,
sem alteração de estado:

```text
semântica exata do F0
payload físico do ACK do 0x33
NAK e erros físicos do 0x33
paginação do 34 acima de 80 passos
```

Sobre o `14`: a ajuda confirma que **existe** uma função de senha e que ela
protege exatamente RUN/STOP/READ/WRITE e a gravação em EEPROM. Somada ao
contexto de string `PassWord Message` / `PassWord Error` já observado nos seis
sítios do `14` em `docs/tp02-pg-frame-inventory.md`, a hipótese fica bem
sustentada — mas segue **hipótese**. Nenhuma captura física atribui semântica de
senha ao `14`, e a regra do projeto continua valendo: não presumir.

## 6. Aproveitamento possível no produto

Itens que a documentação oficial habilita, em ordem de custo:

1. **Check Logic equivalente** — as 15 verificações, com as regras de pilha, os
   pares obrigatórios e as cinco funções de instância única, estão descritas com
   exemplo. É a lista fechada do que o autodiagnóstico do TP02 cobre.
2. **Capacidade por módulo base** — acrescentar o limite de 1,5K palavras do
   módulo de 20/28 pontos ao lado dos 4000 passos do 40/60 já implementado.
3. **Aviso de varredura acima de 200 ms** — exige medir o tempo de varredura,
   que hoje o simulador não expõe; o limite oficial passa a estar disponível
   para quando existir essa medição.
4. **Leitura de projetos PC12 antigos** — as 11 extensões estão documentadas,
   o que torna viável importar um projeto `.PLC` existente com registradores,
   símbolos e comentários.
5. **Tempos de espera do enlace** — 1 para enlace direto, 10 ou mais quando há
   OP05/06/36 no barramento, o que é um caso de campo real.

Nenhum desses itens foi implementado nesta exploração; ficam registrados com a
origem para quem os for implementar.
