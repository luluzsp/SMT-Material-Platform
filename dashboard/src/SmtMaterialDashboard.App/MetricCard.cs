using System.ComponentModel;

namespace SmtMaterialDashboard.App;

internal sealed class MetricCard : Control
{
    private string _caption = string.Empty;
    private string _value = "0";
    private string _glyph = "!";
    private Color _accent = DashboardTheme.Amber;

    public MetricCard()
    {
        DoubleBuffered = true;
        BackColor = DashboardTheme.Surface;
        ForeColor = DashboardTheme.Text;
        Margin = new Padding(8);
        MinimumSize = new Size(180, 82);
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Caption { get => _caption; set { _caption = value; Invalidate(); } }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Value { get => _value; set { _value = value; Invalidate(); } }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Glyph { get => _glyph; set { _glyph = value; Invalidate(); } }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Accent { get => _accent; set { _accent = value; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        bounds.Inflate(-1, -1);
        using var path = Rounded(bounds, 12);
        using var background = new SolidBrush(BackColor);
        using var border = new Pen(DashboardTheme.Border, 1);
        e.Graphics.FillPath(background, path);
        e.Graphics.DrawPath(border, path);

        var iconSize = Math.Max(44, Height - 30);
        var iconRect = new Rectangle(16, (Height - iconSize) / 2, iconSize, iconSize);
        using var iconBackground = new SolidBrush(Color.FromArgb(42, Accent));
        using var iconBorder = new Pen(Accent, 2);
        e.Graphics.FillEllipse(iconBackground, iconRect);
        e.Graphics.DrawEllipse(iconBorder, iconRect);
        using var glyphFont = new Font(DashboardTheme.UiFont, Math.Max(18, iconSize * .42f), FontStyle.Bold, GraphicsUnit.Pixel);
        TextRenderer.DrawText(e.Graphics, Glyph, glyphFont, iconRect, DashboardTheme.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var textLeft = iconRect.Right + 16;
        var valueRect = new Rectangle(textLeft, 6, Width - textLeft - 14, Math.Max(30, Height / 2 - 8));
        var captionRect = new Rectangle(textLeft, Height / 2 + 7, Width - textLeft - 14, Math.Max(24, Height / 2 - 12));
        using var valueFont = new Font(DashboardTheme.UiFont, Math.Max(28, Height * .39f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var captionFont = new Font(DashboardTheme.UiFont, Math.Max(17, Height * .20f), FontStyle.Bold, GraphicsUnit.Pixel);
        TextRenderer.DrawText(e.Graphics, Value, valueFont, valueRect, DashboardTheme.Text,
            TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(e.Graphics, Caption, captionFont, captionRect, DashboardTheme.Text,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);
    }

    private static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
