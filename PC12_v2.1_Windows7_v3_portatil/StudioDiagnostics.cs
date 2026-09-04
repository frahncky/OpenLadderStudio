using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ModernPC12
{
    /// <summary>
    /// Rede de segurança de execução: captura exceções não tratadas, registra em
    /// arquivo e avisa o usuário em vez de encerrar o processo em silêncio.
    /// Deve ser instalada no início de Main, antes de criar qualquer janela.
    /// </summary>
    internal static class StudioDiagnostics
    {
        private const string LogHeader = "# OpenLadder Studio - registro de falhas";

        private static bool installed;
        private static readonly object gate = new object();

        public static void Install()
        {
            if (installed) return;
            installed = true;

            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            }
            catch
            {
                // Já existe janela criada nesta thread: segue com os eventos abaixo.
            }

            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;
        }

        public static string LogDirectory
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "OpenLadder Studio", "logs");
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Report(e.Exception, false);
        }

        private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            Report(e.ExceptionObject as Exception, true);
        }

        private static void Report(Exception error, bool fatal)
        {
            string path = Write(error, fatal);
            Show(error, fatal, path);
        }

        private static string Write(Exception error, bool fatal)
        {
            try
            {
                string directory = LogDirectory;
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                string path = Path.Combine(directory, "falhas-" + DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");

                StringBuilder text = new StringBuilder();
                if (!File.Exists(path))
                {
                    text.AppendLine(LogHeader);
                    text.AppendLine();
                }

                text.AppendLine("-----------------------------------------------");
                text.AppendLine("Data:      " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                text.AppendLine("Aplicativo: " + SafeApplicationName());
                text.AppendLine("Versão:    " + SafeVersion());
                text.AppendLine("Sistema:   " + Environment.OSVersion + " / CLR " + Environment.Version);
                text.AppendLine("Gravidade: " + (fatal ? "fatal (processo encerrado)" : "recuperável"));
                text.AppendLine();
                text.AppendLine(Describe(error));
                text.AppendLine();

                lock (gate)
                {
                    File.AppendAllText(path, text.ToString(), Encoding.UTF8);
                }
                return path;
            }
            catch
            {
                // Registrar a falha não pode virar uma segunda falha.
                return null;
            }
        }

        private static string Describe(Exception error)
        {
            if (error == null) return "Exceção não informada pelo runtime.";

            StringBuilder text = new StringBuilder();
            Exception current = error;
            int level = 0;
            while (current != null && level < 8)
            {
                if (level > 0) text.AppendLine("--- causada por ---");
                text.AppendLine(current.GetType().FullName + ": " + current.Message);
                if (!string.IsNullOrEmpty(current.StackTrace)) text.AppendLine(current.StackTrace);
                current = current.InnerException;
                level++;
            }
            return text.ToString();
        }

        private static void Show(Exception error, bool fatal, string path)
        {
            try
            {
                StringBuilder message = new StringBuilder();
                message.AppendLine(fatal
                    ? "O OpenLadder Studio encontrou um erro e precisa ser fechado."
                    : "O OpenLadder Studio encontrou um erro inesperado.");
                message.AppendLine();
                message.AppendLine(error == null ? "Erro não identificado." : error.Message);

                if (!string.IsNullOrEmpty(path))
                {
                    message.AppendLine();
                    message.AppendLine("Detalhes registrados em:");
                    message.AppendLine(path);
                }

                if (!fatal)
                {
                    message.AppendLine();
                    message.AppendLine("Operações de leitura em andamento podem ter sido interrompidas. "
                        + "Confira a conexão com o controlador antes de continuar.");
                }

                MessageBox.Show(message.ToString(), "OpenLadder Studio",
                    MessageBoxButtons.OK, fatal ? MessageBoxIcon.Error : MessageBoxIcon.Warning);
            }
            catch
            {
                // Sem interface disponível: o registro em arquivo já foi feito.
            }
        }

        private static string SafeApplicationName()
        {
            try { return Path.GetFileName(Application.ExecutablePath); }
            catch { return "desconhecido"; }
        }

        private static string SafeVersion()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.txt");
                if (File.Exists(path)) return File.ReadAllText(path).Trim();
            }
            catch
            {
            }
            return "não informada";
        }
    }
}
