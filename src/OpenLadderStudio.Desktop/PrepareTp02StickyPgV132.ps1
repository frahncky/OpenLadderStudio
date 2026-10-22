$ErrorActionPreference = 'Stop'

$shellPath = Join-Path (Get-Location) 'UniversalStudioShell.build.cs'
$ladderPath = Join-Path (Get-Location) 'LadderEditor.build.cs'
if (-not (Test-Path -LiteralPath $shellPath)) { throw 'V132: UniversalStudioShell.build.cs nao encontrado.' }
if (-not (Test-Path -LiteralPath $ladderPath)) { throw 'V132: LadderEditor.build.cs nao encontrado.' }
$shell = [System.IO.File]::ReadAllText($shellPath)
$ladder = [System.IO.File]::ReadAllText($ladderPath)

function Replace-Section([string]$text, [string]$startAnchor, [string]$endAnchor, [string]$replacement, [string]$label) {
    $start = $text.IndexOf($startAnchor, [System.StringComparison]::Ordinal)
    if ($start -lt 0) { throw "V132: inicio nao encontrado ($label)." }
    $end = $text.IndexOf($endAnchor, $start + $startAnchor.Length, [System.StringComparison]::Ordinal)
    if ($end -lt 0) { throw "V132: fim nao encontrado ($label)." }
    return $text.Substring(0, $start) + $replacement + $text.Substring($end)
}

function Insert-Before([string]$text, [string]$anchor, [string]$addition, [string]$label) {
    $index = $text.IndexOf($anchor, [System.StringComparison]::Ordinal)
    if ($index -lt 0) { throw "V132: ancora nao encontrada ($label)." }
    return $text.Substring(0, $index) + $addition + $text.Substring($index)
}

# -----------------------------------------------------------------------------
# 1. FAST/STICKY PG: memoriza COM + DTR/RTS que realmente qualificaram o link.
# -----------------------------------------------------------------------------
$acqStart = '        private SerialPort AcquireStablePgPortV93'
$acqEnd = '        private SerialPort TryAcquireFastV127'
$acq = @'
        private static readonly object PgProfileCacheLockV132 = new object();
        private static bool pgProfileCacheLoadedV132;
        private static bool pgProfileCacheValidV132;
        private static string pgCachedPortV132 = string.Empty;
        private static bool pgCachedDtrV132;
        private static bool pgCachedRtsV132;
        private static string pgCachedProfileNameV132 = string.Empty;

        private static string PgProfileCachePathV132()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(root)) root = Application.StartupPath;
            return Path.Combine(root, "OpenLadderStudio", "tp02-pg-last-profile.txt");
        }

        private static void LoadPgProfileCacheV132()
        {
            lock (PgProfileCacheLockV132)
            {
                if (pgProfileCacheLoadedV132) return;
                pgProfileCacheLoadedV132 = true;
                try
                {
                    string path = PgProfileCachePathV132();
                    if (!File.Exists(path)) return;
                    string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                    string port = string.Empty;
                    string name = string.Empty;
                    bool dtr = false;
                    bool rts = false;
                    bool haveDtr = false;
                    bool haveRts = false;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i] ?? string.Empty;
                        int sep = line.IndexOf('=');
                        if (sep <= 0) continue;
                        string key = line.Substring(0, sep).Trim().ToUpperInvariant();
                        string value = line.Substring(sep + 1).Trim();
                        if (key == "PORT") port = value;
                        else if (key == "DTR") { dtr = value == "1"; haveDtr = true; }
                        else if (key == "RTS") { rts = value == "1"; haveRts = true; }
                        else if (key == "NAME") name = value;
                    }
                    if (!string.IsNullOrEmpty(port) && haveDtr && haveRts)
                    {
                        pgCachedPortV132 = port;
                        pgCachedDtrV132 = dtr;
                        pgCachedRtsV132 = rts;
                        pgCachedProfileNameV132 = string.IsNullOrEmpty(name) ? "perfil lembrado" : name;
                        pgProfileCacheValidV132 = true;
                    }
                }
                catch { pgProfileCacheValidV132 = false; }
            }
        }

        private static void RememberPgProfileV132(string portName, bool dtr, bool rts, string profileName)
        {
            if (string.IsNullOrEmpty(portName)) return;
            lock (PgProfileCacheLockV132)
            {
                pgProfileCacheLoadedV132 = true;
                pgProfileCacheValidV132 = true;
                pgCachedPortV132 = portName;
                pgCachedDtrV132 = dtr;
                pgCachedRtsV132 = rts;
                pgCachedProfileNameV132 = string.IsNullOrEmpty(profileName) ? "perfil qualificado" : profileName;
                try
                {
                    string path = PgProfileCachePathV132();
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllLines(path, new string[]
                    {
                        "PORT=" + portName,
                        "DTR=" + (dtr ? "1" : "0"),
                        "RTS=" + (rts ? "1" : "0"),
                        "NAME=" + pgCachedProfileNameV132
                    }, Encoding.UTF8);
                }
                catch { }
            }
        }

        private SerialPort TryAcquireSpecificProfileV132(string portName, bool dtr, bool rts,
            string profileName, int helloTimeoutMs, int f0TimeoutMs,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;
            SerialPort serial = null;
            bool keepOpen = false;
            try
            {
                serial = new SerialPort(portName, 19200, Parity.Odd, 8, StopBits.One);
                serial.Handshake = Handshake.None;
                serial.DtrEnable = dtr;
                serial.RtsEnable = rts;
                serial.ReadTimeout = 60;
                serial.WriteTimeout = 1000;
                serial.Open();
                serial.DiscardInBuffer();
                serial.DiscardOutBuffer();
                Thread.Sleep(90);

                for (int helloAttempt = 1; helloAttempt <= 2; helloAttempt++)
                {
                    serial.DiscardInBuffer();
                    serial.Write(HelloRequest, 0, HelloRequest.Length);
                    byte[] helloRaw = ReadUntilSequenceV127(serial, HelloStop, HelloRun, helloTimeoutMs);
                    AppendLogSafe("V132 STICKY HELLO " + profileName + " / "
                        + helloAttempt.ToString(CultureInfo.InvariantCulture) + " RX="
                        + (helloRaw.Length == 0 ? "[]" : ToHex(helloRaw)));

                    if (Contains(helloRaw, HelloRun))
                    {
                        state = "RUN";
                        acquisitionLabel = "STICKY " + profileName + " / HELLO "
                            + helloAttempt.ToString(CultureInfo.InvariantCulture);
                        keepOpen = true;
                        return serial;
                    }
                    if (!Contains(helloRaw, HelloStop)) continue;

                    serial.DiscardInBuffer();
                    serial.Write(F0Request, 0, F0Request.Length);
                    byte[] f0Raw = ReadUntilSequenceV127(serial, F0Response, null, f0TimeoutMs);
                    AppendLogSafe("V132 STICKY F0 " + profileName + " RX="
                        + (f0Raw.Length == 0 ? "[]" : ToHex(f0Raw)));
                    if (Contains(f0Raw, F0Response))
                    {
                        state = "STOP";
                        acquisitionLabel = "STICKY " + profileName + " / HELLO "
                            + helloAttempt.ToString(CultureInfo.InvariantCulture);
                        keepOpen = true;
                        return serial;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                AppendLogSafe("V132 STICKY perfil indisponivel: " + profileName + " - " + ex.Message);
                return null;
            }
            finally
            {
                if (!keepOpen && serial != null) ClosePort(serial);
            }
        }

        private SerialPort TryAcquireRememberedProfileV132(string portName, int round,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;
            LoadPgProfileCacheV132();
            if (!pgProfileCacheValidV132) return null;
            if (!string.Equals(pgCachedPortV132, portName, StringComparison.OrdinalIgnoreCase)) return null;

            AppendLogSafe("V132 STICKY: ultimo perfil qualificado primeiro: " + pgCachedProfileNameV132 + ".");
            SerialPort remembered = TryAcquireSpecificProfileV132(portName,
                pgCachedDtrV132, pgCachedRtsV132, pgCachedProfileNameV132,
                round == 1 ? 420 : 560, round == 1 ? 650 : 820,
                out state, out acquisitionLabel);
            if (remembered != null) acquisitionLabel += " / cache V132";
            return remembered;
        }

        private SerialPort AcquireStablePgPortV93(string portName, int round,
            out string state, out string acquisitionLabel)
        {
            state = string.Empty;
            acquisitionLabel = string.Empty;

            SerialPort remembered = TryAcquireRememberedProfileV132(portName, round,
                out state, out acquisitionLabel);
            if (remembered != null)
            {
                AppendLogSafe("V132 STICKY QUALIFICADO: reconexao rapida no perfil lembrado.");
                return remembered;
            }

            LoadPgProfileCacheV132();
            if (!pgProfileCacheValidV132 || !string.Equals(pgCachedPortV132, portName, StringComparison.OrdinalIgnoreCase))
            {
                for (int preferredAttempt = 1; preferredAttempt <= 2; preferredAttempt++)
                {
                    SerialPort preferred = TryAcquireSpecificProfileV132(portName, true, true,
                        "19200 8O1 DTR=on RTS=on",
                        preferredAttempt == 1 ? 520 : 700,
                        preferredAttempt == 1 ? 720 : 900,
                        out state, out acquisitionLabel);
                    if (preferred != null)
                    {
                        RememberPgProfileV132(portName, true, true, "19200 8O1 DTR=on RTS=on");
                        acquisitionLabel += " / preferencial V132";
                        AppendLogSafe("V132 PREFERENCIAL QUALIFICADO: perfil salvo para proximas operacoes.");
                        return preferred;
                    }
                    if (preferredAttempt < 2) Thread.Sleep(80);
                }
            }

            AppendLogSafe("V132: perfil lembrado/preferencial nao qualificou; usando FAST PG V127.");
            try
            {
                SerialPort fast = TryAcquireFastV127(portName, round, out state, out acquisitionLabel);
                if (fast != null)
                {
                    RememberPgProfileV132(portName, fast.DtrEnable, fast.RtsEnable,
                        "19200 8O1 DTR=" + (fast.DtrEnable ? "on" : "off")
                        + " RTS=" + (fast.RtsEnable ? "on" : "off"));
                    acquisitionLabel += " / memorizado V132";
                    return fast;
                }
            }
            catch (Exception ex)
            {
                AppendLogSafe("V132 FAST PG indisponivel: " + ex.Message);
            }

            AppendLogSafe("V132 FALLBACK: qualificacao conservadora V127.");
            SerialPort fallback = AcquireStablePgPortConservativeV127(portName, round,
                out state, out acquisitionLabel);
            if (fallback != null)
            {
                RememberPgProfileV132(portName, fallback.DtrEnable, fallback.RtsEnable,
                    "19200 8O1 DTR=" + (fallback.DtrEnable ? "on" : "off")
                    + " RTS=" + (fallback.RtsEnable ? "on" : "off"));
                acquisitionLabel += " / memorizado V132";
            }
            return fallback;
        }

'@
$shell = Replace-Section $shell $acqStart $acqEnd $acq 'FAST/STICKY PG'

# -----------------------------------------------------------------------------
# 2. API interna do editor: carrega um .pladder reconstruido pela leitura do PLC.
# Preserva a pergunta de salvamento se houver alteracoes locais nao salvas.
# -----------------------------------------------------------------------------
$editorImport = @'
        internal bool LoadTp02ReadProjectV132(string serializedProject, string sourceName, out string error)
        {
            error = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(serializedProject))
                {
                    error = "Projeto reconstruido vazio.";
                    return false;
                }
                if (!ConfirmDiscard())
                {
                    error = "Importacao cancelada para preservar o projeto atual.";
                    return false;
                }

                DeserializeProject(serializedProject);
                if (rungs.Count == 0) rungs.Add(new LadderRung());
                currentFile = string.Empty;
                dirty = true;
                undoStack.Clear();
                canvas.SelectedRung = 0;
                canvas.SelectedColumn = 0;
                canvas.SelectedLane = 0;
                canvas.Invalidate();
                SetActiveTool(LadderTool.Select);
                UpdateProjectLabel();
                statusLabel.Text = "Programa lido do TP02 carregado no editor"
                    + (string.IsNullOrEmpty(sourceName) ? "." : " - " + sourceName + ".");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

'@
$ladder = Insert-Before $ladder '        private bool ConfirmDiscard()' $editorImport 'importacao no LadderEditor'

# -----------------------------------------------------------------------------
# 3. Decodificacao segura do snapshot PG para o formato Ladder do Studio.
# So substitui o editor quando TODAS as instrucoes puderem ser reconstruidas.
# -----------------------------------------------------------------------------
$decodeMethods = @'
        private sealed class V132ReadWord
        {
            public int Step;
            public string Op = string.Empty;
            public string Operand = string.Empty;
            public int Number;
            public string Raw = string.Empty;
        }

        private static string FormatReadAddressV132(string device, int number)
        {
            return device + number.ToString(device == "SC" ? "000" : "0000", CultureInfo.InvariantCulture);
        }

        private static bool TryDecodeReadWordV132(int step, byte high, byte low, byte braw, out V132ReadWord word)
        {
            word = new V132ReadWord();
            word.Step = step;
            word.Raw = high.ToString("X2", CultureInfo.InvariantCulture) + " "
                + low.ToString("X2", CultureInfo.InvariantCulture) + " "
                + braw.ToString("X2", CultureInfo.InvariantCulture);

            if (high == 0x00 && low == 0x00 && braw == 0x00)
            {
                word.Op = "NOP";
                return true;
            }

            string op = string.Empty;
            switch (low & 0x78)
            {
                case 0x10: op = "STR"; break;
                case 0x18: op = "STR NOT"; break;
                case 0x20: op = "AND"; break;
                case 0x28: op = "AND NOT"; break;
                case 0x30: op = "OR"; break;
                case 0x38: op = "OR NOT"; break;
                case 0x40: op = "OUT"; break;
            }
            if (op.Length > 0 && (high & 0x80) == 0)
            {
                int deviceBase = high & 0x60;
                string device = deviceBase == 0x00 ? "X" : (deviceBase == 0x20 ? "Y" : (deviceBase == 0x40 ? "C" : string.Empty));
                if (device.Length > 0)
                {
                    int number = ((high & 0x1F) * 8) + (low & 0x07) + 1;
                    word.Op = op;
                    word.Operand = FormatReadAddressV132(device, number);
                    return true;
                }
            }

            if ((high & 0x80) == 0 && ((low & 0x78) == 0x60 || (low & 0x78) == 0x68))
            {
                int number = ((high & 0x7F) | ((low & 0x07) << 7)) + 1;
                word.Op = (low & 0x78) == 0x60 ? "TMR" : "CNT";
                word.Operand = FormatReadAddressV132("V", number);
                return true;
            }

            if (high == 0x00 && low == 0x70) { word.Op = "END"; return true; }
            if (high == 0x17 && low == 0x71) { word.Op = "SET"; return true; }
            if (high == 0x18 && low == 0x71) { word.Op = "RST"; return true; }
            if (high == 0x0D && low == 0x77) { word.Op = "ADDW"; return true; }
            if (high == 0x00 && low == 0x01) { word.Op = "AND STR"; return true; }
            if (high == 0x00 && low == 0x02) { word.Op = "OR STR"; return true; }

            if (high >= 0x80 && high <= 0x9F && (low & 0x80) == 0)
            {
                int highNibble = (high & 0x1E) >> 1;
                int lowByte = ((high & 0x01) << 7) | (low & 0x7F);
                word.Op = "K";
                word.Number = (highNibble << 8) | lowByte;
                word.Operand = word.Number.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if ((low & 0x80) != 0)
            {
                int deviceBase = high & 0xF8;
                string device = deviceBase == 0xC0 ? "X" : (deviceBase == 0xC8 ? "Y" : (deviceBase == 0xD0 ? "C" : string.Empty));
                if (device.Length > 0)
                {
                    int number = ((low & 0x7F) * 8) + (high & 0x07) + 1;
                    word.Op = "ARG";
                    word.Operand = FormatReadAddressV132(device, number);
                    return true;
                }
            }

            if ((high & 0xF8) == 0xF0 && (low & 0x80) == 0)
            {
                int h = (high & 0x0E) >> 1;
                int l = ((high & 0x01) << 7) | (low & 0x7F);
                word.Op = "ARG";
                word.Operand = FormatReadAddressV132("D", (h << 8) + l + 1);
                return true;
            }

            word.Op = "UNKNOWN";
            return false;
        }

        private static OpenLadderStudio.Core.LadderProjectElement NewReadElementV132(
            OpenLadderStudio.Core.LadderProjectElementKind kind, string address, string parameter)
        {
            OpenLadderStudio.Core.LadderProjectElement e = new OpenLadderStudio.Core.LadderProjectElement();
            e.Kind = kind;
            e.Address = address ?? string.Empty;
            e.Parameter = parameter ?? string.Empty;
            return e;
        }

        private static string ReadWordTextV132(V132ReadWord w)
        {
            if (w == null) return string.Empty;
            if (w.Op == "K") return "K" + w.Number.ToString(CultureInfo.InvariantCulture);
            return w.Op + (string.IsNullOrEmpty(w.Operand) ? string.Empty : " " + w.Operand);
        }

        private bool TryBuildReadProjectV132(ProgramSnapshot snapshot, out string serializedProject,
            out string verifiedIl, out string error)
        {
            serializedProject = string.Empty;
            verifiedIl = string.Empty;
            error = string.Empty;
            if (snapshot == null || snapshot.Count < 1)
            {
                error = "Snapshot PG vazio.";
                return false;
            }

            List<V132ReadWord> words = new List<V132ReadWord>();
            StringBuilder il = new StringBuilder();
            for (int i = 0; i < snapshot.Count; i++)
            {
                V132ReadWord w;
                bool known = TryDecodeReadWordV132(i, snapshot.High[i], snapshot.Low[i], snapshot.External[i], out w);
                words.Add(w);
                il.Append(i.ToString("0000", CultureInfo.InvariantCulture));
                il.Append(": ");
                il.Append(known ? ReadWordTextV132(w) : "UNKNOWN " + w.Raw);
                il.AppendLine();
                if (!known)
                {
                    error = "Passo " + i.ToString("0000", CultureInfo.InvariantCulture)
                        + " ainda nao possui decodificacao segura: " + w.Raw + ".";
                    verifiedIl = il.ToString();
                    return false;
                }
            }
            verifiedIl = il.ToString();

            OpenLadderStudio.Core.LadderProjectDocument document = new OpenLadderStudio.Core.LadderProjectDocument();
            OpenLadderStudio.Core.LadderProjectRung current = null;
            int conditions = 0;

            for (int i = 0; i < words.Count; i++)
            {
                V132ReadWord w = words[i];
                if (w.Op == "NOP") continue;

                if (w.Op == "STR" || w.Op == "STR NOT")
                {
                    if (current != null)
                    {
                        error = "Estrutura booleana com pilha/ramo complexo no passo "
                            + w.Step.ToString("0000", CultureInfo.InvariantCulture)
                            + " ainda nao e reconstruida automaticamente.";
                        return false;
                    }
                    current = new OpenLadderStudio.Core.LadderProjectRung();
                    current.Series[0] = NewReadElementV132(
                        w.Op == "STR NOT" ? OpenLadderStudio.Core.LadderProjectElementKind.ContactNormallyClosed : OpenLadderStudio.Core.LadderProjectElementKind.ContactNormallyOpen,
                        w.Operand, string.Empty);
                    conditions = 1;
                    continue;
                }

                if (w.Op == "AND" || w.Op == "AND NOT")
                {
                    if (current == null || conditions >= 7)
                    {
                        error = "AND sem rung simples reconstruivel no passo " + w.Step.ToString("0000", CultureInfo.InvariantCulture) + ".";
                        return false;
                    }
                    current.Series[conditions] = NewReadElementV132(
                        w.Op == "AND NOT" ? OpenLadderStudio.Core.LadderProjectElementKind.ContactNormallyClosed : OpenLadderStudio.Core.LadderProjectElementKind.ContactNormallyOpen,
                        w.Operand, string.Empty);
                    conditions++;
                    continue;
                }

                if (w.Op == "OR" || w.Op == "OR NOT")
                {
                    if (current == null || conditions != 1 || current.Parallel[0].Kind != OpenLadderStudio.Core.LadderProjectElementKind.Empty)
                    {
                        error = "Ramo OR complexo no passo " + w.Step.ToString("0000", CultureInfo.InvariantCulture)
                            + " foi lido, mas nao sera desenhado com uma topologia presumida.";
                        return false;
                    }
                    current.Parallel[0] = NewReadElementV132(
                        w.Op == "OR NOT" ? OpenLadderStudio.Core.LadderProjectElementKind.ContactNormallyClosed : OpenLadderStudio.Core.LadderProjectElementKind.ContactNormallyOpen,
                        w.Operand, string.Empty);
                    continue;
                }

                if (w.Op == "OUT")
                {
                    if (current == null) { error = "OUT sem STR no passo " + w.Step.ToString("0000", CultureInfo.InvariantCulture) + "."; return false; }
                    current.Series[7] = NewReadElementV132(OpenLadderStudio.Core.LadderProjectElementKind.Coil, w.Operand, string.Empty);
                    document.Rungs.Add(current);
                    current = null;
                    conditions = 0;
                    continue;
                }

                if (w.Op == "TMR" || w.Op == "CNT")
                {
                    if (current == null || i + 1 >= words.Count || words[i + 1].Op != "K")
                    {
                        error = w.Op + " sem preset literal seguro no passo " + w.Step.ToString("0000", CultureInfo.InvariantCulture) + ".";
                        return false;
                    }
                    V132ReadWord preset = words[++i];
                    current.Series[7] = NewReadElementV132(
                        w.Op == "TMR" ? OpenLadderStudio.Core.LadderProjectElementKind.Timer : OpenLadderStudio.Core.LadderProjectElementKind.Counter,
                        w.Operand, preset.Number.ToString(CultureInfo.InvariantCulture));
                    document.Rungs.Add(current);
                    current = null;
                    conditions = 0;
                    continue;
                }

                if (w.Op == "SET" || w.Op == "RST")
                {
                    if (current == null || i + 1 >= words.Count || words[i + 1].Op != "ARG")
                    {
                        error = w.Op + " sem operando seguro no passo " + w.Step.ToString("0000", CultureInfo.InvariantCulture) + ".";
                        return false;
                    }
                    V132ReadWord arg = words[++i];
                    current.Series[7] = NewReadElementV132(
                        w.Op == "SET" ? OpenLadderStudio.Core.LadderProjectElementKind.Set : OpenLadderStudio.Core.LadderProjectElementKind.Reset,
                        arg.Operand, string.Empty);
                    document.Rungs.Add(current);
                    current = null;
                    conditions = 0;
                    continue;
                }

                if (w.Op == "ADDW")
                {
                    if (current == null || i + 3 >= words.Count)
                    {
                        error = "F-13w ADD incompleto no passo " + w.Step.ToString("0000", CultureInfo.InvariantCulture) + ".";
                        return false;
                    }
                    V132ReadWord a = words[++i];
                    V132ReadWord b = words[++i];
                    V132ReadWord c = words[++i];
                    if ((a.Op != "ARG" && a.Op != "K") || (b.Op != "ARG" && b.Op != "K") || (c.Op != "ARG" && c.Op != "K"))
                    {
                        error = "Operandos de F-13w ADD nao puderam ser reconstruidos com seguranca.";
                        return false;
                    }
                    string pa = a.Op == "K" ? a.Number.ToString(CultureInfo.InvariantCulture) : a.Operand;
                    string pb = b.Op == "K" ? b.Number.ToString(CultureInfo.InvariantCulture) : b.Operand;
                    string pc = c.Op == "K" ? c.Number.ToString(CultureInfo.InvariantCulture) : c.Operand;
                    current.Series[7] = NewReadElementV132(OpenLadderStudio.Core.LadderProjectElementKind.Function,
                        "F-13W", pa + " " + pb + " " + pc);
                    document.Rungs.Add(current);
                    current = null;
                    conditions = 0;
                    continue;
                }

                if (w.Op == "END")
                {
                    if (current != null)
                    {
                        error = "END encontrado antes de fechar o rung anterior.";
                        return false;
                    }
                    OpenLadderStudio.Core.LadderProjectRung end = new OpenLadderStudio.Core.LadderProjectRung();
                    end.Series[7] = NewReadElementV132(OpenLadderStudio.Core.LadderProjectElementKind.End, "F-00", string.Empty);
                    document.Rungs.Add(end);
                    continue;
                }

                error = "Instrucao " + w.Op + " no passo " + w.Step.ToString("0000", CultureInfo.InvariantCulture)
                    + " foi lida, mas a topologia ainda nao e importada automaticamente.";
                return false;
            }

            if (current != null)
            {
                error = "O programa terminou com rung sem instrucao de saida.";
                return false;
            }
            if (document.Rungs.Count == 0)
            {
                error = "Nenhum rung reconstruivel foi encontrado.";
                return false;
            }

            serializedProject = OpenLadderStudio.Core.LadderProjectCodec.Serialize(document);
            return true;
        }

'@
$shell = Insert-Before $shell '        private ProgramSnapshot ReadCanonicalSnapshotOnOpenPort(SerialPort port, string tag)' $decodeMethods 'decoder READ para Ladder'

# -----------------------------------------------------------------------------
# 4. READ: depois do readback, reconstrui o Ladder e o carrega no editor principal.
# -----------------------------------------------------------------------------
$readMethod = @'
        private void StartPgReadV125()
        {
            if (busy) return;
            if (portCombo.SelectedItem == null)
            {
                MessageBox.Show(this, "Selecione a porta COM usada pelo TP-232PG.",
                    "TP02 PG", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string portName = portCombo.SelectedItem.ToString();
            CreateSession();
            logBox.Clear();
            AppendLog("PG READ v1.32 iniciado em " + portName + ".");
            AppendLog("SEGURANCA: READ nao transmite PG33, nao restaura e nao altera o programa do PLC.");
            SetBusy(true);
            SetStatus("READ / LENDO...", Warning);

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception failure = null;
                ProgramSnapshot snapshot = null;
                string successText = string.Empty;
                string reconstructedProject = string.Empty;
                string verifiedIl = string.Empty;
                string reconstructionError = string.Empty;
                bool reconstructionOk = false;
                try
                {
                    snapshot = ReadSnapshotV93Robust(portName, "manual-read-v132");
                    if (snapshot == null || snapshot.Count < 1 || snapshot.EndStep < 0)
                        throw new InvalidDataException("READ PG invalido: F-00 END nao foi encontrado.");

                    SaveSnapshot("manual-read-v132", snapshot);
                    AppendLogSafe("READ OK: estado=" + snapshot.PlcState
                        + " palavras=" + snapshot.Count.ToString(CultureInfo.InvariantCulture)
                        + " END=" + snapshot.EndStep.ToString("0000", CultureInfo.InvariantCulture) + ".");

                    reconstructionOk = TryBuildReadProjectV132(snapshot,
                        out reconstructedProject, out verifiedIl, out reconstructionError);
                    try { File.WriteAllText(Path.Combine(sessionDirectory, "tp02-read-v132.verified.il.txt"), verifiedIl, Encoding.UTF8); } catch { }
                    if (reconstructionOk)
                    {
                        try { File.WriteAllText(Path.Combine(sessionDirectory, "tp02-read-v132.pladder"), reconstructedProject, Encoding.UTF8); } catch { }
                        AppendLogSafe("V132 LADDER: programa lido reconstruido com seguranca para o editor principal.");
                    }
                    else
                    {
                        AppendLogSafe("V132 LADDER: importacao automatica bloqueada: " + reconstructionError);
                    }

                    successText = "READ PG concluido.\r\n\r\n"
                        + "Estado: " + snapshot.PlcState
                        + "\r\nPalavras: " + snapshot.Count.ToString(CultureInfo.InvariantCulture)
                        + "\r\nEND: " + snapshot.EndStep.ToString("0000", CultureInfo.InvariantCulture)
                        + (reconstructionOk
                            ? "\r\n\r\nPrograma reconstruido e pronto para abrir no editor Ladder."
                            : "\r\n\r\nLeitura concluida, mas o desenho automatico foi bloqueado: " + reconstructionError)
                        + "\r\n\r\nNenhuma escrita foi realizada no PLC.";
                }
                catch (Exception ex)
                {
                    failure = ex;
                    try { File.WriteAllText(Path.Combine(sessionDirectory, "pg-read-v132-failure.txt"), ex.ToString(), Encoding.UTF8); } catch { }
                }

                if (IsDisposed) return;
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (failure == null)
                    {
                        bool loaded = false;
                        string loadError = string.Empty;
                        if (reconstructionOk && ladderForm != null && !ladderForm.IsDisposed)
                        {
                            loaded = ladderForm.LoadTp02ReadProjectV132(reconstructedProject,
                                "TP02 " + portName, out loadError);
                        }

                        AppendLog(loaded
                            ? "PASS: READ concluido e programa exibido no editor Ladder."
                            : "PASS: READ concluido; PLC nao foi alterado.");
                        SetStatus(loaded ? "READ OK / LADDER CARREGADO" : "READ OK / SOMENTE LEITURA", Success);

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
                        else if (reconstructionOk && !string.IsNullOrEmpty(loadError))
                        {
                            successText += "\r\n\r\nO editor atual foi preservado: " + loadError;
                        }

                        MessageBox.Show(loaded && Owner != null ? Owner : this, successText,
                            "TP02 - READ PG", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        AppendLog("READ FALHOU: " + failure.Message);
                        SetStatus("READ PENDENTE / SEM ESCRITA", Warning);
                        MessageBox.Show(this,
                            failure.Message + "\r\n\r\nNenhuma escrita foi realizada.",
                            "TP02 - READ PG nao concluido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    SetBusy(false);
                }));
            });
        }

'@
$shell = Replace-Section $shell '        private void StartPgReadV125()' '        private void ShowPgRunStopPendingV125(string action)' $readMethod 'READ exibido no Ladder'

$shell = $shell.Replace('    |    v1.31";', '    |    v1.32";')

[System.IO.File]::WriteAllText($shellPath, $shell, (New-Object System.Text.UTF8Encoding($false)))
[System.IO.File]::WriteAllText($ladderPath, $ladder, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'TP02 V132 aplicado: perfil PG lembrado + READ reconstruido no editor Ladder.' -ForegroundColor Cyan
