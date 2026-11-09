namespace SmtMaterialDashboard.App;

internal sealed class DarkTabControl : TabControl
{
    private bool updatingItemSize;

    public DarkTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(220, 42);
        Padding = new Point(18, 8);
        BackColor = DashboardTheme.Background;
        ForeColor = DashboardTheme.Text;
        Font = new Font(DashboardTheme.UiFont, 11, FontStyle.Bold);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (updatingItemSize || TabCount == 0) return;
        var target = new Size(Math.Max(150, (ClientSize.Width - 10) / TabCount), 42);
        if (ItemSize == target) return;
        updatingItemSize = true;
        try
        {
            ItemSize = target;
        }
        finally
        {
            updatingItemSize = false;
        }
        Invalidate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var selected = e.Index == SelectedIndex;
        var bounds = GetTabRect(e.Index);
        using var background = new SolidBrush(selected ? DashboardTheme.SurfaceAlt : Color.FromArgb(8, 29, 50));
        using var border = new Pen(selected ? DashboardTheme.Cyan : DashboardTheme.Border, selected ? 2 : 1);
        e.Graphics.FillRectangle(background, bounds);
        e.Graphics.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, Font, bounds,
            selected ? DashboardTheme.Text : DashboardTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
