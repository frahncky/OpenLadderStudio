# Operacao da Pesquisa Continua TP02

1. Conecte o TP02 e selecione a porta COM no Laboratorio PG.
2. Configure a OpenAI API uma unica vez em `CONFIGURAR IA`.
3. Deixe `PESQUISA CONTINUA IA` habilitado.
4. Inicie a pesquisa.
5. Nao desconecte o cabo enquanto a campanha estiver em andamento.
6. Use `PARAR` a qualquer momento se desejar encerrar manualmente.

Durante a campanha, o OpenLadder mantem a porta serial aberta quando houver enlace util e envia para o modelo somente o estado tecnico necessario: perfil, DTR/RTS, TX/RX, HELLO/F0, historico recente e IDs permitidos. A chave da API nao faz parte dessas observacoes.

O modelo escolhe uma unica proxima acao por turno. O Safety Gate local valida cada ordem antes de qualquer acesso a serial. Nao existe transmissao de bytes arbitrarios, escrita, download, RUN/STOP remoto, apagamento ou firmware.

O relatorio final registra o encadeamento da pesquisa e os IDs das respostas da API, permitindo auditar por que cada passo foi executado.
