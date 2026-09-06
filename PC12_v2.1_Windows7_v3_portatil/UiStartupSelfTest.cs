using System;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class UiStartupSelfTest
    {
        [STAThread]
        private static int Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (UniversalStudioForm form = new UniversalStudioForm())
                {
                    if (form.IsDisposed) throw new InvalidOperationException("A janela principal foi descartada durante a inicialização.");
                    IntPtr handle = form.Handle;
                    if (handle == IntPtr.Zero) throw new InvalidOperationException("A janela principal não criou um handle válido.");
                }
                Console.WriteLine("UI startup smoke test: OK");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("UI startup smoke test: FALHA");
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }
    }
}
