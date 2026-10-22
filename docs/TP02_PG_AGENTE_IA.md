# Agente IA Adaptativo - WEG TP02

O modo adaptativo foi criado para eliminar o ciclo `captura -> nova versao -> novo teste`. Ele mantem o mesmo executavel e consulta um modelo da OpenAI depois de cada resultado de bancada.

## Principio

O modelo nao controla a serial diretamente. Ele recebe uma observacao e escolhe somente uma acao abstrata. O OpenLadder Studio valida a ordem localmente e somente entao toca na porta COM.

```text
TP02 -> RX -> OpenLadder -> observacao JSON -> OpenAI
                                            |
TP02 <- acao segura <- Safety Gate <- JSON -+
```

## Configuracao

1. abra o Laboratorio PG;
2. clique `CONFIGURAR IA`;
3. informe uma `OPENAI_API_KEY`;
4. deixe `AGENTE IA ADAPTATIVO` marcado;
5. selecione a COM e execute o teste.

A chave e protegida pelo DPAPI do Windows e fica fora dos relatorios. Tambem e possivel fornecer a chave pela variavel de ambiente `OPENAI_API_KEY`. O modelo padrao e `gpt-5.6`; `OPENAI_MODEL` pode substituir esse valor.

## Contrato de acao

O modelo pode escolher somente:

- `open_profile`;
- `set_lines`;
- `hello`;
- `probe`;
- `passive`;
- `wait`;
- `finish`.

O contrato nao possui campo para bytes arbitrarios.

## Safety Gate

Mesmo que a resposta do modelo seja incorreta ou maliciosa, o executor rejeita:

- perfil que nao esteja no pacote;
- probe que nao esteja habilitado na lista READ-ONLY;
- probe antes de um HELLO conhecido;
- `38 00 C7` sem F0 validado na mesma sessao;
- escrita de memoria/programa;
- WBP/download;
- RUN/STOP remoto;
- apagamento;
- firmware;
- qualquer opcode arbitrario.

Quatro ordens invalidas consecutivas encerram a campanha com `AI_SAFETY_STOP`.

## Estado enviado ao modelo

Cada decisao inclui somente informacoes tecnicas da bancada:

- porta COM selecionada;
- perfil serial atual;
- DTR e RTS;
- se o HELLO foi reconhecido;
- se o F0 foi validado;
- ultimo TX e RX em hexadecimal;
- ate 24 eventos recentes;
- nomes dos perfis permitidos;
- IDs dos probes READ-ONLY permitidos.

A chave da API nao faz parte desse objeto.

## Evidencia atual

Os fatos usados pelo prompt do agente sao:

- `19200 8O1`;
- HELLO STOP `80 01 09 75`;
- F0 `F0 00 0F` ja observado com resposta `00 02 10 22 CB`;
- na v0.92, a transicao `RTS TX=on -> RX=off` fez o PLC emitir novamente `80 01 09 75`, sugerindo participacao de RTS na direcao ou na re-sincronizacao do enlace.

O agente e instruido a fazer experimentos discriminatorios, alterando uma variavel por vez e usando captura passiva para separar efeito de linha de efeito de TX.
