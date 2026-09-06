$ErrorActionPreference = 'Stop'
$root = Get-Location
$utf8 = New-Object System.Text.UTF8Encoding($false, $false)
$cp1252 = [System.Text.Encoding]::GetEncoding(1252)
$stringPattern = '@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"'
$wordPattern = '(?<![A-Za-z])([A-Za-z]+)(?![A-Za-z])'

function Decode-U([string]$value) {
    return [regex]::Replace($value, '\\u([0-9A-Fa-f]{4})', {
        param($m)
        return [char][Convert]::ToInt32($m.Groups[1].Value, 16)
    })
}

function Mojibake-Count([string]$value) {
    $count = 0
    $count += ([regex]::Matches($value, '\u00C3(?=[\u0080-\u00BF\u0192])')).Count
    $count += ([regex]::Matches($value, '\u00C2(?=[\u0080-\u00BF\u2000-\u206F])')).Count
    $count += ([regex]::Matches($value, '\u00E2(?=[\u0080-\u00BF\u2000-\u206F])')).Count
    $count += ([regex]::Matches($value, '\uFFFD')).Count
    return $count
}

function Repair-Mojibake([string]$value) {
    for ($i = 0; $i -lt 4; $i++) {
        $before = Mojibake-Count $value
        if ($before -eq 0) { break }
        try { $candidate = $utf8.GetString($cp1252.GetBytes($value)) }
        catch { break }
        $after = Mojibake-Count $candidate
        if ($after -lt $before) { $value = $candidate } else { break }
    }
    return $value
}

$rawMap = @{
    'nao'='n\u00E3o'; 'versao'='vers\u00E3o'; 'versoes'='vers\u00F5es';
    'atualizacao'='atualiza\u00E7\u00E3o'; 'atualizacoes'='atualiza\u00E7\u00F5es';
    'configuracao'='configura\u00E7\u00E3o'; 'configuracoes'='configura\u00E7\u00F5es';
    'conexao'='conex\u00E3o'; 'conexoes'='conex\u00F5es'; 'simulacao'='simula\u00E7\u00E3o';
    'programacao'='programa\u00E7\u00E3o'; 'comunicacao'='comunica\u00E7\u00E3o'; 'comunicacoes'='comunica\u00E7\u00F5es';
    'informacao'='informa\u00E7\u00E3o'; 'informacoes'='informa\u00E7\u00F5es'; 'selecao'='sele\u00E7\u00E3o';
    'operacao'='opera\u00E7\u00E3o'; 'operacoes'='opera\u00E7\u00F5es'; 'opcao'='op\u00E7\u00E3o'; 'opcoes'='op\u00E7\u00F5es';
    'parametro'='par\u00E2metro'; 'parametros'='par\u00E2metros'; 'posicao'='posi\u00E7\u00E3o'; 'posicoes'='posi\u00E7\u00F5es';
    'edicao'='edi\u00E7\u00E3o'; 'alteracao'='altera\u00E7\u00E3o'; 'alteracoes'='altera\u00E7\u00F5es';
    'execucao'='execu\u00E7\u00E3o'; 'validacao'='valida\u00E7\u00E3o'; 'verificacao'='verifica\u00E7\u00E3o'; 'verificacoes'='verifica\u00E7\u00F5es';
    'disponivel'='dispon\u00EDvel'; 'possivel'='poss\u00EDvel'; 'compativel'='compat\u00EDvel';
    'invalido'='inv\u00E1lido'; 'invalida'='inv\u00E1lida'; 'invalidos'='inv\u00E1lidos'; 'invalidas'='inv\u00E1lidas';
    'necessario'='necess\u00E1rio'; 'necessaria'='necess\u00E1ria'; 'necessarios'='necess\u00E1rios'; 'necessarias'='necess\u00E1rias';
    'memoria'='mem\u00F3ria'; 'diagnostico'='diagn\u00F3stico'; 'historico'='hist\u00F3rico'; 'padrao'='padr\u00E3o'; 'padroes'='padr\u00F5es';
    'ultima'='\u00FAltima'; 'ultimo'='\u00FAltimo'; 'proxima'='pr\u00F3xima'; 'proximo'='pr\u00F3ximo'; 'propria'='pr\u00F3pria'; 'proprio'='pr\u00F3prio';
    'usuario'='usu\u00E1rio'; 'usuarios'='usu\u00E1rios'; 'endereco'='endere\u00E7o'; 'enderecos'='endere\u00E7os';
    'funcao'='fun\u00E7\u00E3o'; 'funcoes'='fun\u00E7\u00F5es'; 'acao'='a\u00E7\u00E3o'; 'acoes'='a\u00E7\u00F5es';
    'situacao'='situa\u00E7\u00E3o'; 'solucao'='solu\u00E7\u00E3o'; 'aplicacao'='aplica\u00E7\u00E3o'; 'aplicacoes'='aplica\u00E7\u00F5es';
    'estacao'='esta\u00E7\u00E3o'; 'estacoes'='esta\u00E7\u00F5es'; 'modulo'='m\u00F3dulo'; 'modulos'='m\u00F3dulos';
    'fisico'='f\u00EDsico'; 'fisica'='f\u00EDsica'; 'fisicos'='f\u00EDsicos'; 'fisicas'='f\u00EDsicas';
    'unico'='\u00FAnico'; 'unica'='\u00FAnica'; 'unicos'='\u00FAnicos'; 'unicas'='\u00FAnicas';
    'seguranca'='seguran\u00E7a'; 'sequencia'='sequ\u00EAncia'; 'estagio'='est\u00E1gio';
    'basico'='b\u00E1sico'; 'basica'='b\u00E1sica'; 'basicos'='b\u00E1sicos'; 'basicas'='b\u00E1sicas';
    'observacao'='observa\u00E7\u00E3o'; 'observacoes'='observa\u00E7\u00F5es'; 'saida'='sa\u00EDda'; 'saidas'='sa\u00EDdas';
    'temporizacao'='temporiza\u00E7\u00E3o'; 'evidencia'='evid\u00EAncia'; 'evidencias'='evid\u00EAncias';
    'espaco'='espa\u00E7o'; 'espacos'='espa\u00E7os'; 'conteudo'='conte\u00FAdo'; 'conteudos'='conte\u00FAdos'; 'revisao'='revis\u00E3o';
    'compilacao'='compila\u00E7\u00E3o'; 'condicao'='condi\u00E7\u00E3o'; 'condicoes'='condi\u00E7\u00F5es';
    'instrucao'='instru\u00E7\u00E3o'; 'instrucoes'='instru\u00E7\u00F5es'; 'associacao'='associa\u00E7\u00E3o';
    'semantica'='sem\u00E2ntica'; 'semantico'='sem\u00E2ntico'; 'maquina'='m\u00E1quina'; 'maquinas'='m\u00E1quinas';
    'binaria'='bin\u00E1ria'; 'binario'='bin\u00E1rio'; 'criterio'='crit\u00E9rio'; 'criterios'='crit\u00E9rios';
    'promocao'='promo\u00E7\u00E3o'; 'hipotese'='hip\u00F3tese'; 'hipoteses'='hip\u00F3teses'; 'comparacao'='compara\u00E7\u00E3o';
    'codificacao'='codifica\u00E7\u00E3o'; 'familia'='fam\u00EDlia'; 'familias'='fam\u00EDlias'; 'grafico'='gr\u00E1fico'; 'graficos'='gr\u00E1ficos';
    'area'='\u00E1rea'; 'areas'='\u00E1reas'; 'concluida'='conclu\u00EDda'; 'concluido'='conclu\u00EDdo'; 'deteccao'='detec\u00E7\u00E3o';
    'instalacao'='instala\u00E7\u00E3o'; 'protecao'='prote\u00E7\u00E3o'; 'gravacao'='grava\u00E7\u00E3o'; 'transicao'='transi\u00E7\u00E3o';
    'implementacao'='implementa\u00E7\u00E3o'; 'inicializacao'='inicializa\u00E7\u00E3o'; 'substituicao'='substitui\u00E7\u00E3o';
    'manutencao'='manuten\u00E7\u00E3o'; 'referencia'='refer\u00EAncia'; 'portatil'='port\u00E1til'; 'calibracao'='calibra\u00E7\u00E3o';
    'reconstrucao'='reconstru\u00E7\u00E3o'; 'decodificacao'='decodifica\u00E7\u00E3o'; 'inferencia'='infer\u00EAncia'; 'excecao'='exce\u00E7\u00E3o';
    'restricao'='restri\u00E7\u00E3o'; 'restricoes'='restri\u00E7\u00F5es'; 'atencao'='aten\u00E7\u00E3o';
    'serao'='ser\u00E3o'; 'sera'='ser\u00E1'; 'sao'='s\u00E3o'; 'apos'='ap\u00F3s'; 'ate'='at\u00E9'; 'alem'='al\u00E9m'; 'tambem'='tamb\u00E9m';
    'devera'='dever\u00E1'; 'podera'='poder\u00E1'
}

$map = @{}
foreach ($key in $rawMap.Keys) { $map[$key] = Decode-U $rawMap[$key] }

$uiReplacementRaw = @(
    @('TP02 BRIDGE LAB', 'LABORAT\u00D3RIO TP02'),
    @('Monitor online', 'Monitor on-line'),
    @('Monitoramento online', 'Monitoramento on-line'),
    @('monitoramento online', 'monitoramento on-line'),
    @('OFFLINE', 'OFF-LINE'),
    @('Offline', 'Off-line'),
    @('ONLINE', 'ON-LINE'),
    @('Online', 'On-line'),
    @('Baud rate', 'Taxa de transmiss\u00E3o'),
    @('Data bits', 'Bits de dados'),
    @('Stop bits', 'Bits de parada'),
    @('Unit ID', 'ID da unidade'),
    @('Timeout (ms)', 'Tempo limite (ms)'),
    @('01 - Read Coils', '01 - Ler bobinas'),
    @('02 - Read Discrete Inputs', '02 - Ler entradas discretas'),
    @('03 - Read Holding Registers', '03 - Ler registradores de reten\u00E7\u00E3o'),
    @('04 - Read Input Registers', '04 - Ler registradores de entrada'),
    @('Coils / FC01', 'Bobinas / FC01'),
    @('Discrete Inputs / FC02', 'Entradas discretas / FC02'),
    @('Holding Registers / FC03', 'Registradores de reten\u00E7\u00E3o / FC03'),
    @('Input Registers / FC04', 'Registradores de entrada / FC04'),
    @('O programa n\u00E3o possui rungs.', 'O programa n\u00E3o possui linhas Ladder.'),
    @('Programa sem END: todos os rungs ser\u00E3o executados em cada varredura.', 'Programa sem END: todas as linhas Ladder ser\u00E3o executadas em cada varredura.'),
    @('Energiza\u00E7\u00E3o dos rungs', 'Energiza\u00E7\u00E3o das linhas Ladder'),
    @('\u00DAltimo rung n\u00E3o foi fechado com OUT.', 'A \u00FAltima linha n\u00E3o foi fechada com OUT.'),
    @('RUN/STOP/escrita/download/apagamento', 'RUN/STOP, escrita, transfer\u00EAncia de programa e apagamento'),
    @('download de programa Ladder', 'transfer\u00EAncia do programa Ladder'),
    @('download do programa Ladder', 'transfer\u00EAncia do programa Ladder'),
    @('download Ladder', 'transfer\u00EAncia do programa Ladder'),
    @('status do PLC', 'estado do PLC'),
    @('Status do PLC', 'Estado do PLC')
)
$uiReplacements = @()
foreach ($pair in $uiReplacementRaw) { $uiReplacements += ,@((Decode-U $pair[0]), (Decode-U $pair[1])) }

$grammarRaw = @(
    @(' e confirmado', ' \u00E9 confirmado'),
    @(' e transmitido', ' \u00E9 transmitido'),
    @(' e enviado', ' \u00E9 enviado'),
    @(' este e o dado', ' este \u00E9 o dado'),
    @(' etapa e decodificar', ' etapa \u00E9 decodificar'),
    @(' link e confirmado', ' link \u00E9 confirmado')
)
$grammarReplacements = @()
foreach ($pair in $grammarRaw) { $grammarReplacements += ,@($pair[0], (Decode-U $pair[1])) }

function Preserve-Case([string]$source, [string]$target) {
    if ($source -ceq $source.ToUpperInvariant()) { return $target.ToUpperInvariant() }
    if ($source.Length -gt 0 -and $source.Substring(0,1) -ceq $source.Substring(0,1).ToUpperInvariant()) {
        return $target.Substring(0,1).ToUpperInvariant() + $target.Substring(1)
    }
    return $target
}

function Normalize-Literal([string]$literal) {
    $literal = Repair-Mojibake $literal
    if ($literal -cmatch '^@?"[a-z][a-z0-9_.-]*"$') { return $literal }

    foreach ($pair in $uiReplacements) { $literal = $literal.Replace($pair[0], $pair[1]) }
    $literal = [regex]::Replace($literal, '(?<![A-Za-z])Rung(?=\s)', 'Linha', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    $literal = [regex]::Replace($literal, $wordPattern, {
        param($m)
        $word = $m.Groups[1].Value
        $key = $word.ToLowerInvariant()
        if (-not $map.ContainsKey($key)) { return $word }
        return Preserve-Case $word $map[$key]
    })

    foreach ($pair in $grammarReplacements) { $literal = $literal.Replace($pair[0], $pair[1]) }
    return $literal
}

$changedFiles = 0
$changedStrings = 0
$files = Get-ChildItem -Path $root -Filter '*.cs' -File | Sort-Object Name
foreach ($file in $files) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    $script:localCount = 0
    $newText = [regex]::Replace($text, $stringPattern, {
        param($m)
        $fixed = Normalize-Literal $m.Value
        if ($fixed -cne $m.Value) { $script:changedStrings++; $script:localCount++ }
        return $fixed
    }, [System.Text.RegularExpressions.RegexOptions]::Singleline)

    if ($script:localCount -gt 0) {
        [System.IO.File]::WriteAllText($file.FullName, $newText, $utf8)
        $changedFiles++
        Write-Host ('PT-BR normalizado: {0} ({1} string(s))' -f $file.Name, $script:localCount)
    }
}

Write-Host ('Normalizacao PT-BR concluida: {0} arquivo(s), {1} string(s).' -f $changedFiles, $changedStrings)
