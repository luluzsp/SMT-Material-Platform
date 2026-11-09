using SmtMaterialDashboard.Core;

namespace SmtMaterialDashboard.App;

public sealed class DashboardForm : Form
{
    private readonly SettingsStore _settingsStore;
    private readonly bool _forceDemoMode;
    private readonly string? _screenshotPath;
    private readonly CancellationTokenSource _closing = new();
    private readonly Label _clockLabel = new();
    private readonly Label _dateLabel = new();
    private readonly Label _connectionLabel = new();
    private readonly Label _subtitleLabel = new();
    private readonly Label _footerLeft = new();
    private readonly Label _footerRight = new();
    private readonly DataGridView _grid = new();
    private readonly MetricCard _pendingCard = new() { Caption = "待发料", Glyph = "◷", Accent = DashboardTheme.Amber };
    private readonly MetricCard _issuedCard = new() { Caption = "已发料", Glyph = "✓", Accent = DashboardTheme.Green };
    private readonly MetricCard _insufficientCard = new() { Caption = "库存不足", Glyph = "!", Accent = DashboardTheme.Red };
    private readonly MetricCard _timedOutCard = new() { Caption = "发料超时", Glyph = "⌛", Accent = DashboardTheme.Red };
    private readonly System.Windows.Forms.Timer _clockTimer = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly System.Windows.Forms.Timer _scrollTimer = new();
    private DashboardSettings _settings;
    private bool _refreshing;
    private bool _fullScreen;
    private bool _scrollPaused;
    private int _scrollBottomPauseTicks;
    private FormBorderStyle _savedBorderStyle;
    private FormWindowState _savedWindowState;
    private Rectangle _savedBounds;
    private float _lastAppliedScale;

    public DashboardForm(SettingsStore settingsStore, bool demoMode, string? screenshotPath = null)
    {
        _settingsStore = settingsStore;
        _forceDemoMode = demoMode;
        _screenshotPath = screenshotPath;
        _settings = settingsStore.Load();
        Text = "SMT物料缺料监控看板";
        BackColor = DashboardTheme.Background;
        ForeColor = DashboardTheme.Text;
        Font = new Font(DashboardTheme.UiFont, 10f);
        MinimumSize = new Size(1200, 720);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = screenshotPath is null ? FormWindowState.Maximized : FormWindowState.Normal;
        if (screenshotPath is not null) Size = new Size(1600, 900);
        KeyPreview = true;

        BuildUi();
        ConfigureTimers();
        ApplyDisplaySettings(true);
        Resize += (_, _) => ApplyDisplaySettings();

        Shown += async (_, _) =>
        {
            await RefreshDataAsync();
            if (_screenshotPath is not null) await CaptureAndCloseAsync(_screenshotPath);
        };
        FormClosing += (_, _) => _closing.Cancel();
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F11) ToggleFullScreen();
            if (e.KeyCode == Keys.Escape && _fullScreen) ToggleFullScreen();
        };
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = DashboardTheme.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        var header = BuildHeader();
        var toolbar = BuildToolbar();
        var summary = BuildSummary();
        var grid = BuildGridSection();
        var footer = BuildFooter();
        foreach (var control in new[] { header, toolbar, summary, grid, footer })
        {
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0);
        }
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(toolbar, 0, 1);
        root.Controls.Add(summary, 0, 2);
        root.Controls.Add(grid, 0, 3);
        root.Controls.Add(footer, 0, 4);
        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 82, Padding = new Padding(24, 10, 24, 8) };
        header.Paint += (_, e) =>
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(header.ClientRectangle,
                DashboardTheme.HeaderStart, DashboardTheme.HeaderEnd, 0f);
            e.Graphics.FillRectangle(brush, header.ClientRectangle);
        };
        var title = new Label
        {
            AutoSize = true,
            Text = "SMT 物料缺料监控看板",
            ForeColor = DashboardTheme.Text,
            BackColor = Color.Transparent,
            Font = new Font(DashboardTheme.UiFont, 22, FontStyle.Bold),
            Location = new Point(24, 12)
        };
        _subtitleLabel.AutoSize = true;
        _subtitleLabel.Text = BuildSubtitle();
        _subtitleLabel.ForeColor = DashboardTheme.Muted;
        _subtitleLabel.BackColor = Color.Transparent;
        _subtitleLabel.Location = new Point(27, 52);
        var subtitle = _subtitleLabel;
        _clockLabel.AutoSize = false;
        _clockLabel.Width = 220;
        _clockLabel.Height = 36;
        _clockLabel.TextAlign = ContentAlignment.MiddleRight;
        _clockLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _clockLabel.Font = new Font(DashboardTheme.UiFont, 20, FontStyle.Bold);
        _clockLabel.ForeColor = DashboardTheme.Text;
        _clockLabel.BackColor = Color.Transparent;
        _clockLabel.Location = new Point(header.Width - 244, 8);
        _dateLabel.AutoSize = false;
        _dateLabel.Width = 280;
        _dateLabel.Height = 24;
        _dateLabel.TextAlign = ContentAlignment.MiddleRight;
        _dateLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _dateLabel.ForeColor = DashboardTheme.Muted;
        _dateLabel.BackColor = Color.Transparent;
        _dateLabel.Location = new Point(header.Width - 304, 49);
        header.Resize += (_, _) =>
        {
            _clockLabel.Left = header.ClientSize.Width - _clockLabel.Width - 24;
            _dateLabel.Left = header.ClientSize.Width - _dateLabel.Width - 24;
        };
        header.Controls.AddRange([title, subtitle, _clockLabel, _dateLabel]);
        UpdateClock();
        return header;
    }

    private Control BuildToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(18, 10, 18, 8),
            BackColor = Color.FromArgb(10, 34, 59),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        var dashboard = CreateButton("▦  实时看板", true);
        dashboard.Enabled = false;
        var settings = CreateButton("⚙  设置");
        settings.Click += (_, _) => OpenSettings();

        _connectionLabel.AutoSize = false;
        _connectionLabel.Width = 320;
        _connectionLabel.Height = 34;
        _connectionLabel.TextAlign = ContentAlignment.MiddleRight;
        _connectionLabel.ForeColor = DashboardTheme.Muted;
        _connectionLabel.Margin = new Padding(0);

        var fullScreen = CreateButton("⛶  全屏显示");
        fullScreen.Click += (_, _) => ToggleFullScreen();
        var refresh = CreateButton("↻  立即刷新", true);
        refresh.Click += async (_, _) => await RefreshDataAsync();
        toolbar.Controls.AddRange([dashboard, settings]);
        toolbar.Controls.Add(new Panel { Width = 20, Height = 1 });
        toolbar.Controls.Add(_connectionLabel);
        toolbar.Controls.Add(fullScreen);
        toolbar.Controls.Add(refresh);
        toolbar.Resize += (_, _) =>
        {
            var fixedWidth = toolbar.Controls.Cast<Control>().Where(x => x != _connectionLabel).Sum(x => x.Width + x.Margin.Horizontal);
            _connectionLabel.Width = Math.Max(160, toolbar.ClientSize.Width - fixedWidth - toolbar.Padding.Horizontal - 12);
        };
        return toolbar;
    }

    private Control BuildSummary()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 124,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = DashboardTheme.Background,
            ColumnCount = 4,
            RowCount = 1
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        _pendingCard.Dock = DockStyle.Fill;
        _issuedCard.Dock = DockStyle.Fill;
        _insufficientCard.Dock = DockStyle.Fill;
        _timedOutCard.Dock = DockStyle.Fill;
        table.Controls.Add(_pendingCard, 0, 0);
        table.Controls.Add(_issuedCard, 1, 0);
        table.Controls.Add(_insufficientCard, 2, 0);
        table.Controls.Add(_timedOutCard, 3, 0);
        return table;
    }

    private Control BuildGridSection()
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 0, 18, 8), BackColor = DashboardTheme.Background };
        ConfigureGrid();
        host.Controls.Add(_grid);
        return host;
    }

    private Control BuildFooter()
    {
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.FromArgb(8, 29, 50), Padding = new Padding(18, 0, 18, 0) };
        _footerLeft.Dock = DockStyle.Left;
        _footerLeft.AutoSize = true;
        _footerLeft.TextAlign = ContentAlignment.MiddleLeft;
        _footerLeft.ForeColor = DashboardTheme.Muted;
        _footerRight.Dock = DockStyle.Right;
        _footerRight.AutoSize = true;
        _footerRight.TextAlign = ContentAlignment.MiddleRight;
        _footerRight.ForeColor = DashboardTheme.Muted;
        footer.Controls.AddRange([_footerLeft, _footerRight]);
        return footer;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = DashboardTheme.Background;
        _grid.BorderStyle = BorderStyle.None;
        _grid.GridColor = DashboardTheme.Border;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(9, 31, 52);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = DashboardTheme.Text;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font(DashboardTheme.UiFont, 11.5f, FontStyle.Bold);
        _grid.ColumnHeadersHeight = 48;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.ScrollBars = ScrollBars.Both;
        _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.DefaultCellStyle.BackColor = DashboardTheme.Surface;
        _grid.DefaultCellStyle.ForeColor = DashboardTheme.Text;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(25, 73, 105);
        _grid.DefaultCellStyle.SelectionForeColor = DashboardTheme.Text;
        _grid.DefaultCellStyle.Font = new Font(DashboardTheme.UiFont, 12f, FontStyle.Bold);
        _grid.DefaultCellStyle.Padding = new Padding(5, 3, 5, 3);
        _grid.RowTemplate.Height = 68;
        _grid.ShowCellToolTips = true;
        _grid.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                e.ToolTipText = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
        };
        _grid.MouseEnter += (_, _) => _scrollPaused = true;
        _grid.MouseLeave += (_, _) => _scrollPaused = false;

        AddColumn("工单", 90, 13);
        AddColumn("线体", 70, 11.5f);
        AddColumn("UPH", 45, 6);
        AddColumn("站位", 48, 6);
        AddColumn("料号", 90, 13);
        AddColumn("物料描述", 120, 15.5f);
        AddColumn("缺料 DID", 105, 14.5f);
        AddColumn("叫料时间", 82, 11);
        AddColumn("需求数量", 75, 9.5f);
        AddColumn("物料库位", 135, 16);
        AddColumn("可用库存", 85, 9.5f, 100);
        AddColumn("发料状态", 95, 10.5f, 110);
    }

    private void AddColumn(string header, int minimumWidth, float fillWeight, int? fixedWidth = null)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            MinimumWidth = minimumWidth,
            FillWeight = fillWeight,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            Resizable = DataGridViewTriState.True
        };
        if (fixedWidth.HasValue)
        {
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = fixedWidth.Value;
        }
        _grid.Columns.Add(column);
    }

    private static Button CreateButton(string text, bool primary = false)
    {
        return new Button
        {
            AutoSize = true,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            Text = text,
            BackColor = primary ? Color.FromArgb(10, 120, 157) : DashboardTheme.SurfaceAlt,
            ForeColor = DashboardTheme.Text,
            Font = new Font(DashboardTheme.UiFont, 10),
            Padding = new Padding(10, 0, 10, 0),
            Margin = new Padding(0, 0, 9, 0),
            FlatAppearance = { BorderColor = primary ? DashboardTheme.Cyan : DashboardTheme.Border, BorderSize = 1 }
        };
    }

    private void ConfigureTimers()
    {
        _clockTimer.Interval = 1000;
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        _refreshTimer.Tick += async (_, _) => await RefreshDataAsync();
        _scrollTimer.Tick += (_, _) => ScrollOneRow();
        ResetTimers();
    }

    private void ResetTimers()
    {
        _refreshTimer.Stop();
        _refreshTimer.Interval = Math.Max(5000, _settings.RefreshSeconds * 1000);
        _refreshTimer.Start();
        _scrollTimer.Stop();
        _scrollTimer.Interval = Math.Max(1000, _settings.ScrollSeconds * 1000);
        if (_settings.AutoScroll) _scrollTimer.Start();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        _clockLabel.Text = now.ToString("HH:mm:ss");
        _dateLabel.Text = now.ToString("yyyy-MM-dd  dddd", new System.Globalization.CultureInfo("zh-CN"));
    }

    private async Task RefreshDataAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        var effectiveDemoMode = _forceDemoMode || _settings.DemoMode;
        _connectionLabel.Text = effectiveDemoMode ? "● 正在加载演示数据…" : "● 正在更新 MES / WMS 数据…";
        _connectionLabel.ForeColor = DashboardTheme.Cyan;
        try
        {
            IDashboardApiClient api;
            HttpClient? http = null;
            if (effectiveDemoMode)
            {
                api = new DemoDashboardApiClient();
            }
            else
            {
                http = new HttpClient { Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds) };
                Action<string>? trace = _settings.EnableInterfaceLogging ? AppLogger.Info : null;
                api = new DashboardApiClient(http, _settings.ActiveMesAddress, _settings.ActiveWmsAddress, trace);
            }

            try
            {
                var snapshot = await new DashboardDataService(api).LoadAsync(_settings, _closing.Token);
                BindRows(snapshot.Rows);
                UpdateMetrics(snapshot.Rows);
                _connectionLabel.Text = effectiveDemoMode ? "● 演示数据已加载（接口未调用）" : "● MES / WMS 数据正常";
                _connectionLabel.ForeColor = DashboardTheme.Green;
                _footerRight.Text = $"最后更新：{snapshot.UpdatedAt:yyyy-MM-dd HH:mm:ss}";
            }
            finally
            {
                http?.Dispose();
            }
        }
        catch (OperationCanceledException) when (_closing.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _connectionLabel.Text = "● 数据更新异常";
            _connectionLabel.ForeColor = DashboardTheme.Red;
            _footerRight.Text = $"{DateTime.Now:HH:mm:ss}  {exception.Message}";
            AppLogger.Error("MES/WMS 数据刷新失败", exception);
            var message = exception is TaskCanceledException or TimeoutException
                ? $"MES/WMS 接口请求超时（当前超时设置：{_settings.TimeoutSeconds} 秒）。\r\n请检查网络、接口 IP 与运行环境配置。"
                : $"MES/WMS 数据获取失败：\r\n{exception.Message}";
            MessageBox.Show(this, $"{message}\r\n\r\n错误详情已记录到：\r\n{AppLogger.LogDirectory}", "接口异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void BindRows(IReadOnlyList<DashboardRow> rows)
    {
        _grid.SuspendLayout();
        _grid.Rows.Clear();
        foreach (var row in rows)
        {
            var status = row.Status switch
            {
                FeedStatus.Issued => "已发料",
                FeedStatus.Insufficient => "库存不足",
                FeedStatus.TimedOut => "发料超时",
                _ => "待发料"
            };
            var index = _grid.Rows.Add(
                row.ShopOrder,
                row.Line,
                row.Uph > 0 ? row.Uph.ToString("0.##") : string.Empty,
                row.Location,
                row.Material,
                row.Description,
                row.ShortageDid,
                row.CallTime == DateTime.MinValue ? string.Empty : row.CallTime.ToString("MM-dd HH:mm"),
                row.DemandQuantity.ToString("0.##"),
                row.StoreLocations,
                row.AvailableStock.ToString("0.##"),
                status);
            var gridRow = _grid.Rows[index];
            gridRow.Tag = row;
            gridRow.DefaultCellStyle.BackColor = DashboardTheme.Surface;
            if (row.Status is FeedStatus.Insufficient or FeedStatus.TimedOut)
            {
                gridRow.DefaultCellStyle.ForeColor = DashboardTheme.Red;
                gridRow.DefaultCellStyle.SelectionForeColor = DashboardTheme.Red;
            }
            gridRow.Cells[11].Style.ForeColor = row.Status is FeedStatus.Insufficient or FeedStatus.TimedOut ? DashboardTheme.Red : DashboardTheme.Text;
            gridRow.Cells[11].Style.BackColor = row.Status switch
            {
                FeedStatus.Issued => Color.FromArgb(42, DashboardTheme.Green),
                FeedStatus.Insufficient or FeedStatus.TimedOut => Color.FromArgb(42, DashboardTheme.Red),
                _ => Color.FromArgb(42, DashboardTheme.Amber)
            };
        }
        ApplyDescriptionMode();
        _grid.ResumeLayout();
        _footerLeft.Text = $"按叫料时间{(_settings.SortDirection == SortDirection.Ascending ? "升序" : "降序")}排列 · 发料时限 {_settings.IssuanceTimeMinutes} 分钟 · 已排除 {_settings.ExcludedLocations.Count} 个库存库位";
    }

    private void UpdateMetrics(IReadOnlyList<DashboardRow> rows)
    {
        _pendingCard.Value = rows.Count(x => x.Status == FeedStatus.Pending).ToString();
        _issuedCard.Value = rows.Count(x => x.Status == FeedStatus.Issued).ToString();
        _insufficientCard.Value = rows.Count(x => x.Status == FeedStatus.Insufficient).ToString();
        _timedOutCard.Value = rows.Count(x => x.Status == FeedStatus.TimedOut).ToString();
    }

    private void ApplyDescriptionMode()
    {
        var descriptionStyle = _grid.Columns[5].DefaultCellStyle;
        switch (_settings.DescriptionMode)
        {
            case DescriptionMode.SingleLine:
                _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
                descriptionStyle.WrapMode = DataGridViewTriState.False;
                foreach (DataGridViewRow row in _grid.Rows) row.Height = Scale(52);
                break;
            case DescriptionMode.Full:
                descriptionStyle.WrapMode = DataGridViewTriState.True;
                _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders;
                break;
            default:
                _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
                descriptionStyle.WrapMode = DataGridViewTriState.True;
                foreach (DataGridViewRow row in _grid.Rows) row.Height = Scale(72);
                break;
        }
    }

    private void ScrollOneRow()
    {
        if (!_settings.AutoScroll || _scrollPaused || _grid.Rows.Count == 0) return;
        try
        {
            var first = _grid.FirstDisplayedScrollingRowIndex;
            if (first < 0) first = 0;
            var displayed = Math.Max(1, _grid.DisplayedRowCount(false));
            if (first + displayed >= _grid.Rows.Count)
            {
                _scrollBottomPauseTicks++;
                var requiredTicks = Math.Max(1, _settings.ScrollPauseSeconds / Math.Max(1, _settings.ScrollSeconds));
                if (_scrollBottomPauseTicks >= requiredTicks)
                {
                    _grid.FirstDisplayedScrollingRowIndex = 0;
                    _scrollBottomPauseTicks = 0;
                }
            }
            else
            {
                _grid.FirstDisplayedScrollingRowIndex = first + 1;
                _scrollBottomPauseTicks = 0;
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _settings = dialog.Settings;
        _settingsStore.Save(_settings);
        _subtitleLabel.Text = BuildSubtitle();
        ApplyDisplaySettings(true);
        ResetTimers();
        _ = RefreshDataAsync();
    }

    private void ApplyDisplaySettings(bool force = false)
    {
        var preset = _settings.Resolution switch
        {
            "3840 × 2160" => 1.30f,
            "2560 × 1440" => 1.12f,
            "1600 × 900" => .92f,
            "1366 × 768" => .84f,
            "自动适配" => Math.Clamp(Screen.FromControl(this).Bounds.Width / 1920f, .84f, 1.45f),
            _ => 1f
        };
        var viewportScale = ClientSize.Width switch
        {
            >= 3200 => 1.30f,
            >= 2400 => 1.15f,
            >= 1800 => 1f,
            >= 1500 => .92f,
            _ => .84f
        };
        var scale = Math.Min(preset, viewportScale) * _settings.UiScalePercent / 100f;
        if (!force && Math.Abs(scale - _lastAppliedScale) < .01f) return;
        _lastAppliedScale = scale;
        Font = new Font(DashboardTheme.UiFont, Math.Clamp(10f * scale, 8.5f, 18f));
        _grid.DefaultCellStyle.Font = new Font(DashboardTheme.UiFont, Math.Clamp(12f * scale, 10.5f, 20f), FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font(DashboardTheme.UiFont, Math.Clamp(11.5f * scale, 10f, 19f), FontStyle.Bold);
        _grid.ColumnHeadersHeight = Scale(48);
        ApplyDescriptionMode();
        _grid.Invalidate();
    }

    private int Scale(int value) => (int)Math.Round(value * Math.Clamp(Font.Size / 10f, .8f, 1.8f));

    private string BuildSubtitle()
    {
        if (_forceDemoMode || _settings.DemoMode) return "四部成都汽车电子 · 发料仓库协同 · 演示数据（接口已停用）";
        var environment = _settings.Environment == SystemEnvironment.Test ? "测试环境" : "生产环境";
        return $"四部成都汽车电子 · 发料仓库协同 · {environment}";
    }

    private void ToggleFullScreen()
    {
        if (!_fullScreen)
        {
            _savedBorderStyle = FormBorderStyle;
            _savedWindowState = WindowState;
            _savedBounds = Bounds;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            TopMost = true;
            _fullScreen = true;
        }
        else
        {
            TopMost = false;
            FormBorderStyle = _savedBorderStyle;
            Bounds = _savedBounds;
            WindowState = _savedWindowState;
            _fullScreen = false;
        }
    }

    private async Task CaptureAndCloseAsync(string path)
    {
        await Task.Delay(250);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height);
        DrawToBitmap(bitmap, new Rectangle(Point.Empty, ClientSize));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        Close();
    }
}
