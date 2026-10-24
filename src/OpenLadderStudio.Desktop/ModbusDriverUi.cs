using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;

namespace ModernPC12
{
    /// <summary>
    /// Telas dos drivers Modbus genericos (RTU e TCP): a mesma tela de monitor
    /// serve para conectar e para monitorar, com o transporte escolhido pelo perfil.
    /// </summary>
    internal sealed class ModbusDriverUi : IPlcDriverUi
    {
        private static readonly string[] ids = new string[] { "generic.modbus.rtu", "generic.modbus.tcp" };
        private static readonly PlcFeatureAction[] noFeatures = new PlcFeatureAction[0];

        private ModbusMonitorForm monitorForm;

        public IList<string> DriverIds { get { return ids; } }

        public IList<PlcFeatureAction> ModelFeatures { get { return noFeatures; } }

        public bool Supports(PlcDriverFeature feature)
        {
            return feature == PlcDriverFeature.Connect || feature == PlcDriverFeature.Monitor;
        }

        public void Open(PlcDriverFeature feature, IStudioHost host)
        {
            string key = feature == PlcDriverFeature.Connect ? "PLC" : "MON";
            if (monitorForm == null || monitorForm.IsDisposed) monitorForm = new ModbusMonitorForm();
            SelectTransport(monitorForm, host.Profile);
            host.OpenDocument(monitorForm, "Monitor Modbus RTU/TCP", key);
            host.SetStatus(host.Profile == null ? "Monitor Modbus" : "Monitor " + host.Profile.Protocol);
        }

        private static void SelectTransport(ModbusMonitorForm form, PlcDeviceProfile profile)
        {
            if (form == null || profile == null) return;
            try
            {
                FieldInfo field = typeof(ModbusMonitorForm).GetField("transportCombo", BindingFlags.Instance | BindingFlags.NonPublic);
                ComboBox combo = field == null ? null : field.GetValue(form) as ComboBox;
                if (combo == null) return;
                if (string.Equals(profile.DriverId, "generic.modbus.tcp", StringComparison.OrdinalIgnoreCase)) combo.SelectedIndex = 1;
                else if (string.Equals(profile.DriverId, "generic.modbus.rtu", StringComparison.OrdinalIgnoreCase)) combo.SelectedIndex = 0;
            }
            catch { }
        }
    }
}
