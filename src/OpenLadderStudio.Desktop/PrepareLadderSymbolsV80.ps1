$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$path = Join-Path $root 'LadderEditor.build.cs'
if (-not (Test-Path $path)) { throw 'V80: LadderEditor.build.cs nao encontrado.' }

$text = [System.IO.File]::ReadAllText($path)
$script:eol = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }

function Replace-Required([string]$body, [string]$needle, [string]$replacement, [string]$label) {
    $n = $needle.Replace("`r`n", "`n").Replace("`n", $script:eol)
    $v = $replacement.Replace("`r`n", "`n").Replace("`n", $script:eol)
    if (-not $body.Contains($n)) { throw "V80: ancora nao encontrada ($label)." }
    return $body.Replace($n, $v)
}

if ($text.Contains('private static void DrawInnerMark')) {
    Write-Host 'OpenLadder Studio: simbolos Ladder V80 ja aplicados.' -ForegroundColor DarkGray
    return
}

# ---------------------------------------------------------------------------
# Ate aqui so contato NA, contato NF e bobina tinham simbolo. SET, RESET e as
# duas bordas eram desenhados como caixa de texto com o codigo da funcao TP02
# dentro ("SET F-23", "RST F-24", "↑ F-05"), o que nao e notacao Ladder e
# obriga o leitor a decorar o codigo em vez de reconhecer a forma.
#
# Notacao padrao, adotada aqui:
#
#   SET            -( S )-      bobina com marca
#   RESET          -( R )-      bobina com marca
#   Borda subida   -| P |-      contato com marca
#   Borda descida  -| N |-      contato com marca
#
# Bloco retangular fica reservado ao que e mesmo bloco: TMR, CNT, funcao e END.
# ---------------------------------------------------------------------------

$shapeOld = @'
                if (element.Type == LadderElementType.ContactNO || element.Type == LadderElementType.ContactNC)
                {
                    using (Pen clear = new Pen(background, 4.0f)) g.DrawLine(clear, cx - 16, y, cx + 16, y);
                    g.DrawLine(p, cx - 16, y - 13, cx - 16, y + 13);
                    g.DrawLine(p, cx + 16, y - 13, cx + 16, y + 13);
                    if (element.Type == LadderElementType.ContactNC)
                        g.DrawLine(p, cx - 20, y + 15, cx + 20, y - 15);
                }
                else if (element.Type == LadderElementType.Coil)
                {
                    using (Pen clear = new Pen(background, 4.0f)) g.DrawLine(clear, cx - 24, y, cx + 24, y);
                    g.DrawArc(p, new Rectangle(cx - 24, y - 16, 23, 32), 90, 180);
                    g.DrawArc(p, new Rectangle(cx + 1, y - 16, 23, 32), -90, 180);
                }
'@

$shapeNew = @'
                if (IsContactShape(element.Type))
                {
                    using (Pen clear = new Pen(background, 4.0f)) g.DrawLine(clear, cx - 16, y, cx + 16, y);
                    g.DrawLine(p, cx - 16, y - 13, cx - 16, y + 13);
                    g.DrawLine(p, cx + 16, y - 13, cx + 16, y + 13);
                    if (element.Type == LadderElementType.ContactNC)
                        g.DrawLine(p, cx - 20, y + 15, cx + 20, y - 15);
                    DrawInnerMark(g, InnerMark(element.Type), cx, y, symbol);
                }
                else if (IsCoilShape(element.Type))
                {
                    using (Pen clear = new Pen(background, 4.0f)) g.DrawLine(clear, cx - 24, y, cx + 24, y);
                    g.DrawArc(p, new Rectangle(cx - 24, y - 16, 23, 32), 90, 180);
                    g.DrawArc(p, new Rectangle(cx + 1, y - 16, 23, 32), -90, 180);
                    DrawInnerMark(g, InnerMark(element.Type), cx, y, symbol);
                }
'@
$text = Replace-Required $text $shapeOld $shapeNew 'formas de contato e bobina'

# Os rotulos passam a seguir a forma: quem tem simbolo mostra o endereco acima,
# como ja acontecia com contato e bobina. O codigo da funcao sai de dentro do
# desenho, porque a marca (S, R, P, N) ja identifica a instrucao.
$text = Replace-Required $text @'
            if (element.Type == LadderElementType.ContactNO || element.Type == LadderElementType.ContactNC || element.Type == LadderElementType.Coil) top = element.Address;
'@ @'
            if (IsContactShape(element.Type) || IsCoilShape(element.Type)) top = element.Address;
'@ 'rotulo de quem tem simbolo'

$text = Replace-Required $text @'
            else if (element.Type == LadderElementType.Set) { top = "SET F-23"; bottom = element.Address; }
            else if (element.Type == LadderElementType.Reset) { top = "RST F-24"; bottom = element.Address; }
            else if (element.Type == LadderElementType.EdgeUp) top = "↑ F-05";
            else if (element.Type == LadderElementType.EdgeDown) top = "↓ F-06";
'@ '' 'rotulos textuais de SET/RESET/bordas'

$text = Replace-Required $text @'
                if (element.Type == LadderElementType.ContactNO || element.Type == LadderElementType.ContactNC || element.Type == LadderElementType.Coil)
                {
                    SizeF size = g.MeasureString(top, f);
                    float ty = branch ? y + 14 : y - 32;
'@ @'
                if (IsContactShape(element.Type) || IsCoilShape(element.Type))
                {
                    SizeF size = g.MeasureString(top, f);
                    float ty = branch ? y + 14 : y - 32;
'@ 'posicao do rotulo'

# Helpers, inseridos antes do proprio DrawElement.
$helpers = @'
        private static bool IsContactShape(LadderElementType type)
        {
            return type == LadderElementType.ContactNO
                || type == LadderElementType.ContactNC
                || type == LadderElementType.EdgeUp
                || type == LadderElementType.EdgeDown;
        }

        private static bool IsCoilShape(LadderElementType type)
        {
            return type == LadderElementType.Coil
                || type == LadderElementType.Set
                || type == LadderElementType.Reset;
        }

        /// <summary>Letra que distingue a instrucao dentro do contato ou da bobina.</summary>
        private static string InnerMark(LadderElementType type)
        {
            if (type == LadderElementType.Set) return "S";
            if (type == LadderElementType.Reset) return "R";
            if (type == LadderElementType.EdgeUp) return "P";
            if (type == LadderElementType.EdgeDown) return "N";
            return string.Empty;
        }

        private static void DrawInnerMark(Graphics g, string mark, int cx, int y, Color colour)
        {
            if (string.IsNullOrEmpty(mark)) return;
            using (Font f = new Font("Consolas", 9.0f, FontStyle.Bold))
            using (Brush b = new SolidBrush(colour))
            {
                SizeF size = g.MeasureString(mark, f);
                g.DrawString(mark, f, b, cx - size.Width / 2f, y - size.Height / 2f);
            }
        }

        private static void DrawElement(Graphics g, LadderElement element, Rectangle cell, int y, bool branch, Color background)
'@

$text = Replace-Required $text `
    '        private static void DrawElement(Graphics g, LadderElement element, Rectangle cell, int y, bool branch, Color background)' `
    $helpers `
    'helpers de simbolo'

[System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)
Write-Host 'OpenLadder Studio: simbolos Ladder V80 aplicados (SET/RESET com marca, bordas P/N).' -ForegroundColor Cyan
