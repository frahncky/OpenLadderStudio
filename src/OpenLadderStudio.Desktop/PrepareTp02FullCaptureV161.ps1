$ErrorActionPreference = 'Stop'

$path = Join-Path (Get-Location) 'TP02FullProtocolCapture.build.cs'
if (-not (Test-Path -LiteralPath $path)) { throw 'TP02FullProtocolCapture.build.cs nao encontrado apos v1.60.' }

$text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)

# v1.61 reproduz exatamente a sessao fisica bem-sucedida registrada em 2026-09-14:
# 19200 8O1, DTR=OFF, RTS=OFF, cerca de 450 ms HELLO->F0 e ~420 ms F0->38.
$replacements = @(
    @('profile.Name = "V160 sessao limpa 19200 8O1 DTR=on RTS=off";', 'profile.Name = "V161 sessao fisica 19200 8O1 DTR=off RTS=off";'),
    @('profile.Dtr = true;', 'profile.Dtr = false;'),
    @('profile.Rts = false;', 'profile.Rts = false;'),
    @('+ " | 19200 8O1 DTR=on RTS=off.");', '+ " | 19200 8O1 DTR=off RTS=off.");'),
    @('port = OpenPort(portName, true, false);', 'port = OpenPort(portName, false, false);'),
    @('Log("V160: HELLO confirmou " + detected + "; aguardando 180 ms antes do unico F0 desta abertura.");', 'Log("V161: HELLO confirmou " + detected + "; aguardando 450 ms antes do unico F0 desta abertura.");'),
    @('Thread.Sleep(180);\n\n                        // Regra critica: um unico F0 por abertura da COM.', 'Thread.Sleep(450);\n\n                        // Regra critica: um unico F0 por abertura da COM.')
)

foreach ($pair in $replacements) {
    $old = $pair[0]
    $new = $pair[1]
    if (-not $text.Contains($old)) { throw "V161: trecho nao localizado: $old" }
    $text = $text.Replace($old, $new)
}

# O patch v1.60 ainda se chama AcquireQualifiedPortV160; mantemos o nome para reduzir
# superficie de mudanca, mas a chamada principal passa a ser marcada como v1.61 no log.
$text = $text.Replace('Log("V160: iniciando sessao limpa "', 'Log("V161: iniciando sessao fisica "')
$text = $text.Replace('Log("V160: HELLO nao confirmado nesta abertura; fechando a COM sem enviar F0.");', 'Log("V161: HELLO nao confirmado nesta abertura; fechando a COM sem enviar F0.");')
$text = $text.Replace('Log("V160: F0 sem resposta conhecida; nenhum 38/34/0A sera enviado nesta abertura.");', 'Log("V161: F0 sem resposta conhecida; nenhum 38/34/0A sera enviado nesta abertura.");')
$text = $text.Replace('Log("V160: HELLO+F0 confirmados na mesma sessao limpa: 00 02 10 22 CB.");', 'Log("V161: HELLO+F0 confirmados no perfil fisico comprovado: 00 02 10 22 CB.");')
$text = $text.Replace('Log("V160: COM fechada; aguardando 1500 ms antes de nova sessao limpa.");', 'Log("V161: COM fechada; aguardando 1500 ms antes de nova sessao fisica.");')
$text = $text.Replace('throw new IOException("V160: 12 sessoes limpas sem confirmar HELLO+F0. "', 'throw new IOException("V161: 12 sessoes fisicas sem confirmar HELLO+F0. "')

# Depois que o F0 foi validado, o fluxo principal chama este helper antes do 38.
# A sessao fisica de referencia esperou aproximadamente 420 ms nesse ponto.
$oldHelper = @'
        private static void CaptureF0AlreadyQualifiedV159(SerialPort port)
        {
            // AcquireQualifiedPortV159 já confirmou F0 nesta mesma abertura da COM.
            // Não repetir F0 imediatamente evita o silêncio observado na bancada v1.58.
            if (port == null || !port.IsOpen) throw new InvalidOperationException("Sessao PG v1.59 nao esta aberta.");
        }
'@
$newHelper = @'
        private static void CaptureF0AlreadyQualifiedV159(SerialPort port)
        {
            // v1.61: o F0 ja foi validado na mesma abertura. A bancada fisica de
            // 2026-09-14 mostrou melhor confiabilidade com ~420 ms antes do 38.
            if (port == null || !port.IsOpen) throw new InvalidOperationException("Sessao PG qualificada nao esta aberta.");
            Log("V161: F0 validado; estabilizando 420 ms antes do 38.");
            Thread.Sleep(420);
        }
'@
if (-not $text.Contains($oldHelper)) { throw 'V161: helper CaptureF0AlreadyQualifiedV159 nao localizado.' }
$text = $text.Replace($oldHelper, $newHelper)

[IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false)))
Write-Host 'TP02 Full Capture v1.61 aplicado: DTR/RTS OFF, 450 ms HELLO-F0 e 420 ms F0-38.'
