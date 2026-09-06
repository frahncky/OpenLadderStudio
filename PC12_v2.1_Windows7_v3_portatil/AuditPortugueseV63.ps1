$ErrorActionPreference = 'Stop'
$root = Get-Location

$pattern = '@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"'
$badWords = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
@(
    'nao','versao','versoes','atualizacao','atualizacoes','configuracao','configuracoes',
    'conexao','conexoes','simulacao','programacao','comunicacao','comunicacoes','informacao','informacoes',
    'selecao','operacao','operacoes','opcao','opcoes','parametro','parametros','posicao','posicoes',
    'edicao','alteracao','alteracoes','execucao','validacao','verificacao','verificacoes','disponivel','possivel',
    'compativel','invalido','invalida','invalidos','invalidas','necessario','necessaria','necessarios','necessarias',
    'memoria','diagnostico','historico','padrao','padroes','ultima','ultimo','proxima','proximo','propria','proprio',
    'usuario','usuarios','endereco','enderecos','funcao','funcoes','acao','acoes','situacao','solucao','aplicacao','aplicacoes',
    'estacao','estacoes','modulo','modulos','fisico','fisica','fisicos','fisicas','unico','unica','unicos','unicas',
    'seguranca','sequencia','estagio','basico','basica','basicos','basicas','observacao','observacoes','saida','saidas',
    'temporizacao','evidencia','evidencias','espaco','espacos','conteudo','conteudos','revisao','compilacao','condicao','condicoes',
    'instrucao','instrucoes','associacao','semantica','semantico','maquina','maquinas','binaria','binario','criterio','criterios',
    'promocao','hipotese','hipoteses','comparacao','codificacao','familia','familias','grafico','graficos','area','areas',
    'concluida','concluido','deteccao','instalacao','protecao','gravacao','transicao','implementacao','inicializacao',
    'substituicao','manutencao','referencia','portatil','calibracao','reconstrucao','decodificacao','inferencia','excecao',
    'restricao','restricoes','atencao','serao','sera','sao','apos','ate','alem','tambem','devera','podera'
) | ForEach-Object { [void]$badWords.Add($_) }

function Has-Mojibake([string]$value) {
    if ($value.IndexOf([char]0xFFFD) -ge 0) { return $true }
    if ([regex]::IsMatch($value, '\u00C3(?=[\u0080-\u00BF\u0192])')) { return $true }
    if ([regex]::IsMatch($value, '\u00C2(?=[\u0080-\u00BF\u2000-\u206F])')) { return $true }
    if ([regex]::IsMatch($value, '\u00E2(?=[\u0080-\u00BF\u2000-\u206F])')) { return $true }
    return $false
}

function Has-UnaccentedWord([string]$value) {
    if ($value -cmatch '^@?"[a-z][a-z0-9_.-]*"$') { return $false }
    $tokens = [regex]::Matches($value, '(?<![A-Za-z])[A-Za-z]+(?![A-Za-z])')
    foreach ($token in $tokens) {
        if ($badWords.Contains($token.Value)) { return $true }
    }
    return $false
}

$findings = New-Object System.Collections.Generic.List[string]
$files = Get-ChildItem -Path $root -Filter '*.cs' -File | Sort-Object Name
foreach ($file in $files) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    $matches = [regex]::Matches($text, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    foreach ($m in $matches) {
        $value = $m.Value
        if ((Has-Mojibake $value) -or (Has-UnaccentedWord $value)) {
            $line = 1 + ([regex]::Matches($text.Substring(0, $m.Index), "`n")).Count
            $compact = ($value -replace "`r|`n", ' ')
            if ($compact.Length -gt 240) { $compact = $compact.Substring(0, 237) + '...' }
            $findings.Add(('{0}:{1}: {2}' -f $file.Name, $line, $compact))
        }
    }
}

if ($findings.Count -gt 0) {
    Write-Host ''
    Write-Host '=== AUDITORIA PT-BR: textos suspeitos ===' -ForegroundColor Yellow
    $findings | Sort-Object -Unique | ForEach-Object { Write-Host $_ }
    Write-Host ('Total: {0} ocorrencia(s).' -f $findings.Count) -ForegroundColor Yellow
    throw 'A auditoria de portugues encontrou textos que precisam de revisao.'
}

Write-Host 'Auditoria PT-BR concluida sem textos suspeitos.'
