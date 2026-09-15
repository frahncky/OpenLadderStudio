param(
    [string]$Source = 'TP02PgEmulator.cs',
    [string]$Rules = 'TP02PgEmulatorRules.txt',
    [string]$Output = 'TP02PgEmulator.build.cs'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$base = Split-Path -Parent $MyInvocation.MyCommand.Path

function Resolve-Local([string]$p) {
    if ([System.IO.Path]::IsPathRooted($p)) { return $p }
    return (Join-Path $base $p)
}

function Parse-HexBytes([string]$text) {
    $clean = ($text -replace '[^0-9A-Fa-f]',' ').Trim()
    if ([string]::IsNullOrWhiteSpace($clean)) { return @() }
    $parts = $clean -split '\s+'
    $bytes = @()
    foreach ($p in $parts) {
        if ($p.Length -ne 2) { throw "Byte hexadecimal invalido: $p" }
        $bytes += [Convert]::ToByte($p,16)
    }
    return ,$bytes
}

$sourcePath = Resolve-Local $Source
$rulesPath = Resolve-Local $Rules
$outputPath = Resolve-Local $Output

if (-not (Test-Path $sourcePath)) { throw "Fonte nao encontrada: $sourcePath" }
if (-not (Test-Path $rulesPath)) { throw "Regras nao encontradas: $rulesPath" }

$text = [IO.File]::ReadAllText($sourcePath)
$marker = '                default:'
$idx = $text.IndexOf($marker, [StringComparison]::Ordinal)
if ($idx -lt 0) { throw 'Nao foi encontrado o bloco default do switch do emulador.' }

# 33 e Write PLC Program confirmado offline; nao pode ser sobrescrito por regra externa.
$known = @('F0','38','34','33','0A','14')
$cases = New-Object Text.StringBuilder
$count = 0

foreach ($raw in [IO.File]::ReadAllLines($rulesPath)) {
    $line = $raw.Trim()
    if ($line.Length -eq 0 -or $line.StartsWith('#')) { continue }
    $parts = $line.Split('|')
    if ($parts.Length -lt 3) { throw "Regra invalida: $line" }

    $cmd = $parts[0].Trim().ToUpperInvariant()
    if ($cmd.StartsWith('0X')) { $cmd = $cmd.Substring(2) }
    if ($cmd.Length -ne 2) { throw "CMD invalido: $cmd" }
    [byte]$cmdByte = [Convert]::ToByte($cmd,16)
    if ($known -contains $cmd) { throw "CMD $cmd ja e tratado nativamente." }

    [int]$delay = 0
    if (-not [Int32]::TryParse($parts[1].Trim(), [ref]$delay)) { throw "DELAY invalido: $($parts[1])" }
    if ($delay -lt 0 -or $delay -gt 10000) { throw "DELAY fora da faixa: $delay" }

    $resp = Parse-HexBytes $parts[2]
    if ($resp.Count -eq 0) { throw "Resposta vazia para CMD $cmd" }

    $sum = 0
    foreach ($b in $resp) { $sum = ($sum + [int]$b) -band 0xFF }
    if ($sum -ne 0xFF) { throw ("Resposta do CMD {0} nao fecha soma FF; soma=0x{1:X2}" -f $cmd,$sum) }

    $label = if ($parts.Length -ge 4) { $parts[3].Trim() } else { 'regra externa' }
    $safeLabel = $label.Replace('\\','\\\\').Replace('"','\\"')
    $bytesCs = ($resp | ForEach-Object { '0x' + $_.ToString('X2') }) -join ', '

    [void]$cases.AppendLine(('                case 0x{0}:' -f $cmd))
    [void]$cases.AppendLine(('                    SleepFor({0});' -f $delay))
    [void]$cases.AppendLine(('                    Send("EMU -> PC12 REGRA CMD=0x{0} {1}", new byte[] {{ {2} }});' -f $cmd,$safeLabel,$bytesCs))
    [void]$cases.AppendLine('                    break;')
    [void]$cases.AppendLine('')
    $count++
}

$out = $text.Insert($idx, $cases.ToString())

# Quando houver programa recebido por PG33 (ou seed de bancada), 38/34 refletem
# esse banco. Sem banco ativo, TP02PgReadback preserva o fixture historico.
$old38 = 'Send("EMU -> PC12 38", Response38);'
$new38 = 'Send("EMU -> PC12 38", TP02PgReadback.Build38(ProgramWords, ProgramWordValid, HighestProgramStep, Response38));'
if (-not $out.Contains($old38)) { throw 'Ponto de integracao do comando 38 nao encontrado.' }
$out = $out.Replace($old38, $new38)

$old34 = 'Send("EMU -> PC12 34", BuildProgramReadResponse(frame));'
$new34 = 'Send("EMU -> PC12 34", TP02PgReadback.Build34(frame, ProgramWords, ProgramWordValid, HighestProgramStep, ProgramPage0000));'
if (-not $out.Contains($old34)) { throw 'Ponto de integracao do comando 34 nao encontrado.' }
$out = $out.Replace($old34, $new34)

# O self-test integrado usa exatamente o mesmo decoder PG33 do emulador.
$privateDecode = '        private static bool TryDecodePg33('
$internalDecode = '        internal static bool TryDecodePg33('
if (-not $out.Contains($privateDecode)) { throw 'TryDecodePg33 privado nao encontrado.' }
$out = $out.Replace($privateDecode, $internalDecode)

# Argumentos do cenario v1.56 sao tratados sem poluir o parser historico.
$parseAnchor = @'
                if (!a.StartsWith("--", StringComparison.Ordinal) && port.Length == 0)
                    port = a;
'@
$parseReplacement = @'
                if (TP02PgV156Scenario.TryApplyArgument(a))
                    continue;
                if (!a.StartsWith("--", StringComparison.Ordinal) && port.Length == 0)
                    port = a;
'@
if (-not $out.Contains($parseAnchor)) { throw 'Ancora ParseArguments v1.56 nao encontrada.' }
$out = $out.Replace($parseAnchor, $parseReplacement)

# Self-test roda sem COM; ALL executa a matriz ampla e V156 preserva o teste focal.
$mainAnchor = @'
            string portName = ParseArguments(args);
            if (string.IsNullOrEmpty(portName))
                portName = AskPort();
'@
$mainReplacement = @'
            string portName = ParseArguments(args);
            if (TP02PgV156Scenario.DisableUnknownAck)
                AutoAckUnknown = false;

            if (string.Equals(TP02PgV156Scenario.SelfTestMode, "ALL", StringComparison.Ordinal))
            {
                Environment.ExitCode = TP02PgComprehensiveSelfTest.RunAll(true);
                return;
            }
            if (string.Equals(TP02PgV156Scenario.SelfTestMode, "V156", StringComparison.Ordinal))
            {
                Environment.ExitCode = TP02PgReadbackSelfTest.RunV156Scenario(true);
                return;
            }

            if (string.IsNullOrEmpty(portName))
                portName = AskPort();
'@
if (-not $out.Contains($mainAnchor)) { throw 'Ancora Main/self-test v1.56 nao encontrada.' }
$out = $out.Replace($mainAnchor, $mainReplacement)

# O seed boundary323 representa exatamente o programa de 323 words validado em
# bancada; fica ativo antes de qualquer HELLO/F0/PG34 do cliente.
$seedAnchor = @'
            SeedMemory();
            PrepareCaptureDirectory();
'@
$seedReplacement = @'
            SeedMemory();
            PrepareCaptureDirectory();
            int seededHighestV156 = TP02PgV156Scenario.SeedProgramIfRequested(ProgramWords, ProgramWordValid);
            if (seededHighestV156 >= 0)
            {
                HighestProgramStep = seededHighestV156;
                Log("V156 SEED", "boundary323 carregado: 323 words; END=0322.");
                SaveProgramDump();
            }
'@
if (-not $out.Contains($seedAnchor)) { throw 'Ancora SeedMemory v1.56 nao encontrada.' }
$out = $out.Replace($seedAnchor, $seedReplacement)

# Simula HELLO tardio sem fechar/reabrir a porta virtual. No cenario padrao a
# primeira resposta vem na 5a tentativa, como observado em bancada.
$helloAnchor = @'
            LogFrame("PC12 -> EMU HELLO", request, false);
            SleepFor(220);

            byte[] response = HelloC0 ? HelloResponseC0 : HelloResponse80;
            Send("EMU -> PC12 HELLO", response);
'@
$helloReplacement = @'
            LogFrame("PC12 -> EMU HELLO", request, false);
            string helloGateV156;
            if (!TP02PgV156Scenario.ShouldRespondHello(out helloGateV156))
            {
                Log("V156 HELLO SILENCIOSO", helloGateV156);
                return;
            }
            Log("V156 HELLO GATE", helloGateV156);
            SleepFor(220);

            byte[] response = HelloC0 ? HelloResponseC0 : HelloResponse80;
            Send("EMU -> PC12 HELLO", response);
'@
if (-not $out.Contains($helloAnchor)) { throw 'Ancora HandleHello v1.56 nao encontrada.' }
$out = $out.Replace($helloAnchor, $helloReplacement)

# F0 pode ficar mudo nas primeiras tentativas, mas permanece na mesma sessao.
$f0Anchor = @'
                case 0xF0:
                    SleepFor(220);
                    Send("EMU -> PC12 F0", F0Response);
                    break;
'@
$f0Replacement = @'
                case 0xF0:
                {
                    string f0GateV156;
                    if (!TP02PgV156Scenario.ShouldRespondF0(out f0GateV156))
                    {
                        Log("V156 F0 SILENCIOSO", f0GateV156);
                        break;
                    }
                    Log("V156 F0 GATE", f0GateV156);
                    SleepFor(220);
                    Send("EMU -> PC12 F0", F0Response);
                    break;
                }
'@
if (-not $out.Contains($f0Anchor)) { throw 'Ancora F0 v1.56 nao encontrada.' }
$out = $out.Replace($f0Anchor, $f0Replacement)

# Deixa o cenario visivel no console para evitar confundir emulacao com PLC real.
$headerAnchor = '            Console.WriteLine(" ATENCAO     : use COM virtual; nao use a COM fisica do PLC.");'
$headerReplacement = '            Console.WriteLine(" V156        : " + TP02PgV156Scenario.Describe());' + "`r`n" + $headerAnchor
if (-not $out.Contains($headerAnchor)) { throw 'Ancora do cabecalho v1.56 nao encontrada.' }
$out = $out.Replace($headerAnchor, $headerReplacement)

[IO.File]::WriteAllText($outputPath, $out, [Text.Encoding]::UTF8)
Write-Host ("TP02 emulator build source preparado: {0} regra(s) externa(s) + readback PG33/38/34 + matriz completa + cenario v1.56." -f $count)
