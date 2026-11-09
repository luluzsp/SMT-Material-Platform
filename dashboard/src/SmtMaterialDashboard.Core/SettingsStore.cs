using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmtMaterialDashboard.Core;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmtMaterialDashboard",
            "settings.json");
    }

    public string FilePath { get; }

    public DashboardSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new DashboardSettings();
            }

            var settings = JsonSerializer.Deserialize<DashboardSettings>(File.ReadAllText(FilePath), JsonOptions)
                ?? new DashboardSettings();
            Normalize(settings);
            return settings;
        }
        catch
        {
            return new DashboardSettings();
        }
    }

    public void Save(DashboardSettings settings)
    {
        Normalize(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temp, FilePath, true);
    }

    private static void Normalize(DashboardSettings settings)
    {
        settings.RefreshSeconds = Math.Clamp(settings.RefreshSeconds, 5, 3600);
        settings.TimeoutSeconds = Math.Clamp(settings.TimeoutSeconds, 3, 120);
        settings.WmsPageSize = Math.Clamp(settings.WmsPageSize, 1, 10000);
        settings.IssuanceTimeMinutes = Math.Clamp(settings.IssuanceTimeMinutes, 1, 1440);
        settings.ScrollSeconds = Math.Clamp(settings.ScrollSeconds, 1, 30);
        settings.ScrollPauseSeconds = Math.Clamp(settings.ScrollPauseSeconds, 1, 60);
        settings.UiScalePercent = Math.Clamp(settings.UiScalePercent, 75, 200);
        settings.Factory = settings.Factory.Trim();
        settings.WarehouseId = settings.WarehouseId.Trim();
        settings.MesTestAddress = settings.MesTestAddress.Trim();
        settings.MesProductionAddress = settings.MesProductionAddress.Trim();
        settings.WmsTestAddress = settings.WmsTestAddress.Trim();
        settings.WmsProductionAddress = settings.WmsProductionAddress.Trim();
        settings.ExcludedLocations = settings.ExcludedLocations
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.Lines = settings.Lines
            .Where(x => !string.IsNullOrWhiteSpace(x.Line))
            .GroupBy(x => x.Line.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => new LineSetting { Line = x.Key, Uph = Math.Max(0, x.Last().Uph) })
            .OrderBy(x => x.Line, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
