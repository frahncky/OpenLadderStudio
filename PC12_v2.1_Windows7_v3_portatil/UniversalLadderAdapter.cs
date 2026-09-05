using System;
using System.Collections.Generic;
using System.Reflection;

namespace ModernPC12
{
    internal sealed class UniversalLadderConversionReport
    {
        public UniversalLadderProgram Program;
        public int RungCount;
        public int ElementCount;
        public int ParallelElementCount;
        public int UnsupportedCount;
        public readonly List<string> Warnings = new List<string>();

        public string Summary()
        {
            string text = RungCount.ToString() + " rung(s), " + ElementCount.ToString() + " elemento(s)";
            if (ParallelElementCount > 0) text += ", " + ParallelElementCount.ToString() + " em ramificações";
            if (UnsupportedCount > 0) text += ", " + UnsupportedCount.ToString() + " não convertido(s)";
            return text;
        }
    }

    internal static class UniversalLadderAdapter
    {
        public static UniversalLadderConversionReport FromEditor(LadderEditorForm editor)
        {
            UniversalLadderConversionReport report = new UniversalLadderConversionReport();
            report.Program = new UniversalLadderProgram();
            if (editor == null)
            {
                report.Warnings.Add("Editor Ladder não disponível.");
                return report;
            }

            try
            {
                FieldInfo rungsField = typeof(LadderEditorForm).GetField("rungs", BindingFlags.Instance | BindingFlags.NonPublic);
                List<LadderRung> source = rungsField == null ? null : rungsField.GetValue(editor) as List<LadderRung>;
                if (source == null)
                {
                    report.Warnings.Add("Não foi possível acessar os rungs do editor atual.");
                    return report;
                }

                FieldInfo projectField = typeof(LadderEditorForm).GetField("projectLabel", BindingFlags.Instance | BindingFlags.NonPublic);
                System.Windows.Forms.Label projectLabel = projectField == null ? null : projectField.GetValue(editor) as System.Windows.Forms.Label;
                if (projectLabel != null && !string.IsNullOrEmpty(projectLabel.Text)) report.Program.Name = projectLabel.Text.Trim();

                for (int r = 0; r < source.Count; r++)
                {
                    LadderRung sourceRung = source[r];
                    UniversalLadderRung targetRung = new UniversalLadderRung();

                    for (int c = 0; c < LadderRung.ColumnCount; c++)
                    {
                        UniversalLadderElement series = ConvertElement(sourceRung.Elements[c], report, r, c, false);
                        if (series != null)
                        {
                            targetRung.Series.Add(series);
                            report.ElementCount++;
                        }

                        UniversalLadderElement parallel = ConvertElement(sourceRung.Parallel[c], report, r, c, true);
                        if (parallel != null)
                        {
                            targetRung.Parallel.Add(parallel);
                            report.ElementCount++;
                            report.ParallelElementCount++;
                        }
                    }

                    report.Program.Rungs.Add(targetRung);
                }

                report.RungCount = report.Program.Rungs.Count;
            }
            catch (Exception ex)
            {
                report.Warnings.Add("Falha ao gerar modelo universal: " + ex.Message);
            }

            return report;
        }

        private static UniversalLadderElement ConvertElement(LadderElement source, UniversalLadderConversionReport report, int rung, int column, bool parallel)
        {
            if (source == null || source.Type == LadderElementType.Empty) return null;

            UniversalElementKind kind;
            switch (source.Type)
            {
                case LadderElementType.ContactNO: kind = UniversalElementKind.ContactNO; break;
                case LadderElementType.ContactNC: kind = UniversalElementKind.ContactNC; break;
                case LadderElementType.Coil: kind = UniversalElementKind.Coil; break;
                case LadderElementType.Set: kind = UniversalElementKind.Set; break;
                case LadderElementType.Reset: kind = UniversalElementKind.Reset; break;
                case LadderElementType.Timer: kind = UniversalElementKind.Timer; break;
                case LadderElementType.Counter: kind = UniversalElementKind.Counter; break;
                case LadderElementType.EdgeUp: kind = UniversalElementKind.RisingEdge; break;
                case LadderElementType.EdgeDown: kind = UniversalElementKind.FallingEdge; break;
                case LadderElementType.Function: kind = UniversalElementKind.Function; break;
                case LadderElementType.End: kind = UniversalElementKind.End; break;
                default:
                    report.UnsupportedCount++;
                    report.Warnings.Add("Elemento não suportado no rung " + (rung + 1).ToString() + ", coluna " + (column + 1).ToString() + (parallel ? " (paralelo)." : "."));
                    return null;
            }

            UniversalLadderElement target = new UniversalLadderElement();
            target.Kind = kind;
            target.Address = source.Address ?? string.Empty;
            target.Parameter = source.Parameter ?? string.Empty;
            target.FunctionCode = source.Mode ?? string.Empty;
            target.Column = column;
            return target;
        }

        public static string CheckTarget(UniversalLadderConversionReport report, PlcDeviceProfile profile)
        {
            if (report == null || report.Program == null) return "Não foi possível gerar o modelo Ladder universal.";
            if (profile == null) return "Nenhum controlador de destino selecionado.";

            IPlcDriver driver = PlcDriverRegistry.FindDriver(profile.DriverId);
            string text = "Modelo universal: " + report.Summary() + ".\r\n";
            text += "Destino: " + profile.Manufacturer + " " + profile.Model + " (" + profile.Protocol + ").\r\n";

            if (profile.SupportLevel == PlcSupportLevel.Planned)
            {
                text += "O driver deste controlador ainda está planejado. O projeto pode ser editado no modelo universal, mas não há comunicação ou compilação para este destino.";
                return text;
            }

            if (driver == null)
            {
                text += "O perfil existe, mas nenhum driver foi registrado.";
                return text;
            }

            text += "Driver: " + driver.DisplayName + ". Recursos atuais: " + driver.Capabilities.Summary() + ".\r\n";
            if (string.Equals(profile.DriverId, "weg.tp02.serial", StringComparison.OrdinalIgnoreCase))
                text += "A leitura do TP02 está implementada, mas a geração e o download do programa Ladder ainda dependem da validação do compilador de destino.";
            else if (string.Equals(profile.DriverId, "generic.modbus.rtu", StringComparison.OrdinalIgnoreCase) || string.Equals(profile.DriverId, "generic.modbus.tcp", StringComparison.OrdinalIgnoreCase))
                text += "Modbus genérico suporta monitoramento, não compilação Ladder. Para programar o PLC será necessário um compilador específico da família do fabricante.";
            else
                text += "Ainda não há compilador de destino registrado para esta família.";

            if (report.Warnings.Count > 0) text += "\r\nAvisos do modelo: " + string.Join(" | ", report.Warnings.ToArray());
            return text;
        }
    }
}
