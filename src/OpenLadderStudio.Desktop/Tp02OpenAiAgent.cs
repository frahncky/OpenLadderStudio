using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ModernPC12
{
    internal sealed class Tp02AgentObservation
    {
        public int iteration { get; set; }
        public string port { get; set; }
        public bool portOpen { get; set; }
        public string profile { get; set; }
        public bool dtr { get; set; }
        public bool rts { get; set; }
        public bool linkEstablished { get; set; }
        public bool f0Validated { get; set; }
        public string lastTx { get; set; }
        public string lastRx { get; set; }
        public List<string> recentHistory { get; set; }
        public List<string> allowedProfiles { get; set; }
        public List<string> allowedProbeIds { get; set; }
    }

    internal sealed class Tp02AgentAction
    {
        public string action { get; set; }
        public string profile { get; set; }
        public string probe_id { get; set; }
        public bool? dtr { get; set; }
        public bool? rts { get; set; }
        public int wait_ms { get; set; }
        public string reason { get; set; }
    }

    internal sealed class Tp02OpenAiAgent
    {
        private const string ApiUrl = "https://api.openai.com/v1/responses";
        private readonly string apiKey;
        private readonly string model;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        public Tp02OpenAiAgent(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Chave da OpenAI ausente.");
            apiKey = key.Trim();
            string configured = Environment.GetEnvironmentVariable("OPENAI_MODEL");
            model = string.IsNullOrWhiteSpace(configured) ? "gpt-5.6" : configured.Trim();
            serializer.MaxJsonLength = int.MaxValue;
        }

        public string Model { get { return model; } }

        public Tp02AgentAction NextAction(Tp02AgentObservation observation)
        {
            if (observation == null) throw new ArgumentNullException("observation");

            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["model"] = model;
            payload["instructions"] = BuildInstructions();
            payload["input"] = serializer.Serialize(observation);
            payload["max_output_tokens"] = 700;

            string requestJson = serializer.Serialize(payload);
            string responseJson = PostJson(ApiUrl, requestJson);
            string text = ExtractOutputText(responseJson);
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("A OpenAI respondeu sem texto de acao.");

            text = StripCodeFence(text.Trim());
            Tp02AgentAction action;
            try
            {
                action = serializer.Deserialize<Tp02AgentAction>(text);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Resposta da IA nao e JSON de acao valido: " + text, ex);
            }
            if (action == null || string.IsNullOrWhiteSpace(action.action))
                throw new InvalidDataException("Resposta da IA nao informou action.");
            return action;
        }

        private string PostJson(string url, string json)
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
            byte[] body = Encoding.UTF8.GetBytes(json);
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Accept = "application/json";
            req.UserAgent = "OpenLadder-Studio-TP02-Agent/1.0";
            req.Headers[HttpRequestHeader.Authorization] = "Bearer " + apiKey;
            req.Timeout = 60000;
            req.ReadWriteTimeout = 60000;
            req.ContentLength = body.Length;
            using (Stream s = req.GetRequestStream()) s.Write(body, 0, body.Length);

            try
            {
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return reader.ReadToEnd();
            }
            catch (WebException ex)
            {
                string details = string.Empty;
                try
                {
                    if (ex.Response != null)
                        using (StreamReader reader = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8))
                            details = reader.ReadToEnd();
                }
                catch { }
                throw new InvalidOperationException("Falha na OpenAI API: " + ex.Message + (string.IsNullOrEmpty(details) ? string.Empty : " | " + details), ex);
            }
        }

        private string ExtractOutputText(string responseJson)
        {
            object rootObject = serializer.DeserializeObject(responseJson);
            Dictionary<string, object> root = rootObject as Dictionary<string, object>;
            if (root == null) return string.Empty;

            object direct;
            if (root.TryGetValue("output_text", out direct) && direct != null)
                return Convert.ToString(direct, CultureInfo.InvariantCulture);

            object outputObject;
            if (!root.TryGetValue("output", out outputObject) || outputObject == null) return string.Empty;
            object[] output = outputObject as object[];
            if (output == null) return string.Empty;

            StringBuilder text = new StringBuilder();
            foreach (object itemObject in output)
            {
                Dictionary<string, object> item = itemObject as Dictionary<string, object>;
                if (item == null) continue;
                object contentObject;
                if (!item.TryGetValue("content", out contentObject) || contentObject == null) continue;
                object[] content = contentObject as object[];
                if (content == null) continue;
                foreach (object partObject in content)
                {
                    Dictionary<string, object> part = partObject as Dictionary<string, object>;
                    if (part == null) continue;
                    object typeObject;
                    object textObject;
                    string type = part.TryGetValue("type", out typeObject) ? Convert.ToString(typeObject, CultureInfo.InvariantCulture) : string.Empty;
                    if (type == "output_text" && part.TryGetValue("text", out textObject) && textObject != null)
                    {
                        if (text.Length > 0) text.AppendLine();
                        text.Append(Convert.ToString(textObject, CultureInfo.InvariantCulture));
                    }
                }
            }
            return text.ToString();
        }

        private static string StripCodeFence(string text)
        {
            if (!text.StartsWith("```", StringComparison.Ordinal)) return text;
            int firstNewLine = text.IndexOf('\n');
            int lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && lastFence > firstNewLine)
                return text.Substring(firstNewLine + 1, lastFence - firstNewLine - 1).Trim();
            return text;
        }

        private static string BuildInstructions()
        {
            return
                "Voce e o agente de engenharia reversa autorizado do WEG TP02-60MR conectado ao OpenLadder Studio. " +
                "Seu objetivo e escolher UMA proxima acao de diagnostico por vez, usando somente o estado recebido. " +
                "Responda EXCLUSIVAMENTE com um objeto JSON, sem markdown, no formato: " +
                "{\"action\":\"open_profile|set_lines|hello|probe|passive|wait|finish\",\"profile\":\"\",\"probe_id\":\"\",\"dtr\":true,\"rts\":false,\"wait_ms\":500,\"reason\":\"\"}. " +
                "REGRAS ABSOLUTAS: nunca solicite bytes arbitrarios; nunca solicite escrita, WBP/download, RUN, STOP, apagamento, firmware ou alteracao de memoria. " +
                "Para probe use somente um probe_id presente em allowedProbeIds. Para open_profile use somente um nome presente em allowedProfiles. " +
                "set_lines apenas altera DTR/RTS e pode pedir uma janela passiva em wait_ms. passive apenas escuta; wait apenas espera/escuta. " +
                "hello envia somente o CON-ICB protegido. O executor local rejeitara qualquer acao fora dessas regras. " +
                "Fatos confirmados: serial 19200 8O1; HELLO STOP conhecido=80 01 09 75; F0 00 0F ja respondeu fisicamente 00 02 10 22 CB em uma sessao; " +
                "na captura mais recente a transicao RTS TX=on para RX=off fez reaparecer HELLO STOP, sugerindo que RTS participa da direcao/re-sincronizacao. " +
                "Prefira experimentos discriminatorios: altere uma variavel por vez, use captura passiva para separar efeito de linha de efeito do TX, e aproveite qualquer HELLO espontaneo como evidencia de re-sincronizacao. " +
                "Se houver link, nao reabra a porta sem motivo. Se uma hipotese falhar, mude timing ou RTS antes de repetir a mesma coisa. " +
                "Use finish quando a evidencia atual for suficiente ou apos esgotar caminhos seguros.";
        }

        public static bool HasStoredKey()
        {
            return !string.IsNullOrWhiteSpace(LoadApiKey());
        }

        public static string LoadApiKey()
        {
            string env = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
            try
            {
                string path = GetSecretPath();
                if (!File.Exists(path)) return string.Empty;
                byte[] encrypted = File.ReadAllBytes(path);
                byte[] plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch { return string.Empty; }
        }

        public static void SaveApiKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Chave vazia.");
            string path = GetSecretPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            byte[] plain = Encoding.UTF8.GetBytes(key.Trim());
            byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, encrypted);
        }

        public static bool PromptAndSaveApiKey(IWin32Window owner)
        {
            using (Form form = new Form())
            {
                form.Text = "Configurar OpenAI API - Agente TP02";
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ClientSize = new Size(570, 185);
                form.Font = new Font("Segoe UI", 9.0f);

                Label info = new Label();
                info.Text = "Cole sua OPENAI_API_KEY. Ela sera criptografada pelo Windows (DPAPI) e salva somente para este usuario.\r\nA chave nunca entra nos relatorios do Laboratorio PG nem no GitHub.";
                info.Location = new Point(18, 15);
                info.Size = new Size(530, 48);
                form.Controls.Add(info);

                TextBox box = new TextBox();
                box.Location = new Point(18, 72);
                box.Size = new Size(530, 25);
                box.UseSystemPasswordChar = true;
                box.Text = LoadApiKey();
                form.Controls.Add(box);

                Label billing = new Label();
                billing.Text = "A OpenAI API possui faturamento separado do ChatGPT.";
                billing.Location = new Point(18, 105);
                billing.Size = new Size(360, 22);
                form.Controls.Add(billing);

                Button ok = new Button();
                ok.Text = "SALVAR";
                ok.DialogResult = DialogResult.OK;
                ok.Location = new Point(365, 137);
                ok.Size = new Size(85, 30);
                form.Controls.Add(ok);

                Button cancel = new Button();
                cancel.Text = "CANCELAR";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Location = new Point(463, 137);
                cancel.Size = new Size(85, 30);
                form.Controls.Add(cancel);

                form.AcceptButton = ok;
                form.CancelButton = cancel;
                if (form.ShowDialog(owner) != DialogResult.OK) return false;
                SaveApiKey(box.Text);
                return true;
            }
        }

        private static string GetSecretPath()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "OpenLadderStudio", "Secrets", "openai-api-key.bin");
        }
    }
}
