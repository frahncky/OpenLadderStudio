$ErrorActionPreference = 'Stop'
# V87 - Layout da janela do Studio: proporcoes padrao, cartao de propriedades,
# no do controlador na arvore e barra de status com o modo da aba ativa.
# Roda depois do V86, ultimo passo antes da compilacao do shell.
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$shellPath = Join-Path $root 'UniversalStudioShell.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V87: UniversalStudioShell.build.cs nao encontrado.' }

function Rep([string]$text, [string]$old, [string]$new, [string]$label) {
    # O script pode vir com CRLF (checkout no Windows); o texto trabalhado usa LF.
    $old = $old.Replace("`r`n", "`n")
    $new = $new.Replace("`r`n", "`n")
    if (-not $text.Contains($old)) { throw "V87: ancora nao encontrada ($label). Ajuste PrepareWindowLayoutV87.ps1." }
    return $text.Replace($old, $new)
}

# Casa as chaves ignorando literais de texto e comentarios.
function Find-BlockEnd([string]$text, [int]$open) {
    $depth = 0
    $i = $open
    $n = $text.Length
    while ($i -lt $n) {
        $c = $text[$i]
        if ($c -eq '/' -and $i + 1 -lt $n -and $text[$i + 1] -eq '/') { while ($i -lt $n -and $text[$i] -ne "`n") { $i++ }; continue }
        if ($c -eq '/' -and $i + 1 -lt $n -and $text[$i + 1] -eq '*') { $close = $text.IndexOf('*/', $i + 2); if ($close -lt 0) { return -1 }; $i = $close + 2; continue }
        if ($c -eq '@' -and $i + 1 -lt $n -and $text[$i + 1] -eq '"') {
            $i += 2
            while ($i -lt $n) {
                if ($text[$i] -eq '"') { if ($i + 1 -lt $n -and $text[$i + 1] -eq '"') { $i += 2; continue }; break }
                $i++
            }
            $i++
            continue
        }
        if ($c -eq '"' -or $c -eq "'") {
            $quote = $c
            $i++
            while ($i -lt $n -and $text[$i] -ne $quote) { if ($text[$i] -eq '\') { $i++ }; $i++ }
            $i++
            continue
        }
        if ($c -eq '{') { $depth++ }
        elseif ($c -eq '}') { $depth--; if ($depth -eq 0) { return $i + 1 } }
        $i++
    }
    return -1
}

function Remove-Method([string]$text, [string]$signature, [string]$name) {
    $s = $text.IndexOf($signature)
    if ($s -lt 0) { throw "V87: metodo nao encontrado ($name)." }
    $lineStart = $text.LastIndexOf("`n", $s) + 1
    $open = $text.IndexOf('{', $s)
    $end = Find-BlockEnd $text $open
    if ($end -lt 0) { throw "V87: fim do metodo nao encontrado ($name)." }
    if ($end -lt $text.Length -and $text[$end] -eq "`n") { $end++ }
    return $text.Substring(0, $lineStart) + $text.Substring($end)
}

$shell = [IO.File]::ReadAllText($shellPath).Replace("`r`n", "`n")

# 1. Proporcoes padrao. O divisor fixo em 920 px dava ao painel de instrucoes 300 px
# em 1500 e 700 px em 1920; agora a navegacao e o painel seguem a largura da janela.
$shell = Rep $shell '                int navMax = navSplit.ClientSize.Width - navSplit.Panel2MinSize - navSplit.SplitterWidth;
                if (navMax >= navSplit.Panel1MinSize)
                    navSplit.SplitterDistance = Math.Min(252, navMax);
                int workMax = inspectorSplit.ClientSize.Width - inspectorSplit.Panel2MinSize - inspectorSplit.SplitterWidth;
                if (workMax >= inspectorSplit.Panel1MinSize)
                    inspectorSplit.SplitterDistance = Math.Min(920, workMax);' '                ApplyDefaultProportions();' 'proporcoes ao abrir'
$shell = Rep $shell '                navPanel.Width = 252;' '                ApplyDefaultProportions();' 'proporcao da navegacao no reset'
$shell = Rep $shell '            if (inspector != null) inspector.Width = 292;
' '' 'largura do inspetor no reset'
$shell = Rep $shell 'modeText.Width = 650;' 'modeText.Width = 720;' 'largura do texto de modo'

# 2. Cartao de propriedades no rodape do painel de instrucoes.
$shell = Rep $shell '            host.Controls.Add(list);
            host.Controls.Add(searchHost);' '            properties = new SelectionPropertiesCard();
            host.Controls.Add(list);
            host.Controls.Add(properties);
            host.Controls.Add(searchHost);' 'cartao de propriedades'
$shell = Rep $shell 'PrepareLadderForStudio(ladderForm);' 'PrepareLadderForStudio(ladderForm);
                HookSelectionCard();' 'ligacao do cartao ao editor'
$shell = Rep $shell '                if (key == "MAIN") ShowLadder();' '                if (key == "MAIN") ShowLadder();
                else if (key == "DEVICE") ShowDeviceManager();' 'no do controlador na arvore'

# 1b. Botoes da barra: a largura minima de 92 px estourava a janela em 1180 px; o rotulo define a largura.
$shell = Rep $shell 'b.Width = Math.Max(b.MeasureWidth(), 92);' 'b.Width = Math.Max(b.MeasureWidth() - 8, 76);' 'largura dos botoes da barra'

# 1c. A barra de ferramentas com os quatro botoes de PLC e o zoom precisa de cerca de 1230 px.
$shell = Rep $shell 'MinimumSize = new Size(1180, 720);' 'MinimumSize = new Size(1280, 720);' 'largura minima da janela'

# 3. Barra de status: modelo, conexao, modo da aba ativa e zoom.
$shell = Remove-Method $shell 'private void V74UpdateModeText()' 'V74UpdateModeText'
$shell = Rep $shell '            lastTab = tab;
            if (tab == null) return;' '            lastTab = tab;
            V74UpdateModeText();
            if (tab == null) return;' 'modo ao trocar de aba'

$block = @'
        private SelectionPropertiesCard properties;
        private TreeNode deviceNode;

        private void ApplyDefaultProportions()
        {
            if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { if (v73ProjectTree != null) v73ProjectTree.SelectedNode = null; });
            if (navSplit == null || inspectorSplit == null) return;
            int navWidth = Math.Max(220, Math.Min(280, (int)(ClientSize.Width * 0.17)));
            int navMax = navSplit.ClientSize.Width - navSplit.Panel2MinSize - navSplit.SplitterWidth;
            if (navMax >= navSplit.Panel1MinSize)
                navSplit.SplitterDistance = Math.Max(navSplit.Panel1MinSize, Math.Min(navWidth, navMax));
            navSplit.PerformLayout();

            int inspectorWidth = Math.Max(inspectorSplit.Panel2MinSize, Math.Max(300, Math.Min(360, (int)(ClientSize.Width * 0.21))));
            int distance = inspectorSplit.ClientSize.Width - inspectorWidth - inspectorSplit.SplitterWidth;
            int workMax = inspectorSplit.ClientSize.Width - inspectorSplit.Panel2MinSize - inspectorSplit.SplitterWidth;
            if (distance >= inspectorSplit.Panel1MinSize)
                inspectorSplit.SplitterDistance = Math.Min(distance, workMax);
        }

        private string CurrentModeName()
        {
            StudioTab tab = tabStrip == null ? null : tabStrip.Selected;
            string key = tab == null ? "LD" : tab.Key;
            switch (key)
            {
                case "SIM": return "SIMULAÇÃO";
                case "MON": return "MONITOR";
                case "READ": return "LEITURA";
                case "PLC": return "COMUNICAÇÃO";
                case "UPD": return "ATUALIZAÇÃO";
            }
            return "EDIÇÃO";
        }

        private void V74UpdateModeText()
        {
            int zoom = V74ReadZoomPercent();
            if (v74ZoomLabel != null) v74ZoomLabel.Text = zoom.ToString() + "%";
            if (modeText != null)
            {
                string model = currentProfile == null ? "SEM PLC" : currentProfile.Model;
                string protocol = currentProfile == null || string.IsNullOrEmpty(currentProfile.Protocol) ? string.Empty : "  (" + currentProfile.Protocol + ")";
                modeText.Text = "PLC: " + model + protocol + "   │   ○ OFF-LINE   │   MODO: " + CurrentModeName() + "   │   ZOOM: " + zoom.ToString() + "%";
            }
            RefreshDeviceNode();
        }

        /// <summary>Controlador ativo como primeiro item da arvore; clicar abre a escolha do modelo.</summary>
        private void RefreshDeviceNode()
        {
            if (v73ProjectRoot == null) return;
            if (deviceNode == null)
            {
                deviceNode = new TreeNode();
                deviceNode.Tag = "DEVICE";
                v73ProjectRoot.Nodes.Insert(0, deviceNode);
                v73ProjectTree.SelectedNode = null;
            }
            deviceNode.Text = "Controlador: " + (currentProfile == null ? "nenhum" : currentProfile.Manufacturer + " " + currentProfile.Model);
        }

        private void HookSelectionCard()
        {
            try
            {
                FieldInfo field = typeof(LadderEditorForm).GetField("canvas", BindingFlags.Instance | BindingFlags.NonPublic);
                LadderCanvas canvas = field == null ? null : field.GetValue(ladderForm) as LadderCanvas;
                if (canvas != null) canvas.Paint += delegate { RefreshSelectionCard(); };
            }
            catch { }
            RefreshSelectionCard();
        }

        private static string ElementKindName(LadderElementType type)
        {
            switch (type)
            {
                case LadderElementType.ContactNO: return "Contato NA";
                case LadderElementType.ContactNC: return "Contato NF";
                case LadderElementType.EdgeUp: return "Borda de subida";
                case LadderElementType.EdgeDown: return "Borda de descida";
                case LadderElementType.Coil: return "Bobina";
                case LadderElementType.Set: return "Bobina SET";
                case LadderElementType.Reset: return "Bobina RESET";
                case LadderElementType.Timer: return "Temporizador";
                case LadderElementType.Counter: return "Contador";
                case LadderElementType.Function: return "Função especial";
                case LadderElementType.End: return "END";
            }
            return "Vazio";
        }

        private void RefreshSelectionCard()
        {
            if (properties == null || ladderForm == null || ladderForm.IsDisposed) return;
            try
            {
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo field = typeof(LadderEditorForm).GetField("canvas", flags);
                LadderCanvas canvas = field == null ? null : field.GetValue(ladderForm) as LadderCanvas;
                MethodInfo get = typeof(LadderEditorForm).GetMethod("GetSelectedElement", flags);
                LadderElement element = get == null ? null : get.Invoke(ladderForm, null) as LadderElement;
                if (canvas == null || canvas.SelectedRung < 0 || element == null)
                {
                    properties.ShowSelection(-1, -1, 0, null, string.Empty, string.Empty);
                    return;
                }
                string parameter = element.Parameter ?? string.Empty;
                if (!string.IsNullOrEmpty(element.Mode)) parameter = (parameter + " " + element.Mode).Trim();
                properties.ShowSelection(canvas.SelectedRung, canvas.SelectedColumn, canvas.SelectedLane,
                    ElementKindName(element.Type), element.Address ?? string.Empty, parameter);
            }
            catch { }
        }

        private void ShowLadder()
'@
$shell = Rep $shell '        private void ShowLadder()' $block.TrimEnd() 'insercao do layout'

[IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($true)))
Write-Host 'V87: layout da janela aplicado.'
