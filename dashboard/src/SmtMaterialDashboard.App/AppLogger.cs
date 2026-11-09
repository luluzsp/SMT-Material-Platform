using System.Text;

namespace SmtMaterialDashboard.App;

internal static class AppLogger
{
    private static readonly object Sync = new();

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SmtMaterialDashboard",
        "logs");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception exception) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                var path = Path.Combine(LogDirectory, $"dashboard-{DateTime.Now:yyyyMMdd}.log");
                var text = new StringBuilder()
                    .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                    .Append(" [").Append(level).Append("] ").AppendLine(message);
                if (exception is not null) text.AppendLine(exception.ToString());
                File.AppendAllText(path, text.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never stop dashboard operation.
        }
    }
}
