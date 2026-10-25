// Comentario de linha pendente: o menu Arquivo e a barra de ferramentas nao podem
// perde-lo. Roda contra o OpenLadderStudio.exe ja compilado, numa janela de verdade.
// Uso: powershell -ExecutionPolicy Bypass -File scripts/CheckPendingComment.ps1
//
// PerformClick dispara o mesmo delegate do clique do mouse e, como o ToolStrip nao
// tira o foco da caixa de texto, reproduz exatamente o pior caso: salvar/criar/abrir
// enquanto o campo de comentario ainda tem o texto que o usuario acabou de digitar.
// Foi esse caminho que perdeu texto na revisao da v1.73 (InvokeLadder confirma).
//
// Nao depende de primeiro plano nem de injecao de teclado, entao e deterministico.
// Atalhos de teclado ficam de fora de proposito: keybd_event e instavel quando nao ha
// usuario na sessao (o Windows recusa a troca de primeiro plano) e o atalho se perde
// em silencio. Precisa de uma sessao de desktop; nao roda no CI.
using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class CheckPendingComment
{
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc proc, IntPtr param);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr param);
    private delegate bool EnumProc(IntPtr hWnd, IntPtr param);

    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static Assembly app;
    private static Form shell;
    private static object ladder, canvas, properties;
    private static int checks, failures;

    [STAThread]
    private static int Main(string[] args)
    {
        string exe = Path.GetFullPath(args.Length > 0 ? args[0] : "OpenLadderStudio.exe");
        if (!File.Exists(exe)) { Console.WriteLine("Executavel nao encontrado: " + exe); return 2; }

        Application.EnableVisualStyles();
        app = Assembly.LoadFrom(exe);
        Type palette = app.GetType("ModernPC12.OpenLadderPalette", true);
        palette.GetField("loaded", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        palette.GetField("mode", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, Enum.Parse(app.GetType("ModernPC12.OpenLadderThemeMode", true), "Dark"));

        shell = (Form)Activator.CreateInstance(app.GetType("ModernPC12.UniversalStudioForm", true), true);
        shell.StartPosition = FormStartPosition.Manual;
        shell.Location = new Point(60, 60);
        shell.Size = new Size(1500, 900);

        Thread worker = new Thread(Run);
        worker.IsBackground = true;
        shell.Shown += delegate { worker.Start(); };

        Thread watchdog = new Thread(delegate () { Thread.Sleep(150000); Console.WriteLine("TEMPO ESGOTADO."); Environment.Exit(3); });
        watchdog.IsBackground = true;
        watchdog.Start();

        Application.Run(shell);
        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "Todas as verificacoes passaram (" + checks + ")." : failures + " de " + checks + " verificacoes falharam.");
        return failures == 0 ? 0 : 1;
    }

    // ---------- reflexao ----------

    private static object Field(object target, string name)
    {
        for (Type w = target.GetType(); w != null; w = w.BaseType)
        {
            FieldInfo f = w.GetField(name, Any);
            if (f != null) return f.GetValue(target);
        }
        throw new InvalidOperationException("Campo nao encontrado: " + name);
    }

    private static void SetField(object target, string name, object value)
    {
        for (Type w = target.GetType(); w != null; w = w.BaseType)
        {
            FieldInfo f = w.GetField(name, Any);
            if (f != null) { f.SetValue(target, value); return; }
        }
        throw new InvalidOperationException("Campo nao encontrado: " + name);
    }

    private static object Get(object target, string name)
    {
        for (Type w = target.GetType(); w != null; w = w.BaseType)
        {
            PropertyInfo p = w.GetProperty(name, Any);
            if (p != null) return p.GetValue(target, null);
            FieldInfo f = w.GetField(name, Any);
            if (f != null) return f.GetValue(target);
        }
        throw new InvalidOperationException("Membro nao encontrado: " + name);
    }

    private static void Set(object target, string name, object value)
    {
        for (Type w = target.GetType(); w != null; w = w.BaseType)
        {
            PropertyInfo p = w.GetProperty(name, Any);
            if (p != null && p.CanWrite) { p.SetValue(target, value, null); return; }
            FieldInfo f = w.GetField(name, Any);
            if (f != null) { f.SetValue(target, value); return; }
        }
        throw new InvalidOperationException("Membro nao encontrado: " + name);
    }

    private static object Call(object target, string name, params object[] args)
    {
        for (Type w = target.GetType(); w != null; w = w.BaseType)
        {
            MethodInfo m = w.GetMethod(name, Any);
            if (m != null) return m.Invoke(target, args);
        }
        throw new InvalidOperationException("Metodo nao encontrado: " + name);
    }

    private static object FieldOfType(object target, string typeName)
    {
        for (Type w = target.GetType(); w != null; w = w.BaseType)
            foreach (FieldInfo f in w.GetFields(Any))
                if (f.FieldType.Name == typeName) return f.GetValue(target);
        throw new InvalidOperationException("Campo do tipo " + typeName + " nao encontrado.");
    }

    private static IList Rungs() { return (IList)Field(ladder, "rungs"); }
    private static string RungComment(int i) { return (string)Get(Rungs()[i], "Comment"); }

    // ---------- procura de menu e barra ----------

    private static ToolStripItem FindMenuItem(string topText, string itemText)
    {
        foreach (Control c in AllControls(shell))
        {
            MenuStrip strip = c as MenuStrip;
            if (strip == null) continue;
            foreach (ToolStripItem top in strip.Items)
            {
                ToolStripMenuItem menu = top as ToolStripMenuItem;
                if (menu == null || Strip(menu.Text) != Strip(topText)) continue;
                foreach (ToolStripItem item in menu.DropDownItems)
                    if (Strip(item.Text).StartsWith(Strip(itemText))) return item;
            }
        }
        return null;
    }

    // A barra nao usa ToolStrip: sao controles proprios num painel. O clique e
    // disparado pelo OnClick do controle, que e o mesmo caminho do mouse.
    private static Control FindBarButton(string text)
    {
        Control found = null;
        foreach (Control c in AllControls(shell))
        {
            if (c is MenuStrip || c is ToolStrip) continue;
            if (Strip(c.Text) != Strip(text)) continue;
            if (c.GetType().GetMethod("OnClick", Any) == null) continue;
            // Prefere o que estiver mais acima na janela: a barra de ferramentas.
            if (found == null || c.PointToScreen(Point.Empty).Y < found.PointToScreen(Point.Empty).Y) found = c;
        }
        return found;
    }

    private static void ClickControl(Control control)
    {
        MethodInfo onClick = control.GetType().GetMethod("OnClick", Any);
        onClick.Invoke(control, new object[] { EventArgs.Empty });
    }

    private static string Strip(string text)
    {
        return (text ?? string.Empty).Replace("&", string.Empty).Trim();
    }

    private static IEnumerable AllControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control deep in AllControls(child)) yield return deep;
        }
    }

    // ---------- dialogos ----------

    private static bool IsUpdateNotice(string title) { return title.IndexOf("tualiza", StringComparison.OrdinalIgnoreCase) >= 0; }

    private static IntPtr FindDialog(bool updatesOnly)
    {
        IntPtr found = IntPtr.Zero;
        uint self = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        EnumWindows(delegate (IntPtr h, IntPtr p)
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid != self || !IsWindowVisible(h)) return true;
            StringBuilder cls = new StringBuilder(64);
            GetClassName(h, cls, cls.Capacity);
            if (cls.ToString() != "#32770") return true;
            StringBuilder title = new StringBuilder(256);
            GetWindowText(h, title, title.Capacity);
            if (IsUpdateNotice(title.ToString()) != updatesOnly) return true;
            found = h;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    // Texto de todos os controles filhos do dialogo (a mensagem e os botoes).
    private static string DialogText(IntPtr dialog)
    {
        StringBuilder all = new StringBuilder();
        EnumChildWindows(dialog, delegate (IntPtr h, IntPtr p)
        {
            StringBuilder t = new StringBuilder(512);
            GetWindowText(h, t, t.Capacity);
            all.Append(t.ToString()).Append(' ');
            return true;
        }, IntPtr.Zero);
        return all.ToString();
    }

    // Qualquer dialogo nao serve: sem a confirmacao, "Abrir" vai direto ao seletor de
    // arquivo e um teste que aceitasse qualquer janela passaria com o defeito. So conta
    // a pergunta de alteracoes nao salvas ("Existem alteracoes nao salvas...").
    private static bool WaitForDiscardQuestion(int timeoutMs, out string seen)
    {
        seen = null;
        for (int waited = 0; waited < timeoutMs; waited += 90)
        {
            IntPtr d = FindDialog(false);
            if (d != IntPtr.Zero)
            {
                StringBuilder title = new StringBuilder(256);
                GetWindowText(d, title, title.Capacity);
                string text = DialogText(d);
                seen = title.ToString();
                if (text.IndexOf("altera", StringComparison.OrdinalIgnoreCase) >= 0
                    && text.IndexOf("salv", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            Thread.Sleep(90);
        }
        return false;
    }

    private static void CloseDialogs()
    {
        for (int i = 0; i < 15; i++)
        {
            IntPtr d = FindDialog(false);
            if (d == IntPtr.Zero) return;
            PostMessage(d, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE = cancelar
            Thread.Sleep(220);
        }
    }

    private static void StartUpdateGuard()
    {
        Thread g = new Thread(delegate ()
        {
            while (true)
            {
                IntPtr n = FindDialog(true);
                if (n != IntPtr.Zero) PostMessage(n, 0x0010, IntPtr.Zero, IntPtr.Zero);
                Thread.Sleep(200);
            }
        });
        g.IsBackground = true;
        g.Start();
    }

    // ---------- apoio ----------

    private static void Check(string what, bool ok)
    {
        checks++;
        if (!ok) failures++;
        Console.WriteLine((ok ? "  ok    " : "  FALHA ") + what);
    }

    private static void CheckEquals(string what, string expected, string actual)
    {
        bool ok = expected == actual;
        checks++;
        if (!ok) failures++;
        Console.WriteLine((ok ? "  ok    " : "  FALHA ") + what + (ok ? string.Empty : "  [esperado \"" + expected + "\", obtido \"" + actual + "\"]"));
    }

    private static void OnUi(MethodInvoker action)
    {
        Exception captured = null;
        shell.Invoke((MethodInvoker)delegate { try { action(); } catch (Exception ex) { captured = ex.GetBaseException(); } });
        if (captured != null) throw new InvalidOperationException(captured.Message, captured);
        Thread.Sleep(280);
    }

    private static void TypeInto(object control, string text)
    {
        IntPtr h = (IntPtr)shell.Invoke((Func<IntPtr>)delegate { return (IntPtr)Get(control, "Handle"); });
        foreach (char c in text) { PostMessage(h, 0x0102, (IntPtr)c, IntPtr.Zero); Thread.Sleep(26); }
        Thread.Sleep(240);
    }

    private static void Reset(string path)
    {
        OnUi(delegate
        {
            Call(canvas, "EndEditComment", false, false);
            Call(ladder, "NewProject", false);
            SetField(ladder, "currentFile", path);
            SetField(ladder, "dirty", false);
            SetField(properties, "boundRung", -1);
            SetField(properties, "boundComment", string.Empty);
            Set(Field(properties, "commentBox"), "Text", string.Empty);
        });
    }

    private static void BindPanel(int rung)
    {
        OnUi(delegate
        {
            Set(canvas, "SelectedRung", rung);
            Call(properties, "ShowRung", rung, RungComment(rung));
            object box = Field(properties, "commentBox");
            Call(shell, "Activate");
            Set(shell, "ActiveControl", box);
            Call(box, "Focus");
        });
    }

    // Marca o documento aberto com um contato que nenhum projeto novo teria. Sem isso,
    // "o comentario esta na linha 1" passaria tambem quando o projeto foi trocado e o
    // texto caiu na linha 1 do projeto NOVO, que e justamente a contaminacao a pegar.
    private const string Marker = "X0777";

    private static void MarkDocument()
    {
        OnUi(delegate
        {
            object element = ((Array)Get(Rungs()[0], "Elements")).GetValue(0);
            Set(element, "Type", Enum.Parse(app.GetType("ModernPC12.LadderElementType", true), "ContactNO"));
            Set(element, "Address", Marker);
        });
    }

    private static bool IsOriginalDocument()
    {
        object element = ((Array)Get(Rungs()[0], "Elements")).GetValue(0);
        return (string)Get(element, "Address") == Marker;
    }

    private static bool PanelStillFocused()
    {
        return (bool)shell.Invoke((Func<bool>)delegate
        {
            object active = shell;
            object box = Field(properties, "commentBox");
            while (active != null)
            {
                if (ReferenceEquals(active, box)) return true;
                PropertyInfo p = active.GetType().GetProperty("ActiveControl", Any);
                active = p == null ? null : p.GetValue(active, null);
            }
            return false;
        });
    }

    // ---------- roteiro ----------

    private static void Run()
    {
        try
        {
            ladder = Field(shell, "ladderForm");
            canvas = Field(ladder, "canvas");
            properties = FieldOfType(shell, "SelectionPropertiesCard");
            StartUpdateGuard();
            Thread.Sleep(2200);
            Scenarios();
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("ERRO: " + ex.GetBaseException().Message);
            Console.WriteLine(ex.GetBaseException().StackTrace);
        }
        finally
        {
            try { shell.Invoke((MethodInvoker)delegate { Application.ExitThread(); }); }
            catch { Environment.Exit(failures == 0 ? 0 : 1); }
        }
    }

    private static void Scenarios()
    {
        string dir = Path.Combine(Path.GetTempPath(), "openladder-menu-smoke");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "menu.pladder");

        Console.WriteLine("Menu Arquivo e barra de ferramentas com comentario pendente");

        ToolStripItem salvarMenu = FindMenuItem("Arquivo", "Salvar");
        ToolStripItem novoMenu = FindMenuItem("Arquivo", "Novo");
        ToolStripItem abrirMenu = FindMenuItem("Arquivo", "Abrir");
        Control salvarBarra = FindBarButton("Salvar");
        Check("item 'Salvar' do menu Arquivo encontrado", salvarMenu != null);
        Check("item 'Novo projeto' do menu Arquivo encontrado", novoMenu != null);
        Check("item 'Abrir' do menu Arquivo encontrado", abrirMenu != null);
        Check("botao 'Salvar' da barra encontrado", salvarBarra != null);
        if (salvarMenu == null || novoMenu == null || salvarBarra == null) return;

        // 1. Menu Arquivo > Salvar, com o texto ainda na caixa do painel.
        Console.WriteLine();
        Console.WriteLine("1. Menu Arquivo > Salvar");
        if (File.Exists(path)) File.Delete(path);
        Reset(path);
        BindPanel(0);
        TypeInto(Field(properties, "commentBox"), "menu");
        Check("o foco continua no campo ao acionar o menu", PanelStillFocused());
        OnUi(delegate { salvarMenu.PerformClick(); });
        Thread.Sleep(400);
        CheckEquals("o comentario entrou no documento", "menu", RungComment(0));
        Check("o arquivo foi gravado", File.Exists(path));
        string saved = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        Check("o arquivo contem o comentario", saved.Contains("COMMENT|menu"));

        // 2. Botao Salvar da barra de ferramentas.
        Console.WriteLine();
        Console.WriteLine("2. Barra de ferramentas > Salvar");
        if (File.Exists(path)) File.Delete(path);
        Reset(path);
        BindPanel(1);
        TypeInto(Field(properties, "commentBox"), "barra");
        Check("o foco continua no campo ao acionar a barra", PanelStillFocused());
        OnUi(delegate { ClickControl(salvarBarra); });
        Thread.Sleep(400);
        CheckEquals("o comentario da linha 2 entrou no documento", "barra", RungComment(1));
        saved = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        Check("o arquivo contem o comentario da linha 2", saved.Contains("COMMENT|barra"));

        // 3. Menu Arquivo > Novo projeto: tem de perguntar, e nao contaminar o novo.
        Console.WriteLine();
        Console.WriteLine("3. Menu Arquivo > Novo projeto");
        Reset(path);
        MarkDocument();
        BindPanel(0);
        TypeInto(Field(properties, "commentBox"), "novo");
        shell.BeginInvoke((MethodInvoker)delegate { novoMenu.PerformClick(); });
        string seen;
        bool asked = WaitForDiscardQuestion(5000, out seen);
        Check("perguntou se quer salvar antes de trocar de projeto" + (asked || seen == null ? string.Empty : " [apareceu outro dialogo: " + seen + "]"), asked);
        CloseDialogs();
        Thread.Sleep(400);
        Check("cancelar manteve o documento original aberto", IsOriginalDocument());
        CheckEquals("o comentario ficou no documento em que foi escrito", "novo", RungComment(0));
        Check("nenhum dialogo ficou aberto", FindDialog(false) == IntPtr.Zero);

        // 4. Menu Arquivo > Abrir: mesmo funil.
        Console.WriteLine();
        Console.WriteLine("4. Menu Arquivo > Abrir");
        Reset(path);
        MarkDocument();
        BindPanel(0);
        TypeInto(Field(properties, "commentBox"), "abrir");
        shell.BeginInvoke((MethodInvoker)delegate { abrirMenu.PerformClick(); });
        asked = WaitForDiscardQuestion(5000, out seen);
        Check("perguntou se quer salvar antes de abrir outro projeto" + (asked || seen == null ? string.Empty : " [apareceu outro dialogo: " + seen + "]"), asked);
        CloseDialogs();
        Thread.Sleep(400);
        Check("cancelar manteve o documento original aberto", IsOriginalDocument());
        CheckEquals("o comentario ficou no documento em que foi escrito", "abrir", RungComment(0));
        Check("nenhum dialogo ficou aberto", FindDialog(false) == IntPtr.Zero);

        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
