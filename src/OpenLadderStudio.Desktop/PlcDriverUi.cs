using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ModernPC12
{
    /// <summary>
    /// Fluxo generico que o shell oferece para qualquer controlador. O shell nao
    /// conhece fabricante nem modelo: ele pergunta ao IPlcDriverUi do perfil ativo
    /// se o recurso existe e, se existir, pede que o driver abra a propria tela.
    /// </summary>
    internal enum PlcDriverFeature
    {
        /// <summary>Estabelecer a comunicacao com o controlador.</summary>
        Connect,
        /// <summary>Ler o programa gravado no controlador.</summary>
        ReadProgram,
        /// <summary>Enviar o programa do editor ao controlador.</summary>
        TransferProgram,
        /// <summary>Acompanhar entradas, saidas e registradores.</summary>
        Monitor
    }

    /// <summary>O que o shell expoe a uma tela de driver.</summary>
    internal interface IStudioHost
    {
        IWin32Window Window { get; }
        PlcDeviceProfile Profile { get; }
        /// <summary>Editor Ladder aberto (criado sob demanda).</summary>
        LadderEditorForm Editor { get; }
        /// <summary>Abre ou reativa uma aba de documento. A chave identifica a aba e escolhe o icone.</summary>
        void OpenDocument(Form document, string title, string key);
        void SetStatus(string text);
    }

    /// <summary>Item de "Ferramentas &gt; Recursos do modelo".</summary>
    internal sealed class PlcFeatureAction
    {
        public readonly string Text;
        public readonly Action<IStudioHost> Run;

        public PlcFeatureAction(string text, Action<IStudioHost> run)
        {
            Text = text;
            Run = run;
        }
    }

    /// <summary>
    /// Telas de um driver. Cada familia de controlador implementa esta interface;
    /// o shell escolhe a implementacao pelo DriverId do perfil ativo. Supports diz
    /// se a tela existe; a capacidade declarada em PlcDriverCapabilities continua
    /// sendo conferida pelo shell antes de chamar Open.
    /// </summary>
    internal interface IPlcDriverUi
    {
        /// <summary>Identificadores de driver (PlcDeviceProfile.DriverId) atendidos por esta implementacao.</summary>
        IList<string> DriverIds { get; }
        bool Supports(PlcDriverFeature feature);
        void Open(PlcDriverFeature feature, IStudioHost host);
        /// <summary>Recursos que so existem neste modelo; aparecem em "Recursos do modelo".</summary>
        IList<PlcFeatureAction> ModelFeatures { get; }
    }

    internal static class PlcDriverUiRegistry
    {
        private static readonly List<IPlcDriverUi> items = new List<IPlcDriverUi>();

        static PlcDriverUiRegistry()
        {
            // Um novo driver entra aqui, com a sua implementacao de IPlcDriverUi.
            Register(new Tp02DriverUi());
            Register(new ModbusDriverUi());
        }

        public static void Register(IPlcDriverUi ui)
        {
            if (ui != null) items.Add(ui);
        }

        public static IPlcDriverUi Find(PlcDeviceProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.DriverId)) return null;
            foreach (IPlcDriverUi ui in items)
                foreach (string id in ui.DriverIds)
                    if (string.Equals(id, profile.DriverId, StringComparison.OrdinalIgnoreCase)) return ui;
            return null;
        }
    }

    /// <summary>Ponte entre o shell e IStudioHost, para que o shell nao precise implementar a interface.</summary>
    internal sealed class StudioHostAdapter : IStudioHost
    {
        private readonly IWin32Window window;
        private readonly Func<PlcDeviceProfile> profile;
        private readonly Func<LadderEditorForm> editor;
        private readonly Action<Form, string, string> openDocument;
        private readonly Action<string> setStatus;

        public StudioHostAdapter(IWin32Window window, Func<PlcDeviceProfile> profile, Func<LadderEditorForm> editor,
            Action<Form, string, string> openDocument, Action<string> setStatus)
        {
            this.window = window;
            this.profile = profile;
            this.editor = editor;
            this.openDocument = openDocument;
            this.setStatus = setStatus;
        }

        public IWin32Window Window { get { return window; } }
        public PlcDeviceProfile Profile { get { return profile(); } }
        public LadderEditorForm Editor { get { return editor(); } }
        public void OpenDocument(Form document, string title, string key) { openDocument(document, title, key); }
        public void SetStatus(string text) { setStatus(text); }
    }
}
