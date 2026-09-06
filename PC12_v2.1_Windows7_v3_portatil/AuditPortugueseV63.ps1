$ErrorActionPreference = 'Stop'
$root = Get-Location

$pattern = '@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"'
$suspicious = '(?i)(?<![\p{L}])(nao|versao|versoes|atualizacao|atualizacoes|configuracao|configuracoes|conexao|conexoes|simulacao|programacao|comunicacao|comunicacoes|informacao|informacoes|selecao|operacao|operacoes|opcao|opcoes|parametro|parametros|posicao|posicoes|edicao|alteracao|alteracoes|execucao|validacao|verificacao|disponivel|possivel|compativel|invalido|invalida|necessario|necessaria|necessarios|necessarias|memoria|diagnostico|historico|padrao|padroes|ultima|ultimo|proxima|proximo|propria|proprio|usuario|usuarios|endereco|enderecos|funcao|funcoes|acao|acoes|situacao|solucao|aplicacao|aplicacoes|comando|comandos)(?![\p{L}])'
$mojibake = 'Ã.|Â.|â€|ï¿½|�'

$findings = New-Object System.Collections.Generic.List[string]
$files = Get-ChildItem -Path $root -Filter '*.cs' -File | Sort-Object Name
foreach ($file in $files) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    $matches = [regex]::Matches($text, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    foreach ($m in $matches) {
        $value = $m.Value
        if ($value -match $suspicious -or $value -match $mojibake) {
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
    Write-Host ('Total: {0} ocorrência(s).' -f $findings.Count) -ForegroundColor Yellow
    throw 'A auditoria de português encontrou textos que precisam de revisão.'
}

Write-Host 'Auditoria PT-BR concluída sem textos suspeitos.'
