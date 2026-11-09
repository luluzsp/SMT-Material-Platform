using SmtMaterialDashboard.Core;

namespace SmtMaterialDashboard.App;

internal sealed class SettingsForm : Form
{
    private readonly ComboBox _resolution = Combo();
    private readonly NumericUpDown _scale = Number(75, 200, 100);
    private readonly ComboBox _description = Combo();
    private readonly CheckBox _autoScroll = new();
    private readonly NumericUpDown _scrollSeconds = Number(1, 30, 3);
    private readonly NumericUpDown _scrollPause = Number(1, 60, 5);
    private readonly ComboBox _sortDirection = Combo();
    private readonly TextBox _factory = Input();
    private readonly TextBox _warehouse = Input();
    private readonly ComboBox _environment = Combo();
    private readonly TextBox _mesTestAddress = Input();
    private readonly TextBox _mesProductionAddress = Input();
    private readonly TextBox _wmsTestAddress = Input();
    private readonly TextBox _wmsProductionAddress = Input();
    private readonly CheckBox _demoMode = new();
    private readonly NumericUpDown _refreshSeconds = Number(5, 3600, 30);
    private readonly NumericUpDown _timeoutSeconds = Number(3, 120, 10);
    private readonly NumericUpDown _wmsPageSize = Number(1, 10000, 200);
    private readonly CheckBox _enableInterfaceLogging = new();
    private readonly NumericUpDown _issuanceTimeMinutes = Number(1, 1440, 30);
    private readonly DataGridView _lines = new();
    private readonly DataGridView _excludedLocations = new();
    private readonly int _initialTab;

    public SettingsForm(DashboardSettings source, int initialTab = 0)
    {
        Settings = Clone(source);
        _initialTab = Math.Clamp(initialTab, 0, 2);
        Text = "看板设置";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1100, 820);
        MinimumSize = new Size(960, 720);
        BackColor = DashboardTheme.Background;
        ForeColor = DashboardTheme.Text;
        Font = new Font(DashboardTheme.UiFont, 10.5f);
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = false;

        BuildUi();
        LoadValues();
    }

    public DashboardSettings Settings { get; private set; }

    private void BuildUi()
    {
        var tabs = new DarkTabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildDisplayPage());
        tabs.TabPages.Add(BuildDataPage());
        tabs.TabPages.Add(BuildLinePage());
        tabs.SelectedIndex = _initialTab;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            Padding = new Padding(18, 11, 18, 9),
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.FromArgb(8, 29, 50)
        };
        var save = ActionButton("保存设置", true);
        save.Click += (_, _) => SaveAndClose();
        var cancel = ActionButton("取消");
        cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.AddRange([save, cancel]);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0), Padding = new Padding(0) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        tabs.Dock = DockStyle.Fill;
        tabs.Margin = new Padding(0);
        buttons.Dock = DockStyle.Fill;
        buttons.Margin = new Padding(0);
        root.Controls.Add(BuildSettingsHeader(), 0, 0);
        root.Controls.Add(tabs, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static Control BuildSettingsHeader()
    {
        var header = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 10, 24, 8) };
        header.Paint += (_, e) =>
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                header.ClientRectangle, DashboardTheme.HeaderStart, DashboardTheme.HeaderEnd, 0f);
            e.Graphics.FillRectangle(brush, header.ClientRectangle);
        };
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "看板系统设置",
            ForeColor = DashboardTheme.Text,
            BackColor = Color.Transparent,
            Font = new Font(DashboardTheme.UiFont, 18, FontStyle.Bold),
            Location = new Point(24, 9)
        });
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "显示、接口、线体与库存规则",
            ForeColor = DashboardTheme.Muted,
            BackColor = Color.Transparent,
            Font = new Font(DashboardTheme.UiFont, 10),
            Location = new Point(26, 46)
        });
        return header;
    }

    private TabPage BuildDisplayPage()
    {
        var page = Page("显示与滚动");
        var layout = FormTable();
        _resolution.Items.AddRange(["3840 × 2160", "2560 × 1440", "1920 × 1080", "1600 × 900", "1366 × 768", "自动适配"]);
        _description.Items.AddRange(["单行省略", "两行省略（推荐）", "完整换行"]);
        AddField(layout, "屏幕分辨率", _resolution, 0, "全屏显示时按预设分辨率调整字号和行高");
        AddField(layout, "界面缩放（%）", _scale, 1);
        AddField(layout, "物料描述显示", _description, 2, "默认“两行省略”：固定列宽、统一行高，鼠标悬停显示完整描述");
        _autoScroll.Text = "明细过多时自动循环滚动（鼠标移入表格时暂停）";
        _autoScroll.ForeColor = DashboardTheme.Text;
        _autoScroll.AutoSize = true;
        layout.Controls.Add(_autoScroll, 1, 3);
        AddField(layout, "每行滚动间隔（秒）", _scrollSeconds, 4);
        AddField(layout, "滚动到底停留（秒）", _scrollPause, 5);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildDataPage()
    {
        var page = Page("接口与排序");
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var layout = FormTable();
        _sortDirection.Items.AddRange(["升序（从早到晚）", "降序（从晚到早）"]);
        _environment.Items.AddRange(["测试环境", "生产环境"]);
        _demoMode.Text = "开启演示模式（不调用 MES/WMS 接口，使用内置示例数据）";
        _demoMode.ForeColor = DashboardTheme.Text;
        _demoMode.AutoSize = true;
        _enableInterfaceLogging.Text = "记录 MES/WMS 详细请求日志（URL、参数、时间及响应正文）";
        _enableInterfaceLogging.ForeColor = DashboardTheme.Text;
        _enableInterfaceLogging.AutoSize = true;
        AddField(layout, "当前运行环境", _environment, 0, "切换后使用对应环境的 MES、WMS 地址");
        AddField(layout, "MES 测试区 IP/地址", _mesTestAddress, 1, "支持 IP:端口或完整 URL");
        AddField(layout, "WMS 测试区 IP/地址", _wmsTestAddress, 2, "支持 IP:端口或完整 URL");
        AddField(layout, "MES 生产环境 IP/地址", _mesProductionAddress, 3, "支持 IP:端口或完整 URL");
        AddField(layout, "WMS 生产环境 IP/地址", _wmsProductionAddress, 4, "支持 IP:端口或完整 URL");
        layout.Controls.Add(new Label { Text = "数据模式", ForeColor = DashboardTheme.Muted, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 12, 8, 8) }, 0, 5);
        layout.Controls.Add(_demoMode, 1, 5);
        AddField(layout, "工厂 Factory", _factory, 6);
        AddField(layout, "仓库 ID", _warehouse, 7);
        AddField(layout, "WMS 每页记录数", _wmsPageSize, 8, "作为 getPage 接口的 pageSize 参数，默认 200");
        layout.Controls.Add(new Label { Text = "接口日志", ForeColor = DashboardTheme.Muted, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 12, 8, 8) }, 0, 9);
        layout.Controls.Add(_enableInterfaceLogging, 1, 9);
        AddField(layout, "自动刷新间隔（秒）", _refreshSeconds, 10);
        AddField(layout, "接口超时（秒）", _timeoutSeconds, 11);
        AddField(layout, "发料时限（分钟）", _issuanceTimeMinutes, 12, "当前时间超过叫料时间与发料时限之和后，状态变为“发料超时”");
        AddField(layout, "叫料时间排序", _sortDirection, 13, "缺料明细固定按叫料时间排序");
        scroll.Controls.Add(layout);
        page.Controls.Add(scroll);
        return page;
    }

    private TabPage BuildLinePage()
    {
        var page = Page("线体与库存排除");
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 510, SplitterWidth = 8, BackColor = DashboardTheme.Background };
        split.Resize += (_, _) =>
        {
            var distance = Math.Max(360, (split.ClientSize.Width - split.SplitterWidth) / 2);
            if (distance < split.ClientSize.Width - split.Panel2MinSize) split.SplitterDistance = distance;
        };

        var linePanel = Section("线体与 UPH", "逐行维护生产线名称和对应 UPH，可在最后一行继续新增。");
        _lines.Dock = DockStyle.Fill;
        ConfigureSettingsGrid(_lines);
        _lines.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _lines.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "线体", Name = "Line" });
        _lines.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "UPH", Name = "Uph" });
        linePanel.Controls.Add(BuildGridEditor(_lines, "线体与 UPH", ValidateLineGrid));
        split.Panel1.Padding = new Padding(8);
        split.Panel1.Controls.Add(linePanel);

        var excludePanel = Section("物料库位排除", "逐行维护需要排除的 WMS 库位编码（storeCode）；对应记录及库存数量不会进入看板统计。");
        _excludedLocations.Dock = DockStyle.Fill;
        ConfigureSettingsGrid(_excludedLocations);
        _excludedLocations.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "排除库位编码（storeCode）", Name = "StoreCode", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        excludePanel.Controls.Add(BuildGridEditor(_excludedLocations, "物料库位排除"));
        split.Panel2.Padding = new Padding(8);
        split.Panel2.Controls.Add(excludePanel);
        page.Controls.Add(split);
        return page;
    }

    private void LoadValues()
    {
        _resolution.SelectedItem = Settings.Resolution;
        if (_resolution.SelectedIndex < 0) _resolution.SelectedItem = "自动适配";
        _scale.Value = Math.Clamp(Settings.UiScalePercent, (int)_scale.Minimum, (int)_scale.Maximum);
        _description.SelectedIndex = (int)Settings.DescriptionMode;
        _autoScroll.Checked = Settings.AutoScroll;
        _scrollSeconds.Value = Settings.ScrollSeconds;
        _scrollPause.Value = Settings.ScrollPauseSeconds;
        _factory.Text = Settings.Factory;
        _warehouse.Text = Settings.WarehouseId;
        _environment.SelectedIndex = Settings.Environment == SystemEnvironment.Test ? 0 : 1;
        _mesTestAddress.Text = Settings.MesTestAddress;
        _mesProductionAddress.Text = Settings.MesProductionAddress;
        _wmsTestAddress.Text = Settings.WmsTestAddress;
        _wmsProductionAddress.Text = Settings.WmsProductionAddress;
        _demoMode.Checked = Settings.DemoMode;
        _enableInterfaceLogging.Checked = Settings.EnableInterfaceLogging;
        _refreshSeconds.Value = Settings.RefreshSeconds;
        _timeoutSeconds.Value = Settings.TimeoutSeconds;
        _wmsPageSize.Value = Settings.WmsPageSize;
        _issuanceTimeMinutes.Value = Settings.IssuanceTimeMinutes;
        _sortDirection.SelectedIndex = (int)Settings.SortDirection;
        foreach (var line in Settings.Lines) _lines.Rows.Add(line.Line, line.Uph);
        foreach (var location in Settings.ExcludedLocations) _excludedLocations.Rows.Add(location);
    }

    private void SaveAndClose()
    {
        _lines.EndEdit();
        _excludedLocations.EndEdit();
        var factory = _factory.Text.Trim();
        var warehouse = _warehouse.Text.Trim();
        if (factory.Length == 0 || warehouse.Length == 0)
        {
            MessageBox.Show(this, "工厂和仓库 ID 不能为空。", "设置校验", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var addresses = new[] { _mesTestAddress.Text, _wmsTestAddress.Text, _mesProductionAddress.Text, _wmsProductionAddress.Text };
        if (addresses.Any(string.IsNullOrWhiteSpace))
        {
            MessageBox.Show(this, "MES、WMS 的测试区和生产环境地址都不能为空。", "设置校验", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            DashboardApiClient.BuildEndpoint(_mesTestAddress.Text, _mesTestAddress.Text, "/MES2/Service.action");
            DashboardApiClient.BuildWmsEndpoint(_wmsTestAddress.Text, _wmsTestAddress.Text, "/wms3.0/ms/exi/wmsMaterialStore/getPage");
            DashboardApiClient.BuildEndpoint(_mesProductionAddress.Text, _mesProductionAddress.Text, "/MES2/Service.action");
            DashboardApiClient.BuildWmsEndpoint(_wmsProductionAddress.Text, _wmsProductionAddress.Text, "/wms3.0/ms/exi/wmsMaterialStore/getPage");
        }
        catch (DashboardApiException exception)
        {
            MessageBox.Show(this, exception.Message, "接口地址校验", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var lines = new List<LineSetting>();
        foreach (DataGridViewRow row in _lines.Rows)
        {
            if (row.IsNewRow) continue;
            var line = row.Cells[0].Value?.ToString()?.Trim() ?? string.Empty;
            if (line.Length == 0) continue;
            if (!decimal.TryParse(row.Cells[1].Value?.ToString(), out var uph) || uph <= 0)
            {
                MessageBox.Show(this, $"线体 {line} 的 UPH 必须是大于 0 的数字。", "设置校验", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            lines.Add(new LineSetting { Line = line, Uph = uph });
        }

        Settings = new DashboardSettings
        {
            Environment = _environment.SelectedIndex == 0 ? SystemEnvironment.Test : SystemEnvironment.Production,
            MesTestAddress = _mesTestAddress.Text.Trim(),
            MesProductionAddress = _mesProductionAddress.Text.Trim(),
            WmsTestAddress = _wmsTestAddress.Text.Trim(),
            WmsProductionAddress = _wmsProductionAddress.Text.Trim(),
            DemoMode = _demoMode.Checked,
            EnableInterfaceLogging = _enableInterfaceLogging.Checked,
            Factory = factory,
            WarehouseId = warehouse,
            Resolution = _resolution.SelectedItem?.ToString() ?? "自动适配",
            UiScalePercent = (int)_scale.Value,
            RefreshSeconds = (int)_refreshSeconds.Value,
            TimeoutSeconds = (int)_timeoutSeconds.Value,
            WmsPageSize = (int)_wmsPageSize.Value,
            IssuanceTimeMinutes = (int)_issuanceTimeMinutes.Value,
            AutoScroll = _autoScroll.Checked,
            ScrollSeconds = (int)_scrollSeconds.Value,
            ScrollPauseSeconds = (int)_scrollPause.Value,
            SortDirection = _sortDirection.SelectedIndex == 1 ? SortDirection.Descending : SortDirection.Ascending,
            DescriptionMode = _description.SelectedIndex switch { 0 => DescriptionMode.SingleLine, 2 => DescriptionMode.Full, _ => DescriptionMode.TwoLines },
            Lines = lines,
            ExcludedLocations = ReadExcludedLocations()
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private List<string> ReadExcludedLocations() => _excludedLocations.Rows
        .Cast<DataGridViewRow>()
        .Where(row => !row.IsNewRow)
        .Select(row => row.Cells[0].Value?.ToString()?.Trim() ?? string.Empty)
        .Where(value => value.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private Control BuildGridEditor(DataGridView grid, string itemName, Func<string?>? validate = null)
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = DashboardTheme.Surface };
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
            BackColor = DashboardTheme.Surface
        };
        var status = new Label
        {
            AutoSize = false,
            Width = 130,
            Height = 36,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DashboardTheme.Muted,
            Margin = new Padding(8, 0, 0, 0)
        };
        var add = EditorButton("新增");
        add.Click += (_, _) =>
        {
            var rowIndex = grid.Rows.Add();
            grid.CurrentCell = grid.Rows[rowIndex].Cells[0];
            grid.BeginEdit(true);
            status.Text = $"正在新增{itemName}";
            status.ForeColor = DashboardTheme.Muted;
        };
        var edit = EditorButton("修改");
        edit.Click += (_, _) =>
        {
            if (grid.CurrentRow is null || grid.CurrentRow.IsNewRow)
            {
                MessageBox.Show(this, $"请先选择需要修改的{itemName}记录。", "修改", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            grid.CurrentCell ??= grid.CurrentRow.Cells[0];
            grid.BeginEdit(true);
            status.Text = $"正在修改{itemName}";
            status.ForeColor = DashboardTheme.Muted;
        };
        var remove = EditorButton("删除", true);
        remove.Click += (_, _) =>
        {
            var row = grid.CurrentRow;
            if (row is null || row.IsNewRow)
            {
                MessageBox.Show(this, $"请先选择需要删除的{itemName}记录。", "删除", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, $"确定删除当前{itemName}记录吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            grid.Rows.Remove(row);
            status.Text = "记录已删除，等待保存设置";
            status.ForeColor = DashboardTheme.Muted;
        };
        var save = EditorButton("保存本表", primary: true);
        save.Click += (_, _) =>
        {
            grid.EndEdit();
            var error = validate?.Invoke();
            if (!string.IsNullOrWhiteSpace(error))
            {
                MessageBox.Show(this, error, "表格校验", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            status.Text = "✓ 已确认，保存设置后生效";
            status.ForeColor = DashboardTheme.Green;
        };
        toolbar.Controls.AddRange([add, edit, remove, save, status]);
        host.Controls.Add(grid);
        host.Controls.Add(toolbar);
        return host;
    }

    private string? ValidateLineGrid()
    {
        foreach (DataGridViewRow row in _lines.Rows)
        {
            if (row.IsNewRow) continue;
            var line = row.Cells[0].Value?.ToString()?.Trim() ?? string.Empty;
            if (line.Length == 0) continue;
            if (!decimal.TryParse(row.Cells[1].Value?.ToString(), out var uph) || uph <= 0)
                return $"线体 {line} 的 UPH 必须是大于 0 的数字。";
        }
        return null;
    }

    private static TabPage Page(string text) => new(text) { BackColor = DashboardTheme.Background, ForeColor = DashboardTheme.Text, Padding = new Padding(14) };

    private static Panel Section(string title, string subtitle)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = DashboardTheme.Surface, Padding = new Padding(16, 78, 16, 16) };
        panel.Controls.Add(new Label
        {
            Text = title,
            ForeColor = DashboardTheme.Text,
            Font = new Font(DashboardTheme.UiFont, 13, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(14, 14)
        });
        panel.Controls.Add(new Label
        {
            Text = subtitle,
            ForeColor = DashboardTheme.Muted,
            Font = new Font(DashboardTheme.UiFont, 9.5f),
            AutoEllipsis = true,
            AutoSize = false,
            Height = 34,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Location = new Point(16, 42),
            Width = Math.Max(200, panel.Width - 32)
        });
        panel.Resize += (_, _) => panel.Controls[1].Width = Math.Max(200, panel.ClientSize.Width - 32);
        return panel;
    }

    private static TableLayoutPanel FormTable()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(26, 24, 26, 28), BackColor = DashboardTheme.Surface, Margin = new Padding(8) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static void AddField(TableLayoutPanel table, string label, Control input, int row, string? hint = null)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var caption = new Label { Text = label, ForeColor = DashboardTheme.Text, Font = new Font(DashboardTheme.UiFont, 10.5f, FontStyle.Bold), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 13, 12, 9) };
        input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        input.Margin = new Padding(0, 7, 0, hint is null ? 7 : 0);
        table.Controls.Add(caption, 0, row);
        if (hint is null)
        {
            table.Controls.Add(input, 1, row);
        }
        else
        {
            var host = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, RowCount = 2, ColumnCount = 1, Margin = new Padding(0) };
            host.Controls.Add(input, 0, 0);
            host.Controls.Add(new Label { Text = hint, ForeColor = DashboardTheme.Muted, AutoSize = true, Margin = new Padding(0, 4, 0, 9) }, 0, 1);
            table.Controls.Add(host, 1, row);
        }
    }

    private static ComboBox Combo()
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 28,
            Width = 320,
            BackColor = Color.FromArgb(7, 27, 47),
            ForeColor = DashboardTheme.Text,
            FlatStyle = FlatStyle.Flat
        };
        combo.DrawItem += (_, e) =>
        {
            var selected = (e.State & DrawItemState.Selected) != 0;
            using var background = new SolidBrush(selected ? Color.FromArgb(25, 73, 105) : Color.FromArgb(7, 27, 47));
            e.Graphics.FillRectangle(background, e.Bounds);
            var value = e.Index >= 0 ? combo.Items[e.Index]?.ToString() : combo.Text;
            TextRenderer.DrawText(e.Graphics, value ?? string.Empty, combo.Font, e.Bounds, DashboardTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        };
        return combo;
    }
    private static TextBox Input() => new() { Width = 320, BackColor = Color.FromArgb(7, 27, 47), ForeColor = DashboardTheme.Text, BorderStyle = BorderStyle.FixedSingle };
    private static NumericUpDown Number(int minimum, int maximum, int value) => new() { Minimum = minimum, Maximum = maximum, Value = value, Width = 180, BackColor = Color.FromArgb(7, 27, 47), ForeColor = DashboardTheme.Text, BorderStyle = BorderStyle.FixedSingle };

    private static void ConfigureSettingsGrid(DataGridView grid)
    {
        grid.BackgroundColor = DashboardTheme.Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersDefaultCellStyle.BackColor = DashboardTheme.SurfaceAlt;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = DashboardTheme.Text;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font(DashboardTheme.UiFont, 10.5f, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = DashboardTheme.Surface;
        grid.DefaultCellStyle.ForeColor = DashboardTheme.Text;
        grid.DefaultCellStyle.Font = new Font(DashboardTheme.UiFont, 10.5f);
        grid.DefaultCellStyle.Padding = new Padding(6, 3, 6, 3);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(25, 73, 105);
        grid.DefaultCellStyle.SelectionForeColor = DashboardTheme.Text;
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = true;
        grid.RowTemplate.Height = 36;
        grid.GridColor = DashboardTheme.Border;
    }

    private static Button ActionButton(string text, bool primary = false) => new()
    {
        Text = text,
        AutoSize = true,
        Height = 40,
        Padding = new Padding(18, 0, 18, 0),
        FlatStyle = FlatStyle.Flat,
        BackColor = primary ? Color.FromArgb(10, 120, 157) : DashboardTheme.SurfaceAlt,
        ForeColor = DashboardTheme.Text,
        Font = new Font(DashboardTheme.UiFont, 10.5f, FontStyle.Bold),
        FlatAppearance = { BorderColor = primary ? DashboardTheme.Cyan : DashboardTheme.Border }
    };

    private static Button EditorButton(string text, bool danger = false, bool primary = false) => new()
    {
        Text = text,
        Width = text.Length > 2 ? 80 : 60,
        Height = 34,
        FlatStyle = FlatStyle.Flat,
        BackColor = primary ? Color.FromArgb(10, 120, 157) : DashboardTheme.SurfaceAlt,
        ForeColor = danger ? DashboardTheme.Red : DashboardTheme.Text,
        Font = new Font(DashboardTheme.UiFont, 9.5f, FontStyle.Bold),
        Margin = new Padding(0, 0, 7, 0),
        FlatAppearance = { BorderColor = danger ? DashboardTheme.Red : primary ? DashboardTheme.Cyan : DashboardTheme.Border }
    };

    private static DashboardSettings Clone(DashboardSettings source) => new()
    {
        Environment = source.Environment,
        MesTestAddress = source.MesTestAddress,
        MesProductionAddress = source.MesProductionAddress,
        WmsTestAddress = source.WmsTestAddress,
        WmsProductionAddress = source.WmsProductionAddress,
        DemoMode = source.DemoMode,
        EnableInterfaceLogging = source.EnableInterfaceLogging,
        Factory = source.Factory,
        WarehouseId = source.WarehouseId,
        Resolution = source.Resolution,
        UiScalePercent = source.UiScalePercent,
        RefreshSeconds = source.RefreshSeconds,
        TimeoutSeconds = source.TimeoutSeconds,
        WmsPageSize = source.WmsPageSize,
        IssuanceTimeMinutes = source.IssuanceTimeMinutes,
        AutoScroll = source.AutoScroll,
        ScrollSeconds = source.ScrollSeconds,
        ScrollPauseSeconds = source.ScrollPauseSeconds,
        SortDirection = source.SortDirection,
        DescriptionMode = source.DescriptionMode,
        Lines = source.Lines.Select(x => new LineSetting { Line = x.Line, Uph = x.Uph }).ToList(),
        ExcludedLocations = source.ExcludedLocations.ToList()
    };
}
