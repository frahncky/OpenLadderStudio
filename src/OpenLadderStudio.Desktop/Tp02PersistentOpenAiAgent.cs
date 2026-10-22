using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace ModernPC12
{
    // Sessao persistente para pesquisa continua do TP02.
    // Encadeia as decisoes pela Responses API usando previous_response_id.
    internal sealed class Tp02PersistentOpenAiAgent
    {
        private const string ApiUrl = "https://api.openai.com/v1/responses";
        private readonly string apiKey;
        private readonly string model;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private string previousResponseId = string.Empty;
        private int turnCount;

        public Tp02PersistentOpenAiAgent(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Chave da OpenAI ausente.");
            apiKey = key.Trim();
            string configured = Environment.GetEnvironmentVariable("OPENAI_MODEL");
            model = string.IsNullOrWhiteSpace(configured) ? "gpt-5.6" : configured.Trim();
            serializer.MaxJsonLength = int.MaxValue;
        }

        public string Model { get { return model; } }
        public string PreviousResponseId { get { return previousResponseId; } }
        public int TurnCount { get { return turnCount; } }

        public void ResetConversation()
        {
            previousResponseId = string.Empty;
            turnCount = 0;
        }

        public Tp02AgentAction NextAction(Tp02AgentObservation observation)
        {
            if (observation == null) throw new ArgumentNullException("observation");

            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["model"] = model;
            payload["instructions"] = BuildInstructions();
            payload["input"] = serializer.Serialize(observation);
            payload["max_output_tokens"] = 700;
            payload["truncation"] = "auto";
            payload["store"] = true;
            if (!string.IsNullOrWhiteSpace(previousResponseId))
                payload["previous_response_id"] = previousResponseId;

            // Structured Output: a resposta deve obedecer exatamente ao contrato local.
            Dictionary<string, object> schema = new Dictionary<string, object>();
            schema["type"] = "object";
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties["action"] = new Dictionary<string, object> {
                { "type", "string" },
                { "enum", new string[] { "open_profile", "set_lines", "hello", "probe", "passive", "wait", "finish" } }
            };
            properties["profile"] = new Dictionary<string, object> { { "type", "string" } };
            properties["probe_id"] = new Dictionary<string, object> { { "type", "string" } };
            properties["dtr"] = new Dictionary<string, object> { { "type", new string[] { "boolean", "null" } } };
            properties["rts"] = new Dictionary<string, object> { { "type", new string[] { "boolean", "null" } } };
            properties["wait_ms"] = new Dictionary<string, object> { { "type", "integer" } };
            properties["reason"] = new Dictionary<string, object> { { "type", "string" } };
            schema["properties"] = properties;
            schema["required"] = new string[] { "action", "profile", "probe_id", "dtr", "rts", "wait_ms", "reason" };
            schema["additionalProperties"] = false;

            Dictionary<string, object> format = new Dictionary<string, object>();
            format["type"] = "json_schema";
            format["name"] = "tp02_next_action";
            format["strict"] = true;
            format["schema"] = schema;
            payload["text"] = new Dictionary<string, object> { { "format", format } };

            string responseJson = PostJson(ApiUrl, serializer.Serialize(payload));
            string responseId = ExtractResponseId(responseJson);
            if (!string.IsNullOrWhiteSpace(responseId)) previousResponseId = responseId;
            turnCount++;

            string text = ExtractOutputText(responseJson);
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("A OpenAI respondeu sem acao estruturada.");
            Tp02AgentAction action = serializer.Deserialize<Tp02AgentAction>(text.Trim());
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
            req.UserAgent = "OpenLadder-Studio-TP02-Continuous-Agent/1.0";
            req.Headers[HttpRequestHeader.Authorization] = "Bearer " + apiKey;
            req.Timeout = 90000;
            req.ReadWriteTimeout = 90000;
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

        private string ExtractResponseId(string responseJson)
        {
            object rootObject = serializer.DeserializeObject(responseJson);
            Dictionary<string, object> root = rootObject as Dictionary<string, object>;
            if (root == null) return string.Empty;
            object id;
            return root.TryGetValue("id", out id) && id != null ? Convert.ToString(id, CultureInfo.InvariantCulture) : string.Empty;
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
                    object typeObject, textObject;
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

        private static string BuildInstructions()
        {
            return
                "Voce conduz uma pesquisa CONTINUA e autorizada do protocolo PG do WEG TP02-60MR. " +
                "A porta serial deve permanecer aberta sempre que houver enlace util. Escolha UMA proxima acao segura por turno. " +
                "Nunca solicite bytes arbitrarios, escrita, WBP/download, RUN, STOP, apagamento, firmware ou alteracao de memoria. " +
                "Use somente action=open_profile|set_lines|hello|probe|passive|wait|finish, perfis de allowedProfiles e probes de allowedProbeIds. " +
                "Seu objetivo nao e encerrar cedo: continue ate fechar de forma reproduzivel handshake, F0/preflight, leitura de programa e leitura de sistema usando apenas probes READ-ONLY. " +
                "Um HELLO que reaparece durante um probe e evidencia de ressincronizacao; preserve a COM e investigue timing/RTS antes de reabrir. " +
                "Fatos fisicos: 19200 8O1; HELLO STOP=80 01 09 75; F0 00 0F ja respondeu 00 02 10 22 CB; RTS TX on -> RX off ja provocou novo HELLO STOP. " +
                "Mude uma variavel por vez, compare repetibilidade, use captura passiva e evite repetir a mesma experiencia sem mudar hipotese. " +
                "Somente use finish quando o estado recebido indicar PROTOCOL_CLOSURE_READY=true. Se ainda nao estiver pronto, continue investigando caminhos seguros.";
        }
    }
}
