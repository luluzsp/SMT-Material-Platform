namespace SmtMaterialDashboard.Core;

public enum FeedStatus
{
    Pending,
    Issued,
    Insufficient,
    TimedOut
}

public enum SortDirection
{
    Ascending,
    Descending
}

public enum DescriptionMode
{
    SingleLine,
    TwoLines,
    Full
}

public enum SystemEnvironment
{
    Test,
    Production
}

public sealed class LineSetting
{
    public string Line { get; set; } = string.Empty;
    public decimal Uph { get; set; }
}

public sealed class DashboardSettings
{
    public SystemEnvironment Environment { get; set; } = SystemEnvironment.Production;
    public string MesTestAddress { get; set; } = "10.131.30.150:20010";
    public string MesProductionAddress { get; set; } = "10.131.30.150:20010";
    public string WmsTestAddress { get; set; } = "10.128.30.78:20018";
    public string WmsProductionAddress { get; set; } = "10.128.30.78:20018";
    public bool DemoMode { get; set; }
    public string Factory { get; set; } = "CT21";
    public string WarehouseId { get; set; } = "20117901";
    public string Resolution { get; set; } = "3840 × 2160";
    public int UiScalePercent { get; set; } = 100;
    public int RefreshSeconds { get; set; } = 30;
    public int TimeoutSeconds { get; set; } = 10;
    public int WmsPageSize { get; set; } = 200;
    public bool EnableInterfaceLogging { get; set; } = true;
    public int IssuanceTimeMinutes { get; set; } = 30;
    public bool AutoScroll { get; set; }
    public int ScrollSeconds { get; set; } = 3;
    public int ScrollPauseSeconds { get; set; } = 5;
    public SortDirection SortDirection { get; set; } = SortDirection.Ascending;
    public DescriptionMode DescriptionMode { get; set; } = DescriptionMode.TwoLines;
    public List<LineSetting> Lines { get; set; } =
    [
        new() { Line = "H5-2F-S02", Uph = 1200 },
        new() { Line = "H5-2F-S03", Uph = 980 }
    ];
    public List<string> ExcludedLocations { get; set; } = [];

    public string ActiveMesAddress => Environment == SystemEnvironment.Test ? MesTestAddress : MesProductionAddress;
    public string ActiveWmsAddress => Environment == SystemEnvironment.Test ? WmsTestAddress : WmsProductionAddress;
}

public sealed class ShortageRecord
{
    public int Id { get; init; }
    public string ShopOrder { get; init; } = string.Empty;
    public string Line { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public string OriginalDid { get; init; } = string.Empty;
    public DateTime CallTime { get; init; }
    public decimal DemandQuantity { get; init; }
    public string PullDid { get; init; } = string.Empty;
    public DateTime? PullOutTime { get; init; }
}

public sealed class InventoryRecord
{
    public string Material { get; init; } = string.Empty;
    public string StoreCode { get; init; } = string.Empty;
    public string StoreLocation { get; init; } = string.Empty;
    public decimal AvailableQuantity { get; init; }
    public string Description { get; init; } = string.Empty;
}

public sealed class DashboardRow
{
    public int Id { get; init; }
    public string ShopOrder { get; init; } = string.Empty;
    public string Line { get; init; } = string.Empty;
    public decimal Uph { get; init; }
    public string Location { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ShortageDid { get; init; } = string.Empty;
    public DateTime CallTime { get; init; }
    public decimal DemandQuantity { get; init; }
    public string StoreLocations { get; init; } = string.Empty;
    public decimal AvailableStock { get; init; }
    public FeedStatus Status { get; init; }
}

public sealed class DashboardSnapshot
{
    public required IReadOnlyList<DashboardRow> Rows { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string Message { get; init; } = string.Empty;
}
