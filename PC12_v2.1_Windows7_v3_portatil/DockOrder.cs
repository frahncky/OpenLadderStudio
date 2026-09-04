using System.Windows.Forms;

namespace ModernPC12
{
    /// <summary>
    /// Ordena a ancoragem dos filhos de um container.
    ///
    /// O layout do Windows Forms percorre os filhos do ultimo indice para o primeiro:
    /// quem esta no indice mais alto escolhe seu espaco primeiro e fica na borda
    /// externa. Um controle Fill inserido por ultimo ocupa toda a area util e as
    /// barras ancoradas passam a se sobrepor ao conteudo.
    ///
    /// Passe o controle Fill primeiro e depois as barras, da mais interna para a mais
    /// externa.
    /// </summary>
    internal static class DockOrder
    {
        public static void Apply(Control parent, params Control[] fillThenInnerToOuter)
        {
            if (parent == null || fillThenInnerToOuter == null) return;
            int i;
            for (i = 0; i < fillThenInnerToOuter.Length; i++)
            {
                Control c = fillThenInnerToOuter[i];
                if (c != null && c.Parent == parent) parent.Controls.SetChildIndex(c, i);
            }
        }
    }
}
