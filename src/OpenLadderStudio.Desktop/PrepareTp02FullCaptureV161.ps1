$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'TP02FullProtocolCapture.build.cs'
if (-not (Test-Path -LiteralPath $path)) { throw 'TP02FullProtocolCapture.build.cs nao encontrado apos v1.60.' }

$text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)

# v1.61 reproduz a sessao fisica bem-sucedida registrada em 2026-09-14:
# 19200 8O1, DTR=OFF, RTS=OFF, cerca de 450 ms HELLO->F0 e 420 ms F0->38.
$replacements = @(
    @('profile.Name = "V160 sessao limpa 19200 8O1 DTR=on RTS=off";', 'profile.Name = "V161 sessao fisica 19200 8O1 DTR=off RTS=off";'),
    @('profile.Dtr = true;', 'profile.Dtr = false;'),
    @('+ " | 19200 8O1 DTR=on RTS=off.");', '+ " | 19200 8O1 DTR=off RTS=off.");'),
    @('port = OpenPort(portName, true, false);', 'port = OpenPort(portName, false, false);'),
    @('Log("V160: HELLO confirmou " + detected + "; aguardando 180 ms antes do unico F0 desta abertura.");', 'Log("V161: HELLO confirmou " + detected + "; aguardando 450 ms antes do unico F0 desta abertura.");')
)

foreach ($pair in $replacements) {
    $old = $pair[0]
    $new = $pair[1]
    if (-not $text.Contains($old)) { throw "V161: trecho nao localizado: $old" }
    $text = $text.Replace($old, $new)
}

# Nao usar '\n' literal em strings PowerShell: o build v1.60 tem quebras reais.
# A ancora estrutural abaixo so encontra o Sleep depois de HELLO confirmado,
# nao o Sleep(180) utilizado entre tentativas de HELLO.
$delayPattern = '(?m)^([ \t]*)Thread\.Sleep\(180\);(?=\r?\n[ \t]*\r?\n[ \t]*// Regra critica: um unico F0 por abertura da COM\.)'
$delayMatches = [regex]::Matches($text, $delayPattern)
if ($delayMatches.Count -ne 1) { throw "V161: esperado um unico Sleep(180) antes do F0; encontrados $($delayMatches.Count)." }
$text = [regex]::Replace($text, $delayPattern, '${1}Thread.Sleep(450);')

# O patch v1.60 ainda se chama AcquireQualifiedPortV160; mantemos o nome
# para reduzir a superficie de mudanca e identificar a v1.61 no log.
$text = $text.Replace('Log("V160: iniciando sessao limpa "', 'Log("V161: iniciando sessao fisica "')
$text = $text.Replace('Log("V160: HELLO nao confirmado nesta abertura; fechando a COM sem enviar F0.");', 'Log("V161: HELLO nao confirmado nesta abertura; fechando a COM sem enviar F0.");')
$text = $text.Replace('Log("V160: F0 sem resposta conhecida; nenhum 38/34/0A sera enviado nesta abertura.");', 'Log("V161: F0 sem resposta conhecida; nenhum 38/34/0A sera enviado nesta abertura.");')
$text = $text.Replace('Log("V160: HELLO+F0 confirmados na mesma sessao limpa: 00 02 10 22 CB.");', 'Log("V161: HELLO+F0 confirmados no perfil fisico comprovado: 00 02 10 22 CB.");')
$text = $text.Replace('Log("V160: COM fechada; aguardando 1500 ms antes de nova sessao limpa.");', 'Log("V161: COM fechada; aguardando 1500 ms antes de nova sessao fisica.");')
$text = $text.Replace('throw new IOException("V160: 12 sessoes limpas sem confirmar HELLO+F0. "', 'throw new IOException("V161: 12 sessoes fisicas sem confirmar HELLO+F0. "')

# Evita comparar comentarios acentuados ou blocos inteiros; substitui somente
# a guarda ASCII exclusiva do helper, independentemente de CRLF/LF.
$helper = 'private static void CaptureF0AlreadyQualifiedV159(SerialPort port)'
$oldGuard = '            if (port == null || !port.IsOpen) throw new InvalidOperationException("Sessao PG v1.59 nao esta aberta.");'
$newGuard = @'
            if (port == null || !port.IsOpen) throw new InvalidOperationException("Sessao PG qualificada nao esta aberta.");
            Log("V161: F0 validado; estabilizando 420 ms antes do 38.");
            Thread.Sleep(420);
'@
if (-not $text.Contains($helper)) { throw 'V161: helper CaptureF0AlreadyQualifiedV159 nao localizado.' }
if (-not $text.Contains($oldGuard)) { throw 'V161: guarda do helper CaptureF0AlreadyQualifiedV159 nao localizada.' }
$text = $text.Replace($oldGuard, $newGuard.TrimEnd("`r", "`n"))

# As duas alteracoes criticas devem ter sido aplicadas antes de liberar o build.
if (-not $text.Contains('port = OpenPort(portName, false, false);')) { throw 'V161: perfil DTR/RTS OFF ausente.' }
if (-not $text.Contains('Thread.Sleep(450);')) { throw 'V161: atraso HELLO-F0 ausente.' }
if (-not $text.Contains('Log("V161: F0 validado; estabilizando 420 ms antes do 38.");')) { throw 'V161: atraso F0-38 ausente.' }

[IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false)))
Write-Host 'TP02 Full Capture v1.61 aplicado: DTR/RTS OFF, 450 ms HELLO-F0 e 420 ms F0-38.'
