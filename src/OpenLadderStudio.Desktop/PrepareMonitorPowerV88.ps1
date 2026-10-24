$ErrorActionPreference = 'Stop'
# V88 - O monitor do PLC real acende o diagrama do editor.
# Roda depois do V87, ultimo passo antes da compilacao do shell. O shell nao
# conhece modelo: liga o destino generico IStudioHost.ShowRungPower ao mesmo
# repasse que a simulacao ja usa (OnSimulatorPower, do V86). Quem calcula a
# energizacao e o driver, a partir dos bits que leu.
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V88: UniversalStudioShell.build.cs nao encontrado.' }

function Rep([string]$text, [string]$old, [string]$new, [string]$label) {
    # O script pode vir com CRLF (checkout no Windows); o texto trabalhado usa LF.
    $old = $old.Replace("`r`n", "`n")
    $new = $new.Replace("`r`n", "`n")
    if (-not $text.Contains($old)) { throw "V88: ancora nao encontrada ($label). Ajuste PrepareMonitorPowerV88.ps1." }
    return $text.Replace($old, $new)
}

$shell = [IO.File]::ReadAllText($shellPath).Replace("`r`n", "`n")

if (-not $shell.Contains('private void OnSimulatorPower(bool[] power)')) { throw 'V88: OnSimulatorPower (V86) nao encontrado.' }

$shell = Rep $shell '                    delegate(string text) { if (statusText != null) statusText.Text = text; });
            return studioHost;' '                    delegate(string text) { if (statusText != null) statusText.Text = text; });
            StudioHostAdapter adapter = studioHost as StudioHostAdapter;
            if (adapter != null && adapter.RungPowerSink == null) adapter.RungPowerSink = OnDriverPower;
            return studioHost;' 'destino do destaque no Host()'

# Arbitragem: a simulacao tem prioridade. Enquanto ela publica um resultado do
# programa do editor, o destaque vindo do driver e descartado.
$shell = Rep $shell '                simulatorForm.PowerChanged += OnSimulatorPower;' '                simulatorForm.PowerChanged += OnSimulatorPower;
                simulatorForm.PowerChanged += delegate(bool[] p) { simulatorOwnsPower = p != null; };' 'arbitragem do simulador'

$methods = @'
        private bool simulatorOwnsPower;
        private bool driverPowerSuspendedNotice;

        private void OnDriverPower(bool[] power)
        {
            if (simulatorOwnsPower)
            {
                if (power != null && !driverPowerSuspendedNotice && statusText != null)
                    statusText.Text = "Destaque do PLC suspenso: a simulação está usando o diagrama.";
                driverPowerSuspendedNotice = power != null;
                return;
            }
            driverPowerSuspendedNotice = false;
            OnSimulatorPower(power);
        }

        /// <summary>Aba fechada: o documento so fica oculto, entao o driver e avisado para parar o que estiver fazendo.</summary>
        private void NotifyDocumentClosed(Form document)
        {
            if (document == null) return;
            if (document == simulatorForm && simulatorOwnsPower)
            {
                simulatorOwnsPower = false;
                OnSimulatorPower(null);
            }
            StudioHostAdapter adapter = studioHost as StudioHostAdapter;
            if (adapter != null) adapter.RaiseDocumentClosed(document);
        }

        private void OnSimulatorPower(bool[] power)
'@
$shell = Rep $shell '        private void OnSimulatorPower(bool[] power)' $methods.TrimEnd() 'metodos de arbitragem'

$shell = Rep $shell '                if (t.Document != null && !t.Document.IsDisposed) t.Document.Visible = false;' '                if (t.Document != null && !t.Document.IsDisposed) t.Document.Visible = false;
                NotifyDocumentClosed(t.Document);' 'aviso de documento fechado'

foreach ($dead in @('IsTp02', 'RequireTp02', 'weg\.tp02\.serial')) {
    if ($shell -match $dead) { throw "V88: referencia proibida no shell ($dead)." }
}

[IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($true)))
Write-Host 'V88: destaque do diagrama a partir do monitor do PLC ligado ao editor.'
