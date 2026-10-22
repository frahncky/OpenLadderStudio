$ErrorActionPreference = 'Stop'
$path = Join-Path (Get-Location) 'TP02FullProtocolCapture.build.cs'
if (-not (Test-Path -LiteralPath $path)) { throw 'V162: fonte de build nao encontrado.' }
$text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)

# O tempo legado RX(Nms) inclui a janela de silencio apos o ultimo byte.
# O instante registrado e de leitura pela aplicacao, nao de chegada no fio.
$changes = @(
    @('private static byte[] ReadBurst(SerialPort port, int timeoutMs, int quietMs)', 'private static byte[] ReadBurst(SerialPort port, int timeoutMs, int quietMs, Stopwatch sw, out long firstReadMs, out long lastReadMs)'),
    @('List<byte> bytes = new List<byte>(); DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs); DateTime last = DateTime.MinValue;', 'firstReadMs = -1; lastReadMs = -1; List<byte> bytes = new List<byte>(); DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs); DateTime last = DateTime.MinValue;'),
    @('for (int i = 0; i < got; i++) bytes.Add(chunk[i]); last = DateTime.UtcNow;', 'if (got > 0) { long observedMs = sw.ElapsedMilliseconds; if (firstReadMs < 0) firstReadMs = observedMs; lastReadMs = observedMs; for (int i = 0; i < got; i++) bytes.Add(chunk[i]); last = DateTime.UtcNow; }'),
    @('port.Write(request, 0, request.Length); byte[] raw = ReadBurst(port, timeoutMs, quietMs); sw.Stop();', 'port.Write(request, 0, request.Length); long firstReadMs, lastReadMs; byte[] raw = ReadBurst(port, timeoutMs, quietMs, sw, out firstReadMs, out lastReadMs); sw.Stop();')
)
foreach ($change in $changes) {
    $old = $change[0]; $new = $change[1]
    $count = [regex]::Matches($text, [regex]::Escape($old)).Count
    if ($count -ne 1) { throw "V162: ancora exige uma ocorrencia, encontrou $count : $old" }
    $text = $text.Replace($old, $new)
}
$anchor = '            CaptureEntry e = new CaptureEntry(); e.Sequence = seq;'
$count = [regex]::Matches($text, [regex]::Escape($anchor)).Count
if ($count -ne 1) { throw "V162: ancora CaptureEntry exige uma ocorrencia, encontrou $count" }
$line = '            Log(label + " RX-TIMING first_read_ms=" + firstReadMs.ToString(CultureInfo.InvariantCulture) + " last_read_ms=" + lastReadMs.ToString(CultureInfo.InvariantCulture) + " elapsed_ms=" + sw.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + " quiet_ms=" + quietMs.ToString(CultureInfo.InvariantCulture) + " (read timestamps, not wire arrival)");'
$text = $text.Replace($anchor, $line + "`r`n" + $anchor)
if (-not $text.Contains('RX-TIMING first_read_ms=')) { throw 'V162: medicao ausente.' }
if ($text.Contains('ReadBurst(port, timeoutMs, quietMs);')) { throw 'V162: chamada antiga ainda presente.' }
[IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false)))
Write-Host 'TP02 Full Capture v1.62: leituras instrumentadas; TX, perfis, timeouts e allowlist SAFE preservados.'
