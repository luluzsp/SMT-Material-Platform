using SmtMaterialDashboard.Core;

namespace SmtMaterialDashboard.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, eventArgs) =>
        {
            AppLogger.Error("未处理的界面异常", eventArgs.Exception);
            MessageBox.Show($"软件发生异常：\r\n{eventArgs.Exception.Message}\r\n\r\n日志目录：\r\n{AppLogger.LogDirectory}", "系统异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception) AppLogger.Error("未处理的系统异常", exception);
        };
        var demoMode = args.Any(x => x.Equals("--demo", StringComparison.OrdinalIgnoreCase));
        string? screenshotPath = null;
        var screenshotIndex = Array.FindIndex(args, x => x.Equals("--screenshot", StringComparison.OrdinalIgnoreCase));
        if (screenshotIndex >= 0 && screenshotIndex + 1 < args.Length) screenshotPath = args[screenshotIndex + 1];
        var settingsScreenshotIndex = Array.FindIndex(args, x => x.Equals("--settings-screenshot", StringComparison.OrdinalIgnoreCase));
        if (settingsScreenshotIndex >= 0 && settingsScreenshotIndex + 1 < args.Length)
        {
            var path = args[settingsScreenshotIndex + 1];
            var tabIndex = 0;
            var tabArgumentIndex = Array.FindIndex(args, x => x.Equals("--settings-tab", StringComparison.OrdinalIgnoreCase));
            if (tabArgumentIndex >= 0 && tabArgumentIndex + 1 < args.Length) int.TryParse(args[tabArgumentIndex + 1], out tabIndex);
            var form = new SettingsForm(new SettingsStore().Load(), tabIndex);
            form.Shown += async (_, _) =>
            {
                await Task.Delay(200);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                PrepareForSettingsScreenshot(form);
                using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                form.Close();
            };
            Application.Run(form);
            return;
        }
        Application.Run(new DashboardForm(new SettingsStore(), demoMode, screenshotPath));
    }

    private static void PrepareForSettingsScreenshot(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is DataGridView grid)
            {
                grid.EndEdit();
                grid.CurrentCell = null;
                grid.AllowUserToAddRows = false;
            }
            PrepareForSettingsScreenshot(control);
        }
    }
}
