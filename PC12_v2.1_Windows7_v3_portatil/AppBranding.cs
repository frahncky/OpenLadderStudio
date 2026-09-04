using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ModernPC12
{
    internal static class AppBranding
    {
        private static bool installed;
        private static Icon appIcon;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            appIcon = LoadIcon();
            Application.Idle += ApplyToOpenForms;
            ApplyToOpenForms(null, EventArgs.Empty);
        }

        public static void Apply(Form form)
        {
            if (form == null) return;
            if (appIcon == null) appIcon = LoadIcon();
            if (appIcon == null) return;

            try
            {
                form.Icon = (Icon)appIcon.Clone();
                form.ShowIcon = true;
            }
            catch
            {
            }
        }

        private static void ApplyToOpenForms(object sender, EventArgs e)
        {
            if (appIcon == null) return;
            try
            {
                for (int i = 0; i < Application.OpenForms.Count; i++)
                {
                    Form form = Application.OpenForms[i];
                    if (form == null || form.IsDisposed) continue;
                    Apply(form);
                }
            }
            catch
            {
            }
        }

        private static Icon LoadIcon()
        {
            try
            {
                string external = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenLadderStudio.ico");
                if (File.Exists(external)) return new Icon(external);
            }
            catch
            {
            }

            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return null;
            }
        }
    }
}
