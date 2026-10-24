using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ModernPC12
{
    /// <summary>
    /// Telas do driver WEG TP02. Conectar abre o monitor, porque a conexao serial
    /// e feita dentro dele; ler e transferir usam as telas de programa do TP02.
    /// </summary>
    internal sealed class Tp02DriverUi : IPlcDriverUi
    {
        private static readonly string[] ids = new string[] { "weg.tp02.serial" };

        private TP02ProgramReaderForm readerForm;
        private TP02ControlV31Form monitorForm;
        private bool closeHooked;
        private readonly List<PlcFeatureAction> features = new List<PlcFeatureAction>();

        public Tp02DriverUi()
        {
            features.Add(new PlcFeatureAction("Monitor MMI (somente leitura)", delegate(IStudioHost host) { OpenMonitor(host); }));
        }

        public IList<string> DriverIds { get { return ids; } }

        public IList<PlcFeatureAction> ModelFeatures { get { return features; } }

        public bool Supports(PlcDriverFeature feature)
        {
            return true;
        }

        public void Open(PlcDriverFeature feature, IStudioHost host)
        {
            switch (feature)
            {
                case PlcDriverFeature.Connect:
                case PlcDriverFeature.Monitor:
                    OpenMonitor(host);
                    break;
                case PlcDriverFeature.ReadProgram:
                    OpenReader(host);
                    break;
                case PlcDriverFeature.TransferProgram:
                    OpenTransfer(host);
                    break;
            }
        }

        private void OpenMonitor(IStudioHost host)
        {
            if (monitorForm == null || monitorForm.IsDisposed)
            {
                monitorForm = new TP02ControlV31Form();
                monitorForm.SetReadOnlyMode(true);
                // Acender diagrama: o monitor le os bits; a energizacao e calculada aqui,
                // com a mesma regra da simulacao, sobre o programa atual do editor.
                monitorForm.DiagramAddressProvider = delegate
                {
                    UniversalLadderProgram program = EditorProgram(host);
                    return program == null ? null : LadderPlcPowerEvaluator.ReadableAddresses(program);
                };
                monitorForm.DiagramBitsRead += delegate(IDictionary<string, bool> bits)
                {
                    host.ShowRungPower(bits == null ? null : LadderPlcPowerEvaluator.Evaluate(EditorProgram(host), bits));
                };
                if (!closeHooked)
                {
                    closeHooked = true;
                    host.DocumentClosed += delegate(Form document)
                    {
                        if (document != null && document == monitorForm) monitorForm.StopDiagram();
                    };
                }
            }
            host.OpenDocument(monitorForm, "Monitor do PLC (somente leitura)", "MON");
            host.SetStatus("Monitor do PLC em modo somente leitura");
        }

        private static UniversalLadderProgram EditorProgram(IStudioHost host)
        {
            LadderEditorForm editor = host.Editor;
            if (editor == null || editor.IsDisposed) return null;
            return UniversalLadderAdapter.FromEditor(editor).Program;
        }

        private void OpenReader(IStudioHost host)
        {
            if (readerForm == null || readerForm.IsDisposed) readerForm = new TP02ProgramReaderForm();
            host.OpenDocument(readerForm, "Leitura do programa do PLC", "READ");
            host.SetStatus("Leitura do programa do PLC");
        }

        private void OpenTransfer(IStudioHost host)
        {
            LadderEditorForm editor = host.Editor;
            if (editor == null || editor.IsDisposed)
            {
                MessageBox.Show(host.Window, "O editor Ladder não está disponível.", "OpenLadder Studio",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (TP02ProgramTransferForm dialog = new TP02ProgramTransferForm(host.Profile, editor))
            {
                dialog.ShowDialog(host.Window);
            }
            host.SetStatus("Transferência encerrada");
        }
    }
}
