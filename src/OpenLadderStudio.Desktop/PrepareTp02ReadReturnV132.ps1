$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V132 return: UniversalStudioShell.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)

$oldFocus = @'
                        if (loaded)
                        {
                            try
                            {
                                Hide();
                                if (Owner != null) Owner.Activate();
                                ladderForm.BringToFront();
                                ladderForm.Focus();
                            }
                            catch { }
                        }
'@
$newFocus = @'
                        if (loaded)
                        {
                            try
                            {
                                if (Owner != null) Owner.Activate();
                                ladderForm.BringToFront();
                                ladderForm.Focus();
                            }
                            catch { }
                        }
'@
if (-not $shell.Contains($oldFocus)) { throw 'V132 return: bloco de foco READ nao encontrado.' }
$shell = $shell.Replace($oldFocus, $newFocus)

$oldMessage = @'
                        MessageBox.Show(loaded && Owner != null ? Owner : this, successText,
                            "TP02 - READ PG", MessageBoxButtons.OK, MessageBoxIcon.Information);
'@
$newMessage = @'
                        MessageBox.Show(loaded && Owner != null ? Owner : this, successText,
                            "TP02 - READ PG", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        if (loaded)
                        {
                            SetBusy(false);
                            Close();
                            return;
                        }
'@
if (-not $shell.Contains($oldMessage)) { throw 'V132 return: mensagem READ nao encontrada.' }
$shell = $shell.Replace($oldMessage, $newMessage)

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'TP02 READ V132: apos importar, fecha o dialogo PG e retorna ao Ladder principal.' -ForegroundColor Cyan
