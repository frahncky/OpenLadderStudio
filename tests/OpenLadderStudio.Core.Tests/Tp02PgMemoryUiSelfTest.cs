using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ModernPC12;

internal static class Tp02PgMemoryUiSelfTest
{
    private static int failures;
    private static int checks;

    private static void Check(bool value, string message)
    {
        checks++;
        if (value) return;
        failures++;
        Console.Error.WriteLine("FALHA UI: " + message);
    }

    private static T Field<T>(Form form, string name) where T : Control
    {
        return (T)form.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
    }

    private static void Generate(Form form)
    {
        form.GetType().GetMethod("Generate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
        Application.DoEvents();
    }

    private static void Bounds(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (!child.Visible) continue;
            Check(child.Left >= 0 && child.Top >= 0 && child.Right <= parent.ClientSize.Width + 1
                && child.Bottom <= parent.ClientSize.Height + 1, "controle cabe no painel: " + child.GetType().Name);
            if (child is TableLayoutPanel || child is FlowLayoutPanel) Bounds(child);
        }
    }

    [STAThread]
    public static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string pictures = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\artifacts\\tp02-memory-ui"));
        Directory.CreateDirectory(pictures);
        try
        {
            foreach (float scale in new float[] { 1.0f, 1.5f, 2.0f })
            {
                using (TP02PgMemoryForm form = new TP02PgMemoryForm())
                {
                    form.Size = form.MinimumSize;
                    form.Show();
                    if (scale != 1.0f) form.Scale(new SizeF(scale, scale));
                    Application.DoEvents();
                    ComboBox operation = Field<ComboBox>(form, "operation");
                    TextBox input = Field<TextBox>(form, "input");
                    TextBox output = Field<TextBox>(form, "output");
                    Check(Field<NumericUpDown>(form, "number").Maximum == 1024, "limite inicial V1024");
                    Generate(form);
                    Check(output.Text.Contains("Quadros gerados: 16") && output.Text.Contains("0A 03 53 C0 80 5F"), "UI gera todas as páginas V");
                    for (int i = 0; i < operation.Items.Count; i++)
                    {
                        operation.SelectedIndex = i;
                        Application.DoEvents();
                        Bounds(form);
                        Check(output.Height >= 80 * scale, "resultado permanece legível na operação " + i);
                    }
                    operation.SelectedIndex = 6;
                    Check(Field<NumericUpDown>(form, "number").Maximum == 130, "FL restringe número a 130");
                    operation.SelectedIndex = 13;
                    input.Text = "00 06 04 D2 01 00 0F FF 14";
                    Generate(form);
                    Check(output.Text.Contains("Atual (ms): 1234") && output.Text.Contains("Mínimo (ms): 256")
                        && output.Text.Contains("Máximo (ms): 4095"), "UI mantém ordem atual/mínimo/máximo");
                    using (Bitmap picture = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(picture, new Rectangle(Point.Empty, picture.Size));
                        picture.Save(Path.Combine(pictures, "memory-scale-" + ((int)(scale * 100)) + ".png"), ImageFormat.Png);
                    }
                    input.Text = "00 06 04 D2 01 00 0F FF 15";
                    Generate(form);
                    Check(output.Text.Contains("checksum inválido") && !output.Text.Contains("Atual (ms)"), "RX inválido não deixa valores antigos");
                    operation.SelectedIndex = 9;
                    input.Text = "58 57 23 31 3 12 99";
                    Generate(form);
                    Check(output.Text.Contains("09 11 53 F9 0E 00 3A 00 39 00 17 00 1F 00 03 00 0C 00 63 70"), "UI monta RTC nativo");
                    form.Close();
                }
            }
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        Console.WriteLine("Tp02PgMemoryUiSelfTest: " + checks + " verificações; " + failures + " falhas (sem comunicação com PLC).");
        return failures == 0 ? 0 : 1;
    }
}
