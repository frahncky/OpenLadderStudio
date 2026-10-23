# Laboratorio TP02 offline

O objetivo deste roteiro e desenvolver e verificar o suporte TP02 sem PLC e
sem abrir uma porta COM fisica. Ele nao substitui a validacao final em
equipamento, mas permite repetir a maior parte do trabalho de protocolo com
seguranca.

## Execucao automatica

No diretorio `src/OpenLadderStudio.Desktop`, execute:

```bat
RunTp02OfflineLab.bat
```

O roteiro compila `OpenLadderTP02Emulator.exe` e roda
`--self-test-all`. Esse modo nao pede porta, nao cria conexao serial e nao
transmite bytes.

## Cobertura atual

O emulador e os testes verificam offline:

- checksum de soma modulo 256 igual a `FF`;
- parser e limites do PG33 (1 a 80 words, endereco e comprimento);
- leitura paginada PG34 e metadados PG38;
- persistencia de programa, END e cauda antiga apos uma gravacao curta;
- respostas atrasadas de HELLO/F0 no cenario v1.56;
- BRAW para todas as combinacoes de bytes HIGH/LOW;
- respostas malformadas, truncadas e com checksum invalido;
- vetores de protocolo e codec de programa do computador.

O teste e um modelo de software. Respostas marcadas como sinteticas pelo
emulador permanecem hipoteses de compatibilidade, nao observacoes do PLC.

## Teste ponta a ponta sem PLC

Para fazer o PC12 original conversar com o emulador, crie um par de portas
COM virtuais (por exemplo, COM10 e COM11). O PC12 usa uma ponta e o emulador a
outra. Nunca configure o emulador para uma COM fisica conectada ao TP02.

```bat
RunTp02EmulatorV156.bat COM11
```

Esse passo abre somente a COM virtual indicada. Ele e util para observar o
comportamento do PC12 contra respostas reproduziveis, sem PLC.

### Hipotese PG38 de 16 bits

No replay de 22/10/2026, o emulador respondeu `38` com `00 02 00 9E 5F`
(campo limitado a uma pagina de 80 passos) e o PC12 leu apenas as paginas
`0000` e `0050` do programa de 323 words. Para testar se o PC12 usa os bytes
2-3 do `38` como `2 * (passos - 1)` do programa inteiro, rode:

```bat
RunTp02EmulatorV156.bat COM4 --pg38=total16
```

Com o seed de 323 words, o `38` passa a ser `00 02 02 84 77`. No replay
controlado de 23/10/2026, o PC12 pediu nove paginas: `0000`, `0050`, `00A0`,
`00F0`, `0140`, `0190`, `01E0`, `0230` e `0280`; depois iniciou leituras
`0A` de memoria. Portanto, a previsao inicial de cinco paginas foi refutada:
o PC12 usa o campo de 16 bits do `38`, mas a conversao exata para o limite de
paginas ainda e desconhecida. O registro local e
`tp02-emulator-captures/TP02-Emulator-20261023-130853.txt`.

O editor PC12 exibiu `DOUBLE OUT` no endereco `0003` depois de importar o
seed. Isso e uma validacao semantica esperada para o programa de fronteira,
que repete `OUT C0001`; nao representa falha da comunicacao serial. Continua
sendo hipotese: a unica captura fisica do `38` tem 3 passos
(`00 02 00 04 F9`), em que os dois modos coincidem. O padrao continua
`--pg38=page`.

## O que permanece pendente de hardware

Somente uma validacao final controlada pode confirmar cabo, padrao eletrico
(RS-232/RS-422), sinais DTR/RTS, firmware e efeitos reais de leitura/escrita.
As rotinas de escrita, RUN, STOP, download e apagamento devem continuar fora
do teste fisico ate haver uma captura rotulada e um plano de reversao.
