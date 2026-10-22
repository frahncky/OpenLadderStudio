# Pesquisa Continua IA - WEG TP02

> **DOCUMENTO HISTÓRICO DE ARQUITETURA.** Este arquivo descreve o modo de pesquisa contínua do motor 1.9 e não representa o fluxo experimental focado atual. Para retomar a engenharia reversa, consulte primeiro **[`TP02_PG_ESTADO_DA_ARTE.md`](TP02_PG_ESTADO_DA_ARTE.md)** e **[`data/tp02_pg_observations.tsv`](data/tp02_pg_observations.tsv)**. O snapshot canônico atual é OpenLadder Studio v1.03 / TP02 PG Lab 1.16.

A Pesquisa Continua IA foi criada para manter uma unica sessao de bancada aberta enquanto o OpenLadder Studio investiga o protocolo PG do WEG TP02-60MR.

## Objetivo

Eliminar o ciclo manual `executar teste -> fotografar resultado -> alterar programa -> instalar nova versao`. No modo continuo, o laboratorio mantem a porta COM aberta sempre que houver um enlace util, envia cada nova observacao tecnica para a OpenAI API e recebe uma unica proxima acao segura.

```text
TP02 <-> COM aberta <-> OpenLadder Studio <-> OpenAI Responses API
          ^                   |
          |                   v
          +------ Safety Gate local
```

## Conversa persistente

O agente usa a Responses API e encadeia cada decisao pela propriedade `previous_response_id`. Assim, o modelo recebe o historico da investigacao como uma conversa continua em vez de analisar cada captura de forma isolada.

As instrucoes de seguranca e de engenharia sao reenviadas em todos os turnos. A resposta do modelo usa Structured Outputs com JSON Schema e deve obedecer ao contrato local de acoes.

## Acoes permitidas

A IA pode escolher somente:

- `open_profile`;
- `set_lines`;
- `hello`;
- `probe`;
- `passive`;
- `wait`;
- `finish`.

Nao existe `send_hex`. O modelo nao pode fornecer bytes arbitrarios para a porta serial.

## Safety Gate

Mesmo que o modelo produza uma ordem incorreta, o executavel continua sendo a autoridade final. Permanecem bloqueados:

- escrita em memoria ou programa;
- WBP/download;
- RUN ou STOP remoto;
- apagamento;
- firmware;
- qualquer quadro fora da allowlist de leitura.

O `38 00 C7` continua dependendo da validacao do F0 na mesma sessao.

## Sessao de pesquisa

O motor PG Lab 1.9 executa ate 200 decisoes ou 60 minutos por campanha. O usuario pode interromper a pesquisa a qualquer momento.

Abrir outro perfil serial continua permitido quando necessario, mas o agente e instruido a preservar a mesma porta aberta sempre que houver enlace util. Mudancas de DTR/RTS podem ser testadas sem TX para separar efeitos de linha dos efeitos de comandos.

Cada turno registra:

- perfil serial;
- DTR/RTS;
- ultimo TX e RX;
- estado do HELLO;
- estado do F0;
- historico recente;
- ID da resposta da OpenAI;
- probes tentados e probes que produziram RX.

A `OPENAI_API_KEY` nunca e colocada no relatorio. Ela continua protegida pelo DPAPI do Windows ou pode ser fornecida por `OPENAI_API_KEY`.

## Fechamento local da pesquisa

O modelo nao pode encerrar a campanha apenas porque considera que ja possui evidencia suficiente. A acao `finish` e bloqueada ate o OpenLadder atingir o criterio local `PROTOCOL_CLOSURE_READY` para a investigacao de leitura.

O criterio atual exige:

1. enlace conhecido estabelecido;
2. F0 validado fisicamente;
3. tentativa de todos os probes READ-ONLY atuais;
4. RX real no F0;
5. RX real na leitura estatica de programa;
6. RX real em pelo menos uma leitura de sistema.

Esse criterio representa fechamento suficiente da fase READ-ONLY atual; nao significa que escrita/download estejam liberados.

## Fluxo de bancada

1. conectar o TP02;
2. selecionar a porta COM;
3. configurar a OpenAI API uma unica vez;
4. deixar `PESQUISA CONTINUA IA` marcado;
5. iniciar o laboratorio;
6. aguardar a investigacao ou usar PARAR quando desejar.

A partir dai, as novas respostas do PLC sao analisadas automaticamente dentro da mesma conversa de pesquisa, sem depender de novas versoes do programa a cada hipotese.
