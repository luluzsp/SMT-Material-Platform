using SmtMaterialDashboard.Core;
using System.Net;
using System.Text;

var tests = new DashboardTests();
await tests.RunAsync();
Console.WriteLine("All dashboard tests passed.");

internal sealed class DashboardTests
{
    public async Task RunAsync()
    {
        await AggregatesAndFiltersInventoryAsync();
        await CalculatesStatusAsync();
        CalculatesIssuanceTimeout();
        SortsByCallTimeByDefault();
        PersistsSettings();
        BuildsConfiguredEndpoints();
        await LogsInterfaceRequestAndResponseAsync();
    }

    private static async Task AggregatesAndFiltersInventoryAsync()
    {
        var api = new FakeApi();
        var settings = Settings();
        settings.ExcludedLocations = ["EXCLUDE-CODE"];
        var snapshot = await new DashboardDataService(api).LoadAsync(settings, CancellationToken.None);
        var row = snapshot.Rows.Single(x => x.Material == "MAT-1");
        Equal(200, api.LastInventoryPageSize, "default WMS page size must be passed to getPage");
        Equal("NORMAL-CODE", row.StoreLocations, "dashboard material location must display the non-excluded storeCode");
        Equal(220m, row.AvailableStock, "excluded location quantity must not be counted");
        Equal(200m, row.DemandQuantity, "demand quantity comes directly from MES DEMAND_QTY");
    }

    private static async Task CalculatesStatusAsync()
    {
        var snapshot = await new DashboardDataService(new FakeApi(), () => new DateTime(2026, 9, 20, 8, 20, 0)).LoadAsync(Settings(), CancellationToken.None);
        var first = snapshot.Rows.Single(x => x.Material == "MAT-1");
        var second = snapshot.Rows.Single(x => x.Material == "MAT-2");
        var issued = snapshot.Rows.Single(x => x.Material == "MAT-3");
        Equal(FeedStatus.Pending, first.Status, "stock covering demand is pending");
        Equal(FeedStatus.Insufficient, second.Status, "stock below demand is insufficient");
        Equal(FeedStatus.Issued, issued.Status, "pull DID takes precedence over stock");
    }

    private static void CalculatesIssuanceTimeout()
    {
        var shortage = new ShortageRecord { CallTime = new DateTime(2026, 9, 20, 8, 0, 0) };
        Equal(FeedStatus.Pending,
            DashboardDataService.DetermineStatus(shortage, 100, 50, 30, new DateTime(2026, 9, 20, 8, 30, 0)),
            "deadline itself is not timed out");
        Equal(FeedStatus.TimedOut,
            DashboardDataService.DetermineStatus(shortage, 0, 50, 30, new DateTime(2026, 9, 20, 8, 31, 0)),
            "current time after deadline is timed out even when stock is insufficient");
        var issued = new ShortageRecord { CallTime = shortage.CallTime, PullDid = "PACK-1" };
        Equal(FeedStatus.Issued,
            DashboardDataService.DetermineStatus(issued, 0, 50, 30, new DateTime(2026, 9, 20, 9, 0, 0)),
            "issued status has highest priority");
    }

    private static void SortsByCallTimeByDefault()
    {
        var settings = Settings();
        var rows = new[]
        {
            new DashboardRow { Id = 2, CallTime = new DateTime(2026, 9, 20, 9, 0, 0) },
            new DashboardRow { Id = 1, CallTime = new DateTime(2026, 9, 20, 8, 0, 0) }
        };
        Equal(1, DashboardDataService.Sort(rows, settings).First().Id, "default sort is call time ascending");
    }

    private static void PersistsSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), "smt-dashboard-tests", Guid.NewGuid().ToString("N"));
        var file = Path.Combine(directory, "settings.json");
        var store = new SettingsStore(file);
        var settings = Settings();
        settings.ExcludedLocations = [" 2AB1 ", "2ab1", "QK-LOCK"];
        settings.Environment = SystemEnvironment.Test;
        settings.DemoMode = true;
        settings.MesTestAddress = "10.10.1.20:8080";
        settings.IssuanceTimeMinutes = 45;
        settings.WmsPageSize = 350;
        settings.EnableInterfaceLogging = false;
        store.Save(settings);
        var loaded = store.Load();
        Equal(2, loaded.ExcludedLocations.Count, "settings normalize duplicate locations");
        Equal(SystemEnvironment.Test, loaded.Environment, "selected environment is persisted");
        Equal(true, loaded.DemoMode, "demo mode is persisted");
        Equal("10.10.1.20:8080", loaded.ActiveMesAddress, "active MES address follows selected environment");
        Equal(45, loaded.IssuanceTimeMinutes, "issuance timeout is persisted");
        Equal(350, loaded.WmsPageSize, "WMS page size is persisted");
        Equal(false, loaded.EnableInterfaceLogging, "interface logging switch is persisted");
        Directory.Delete(directory, true);
    }

    private static void BuildsConfiguredEndpoints()
    {
        Equal("http://10.10.1.20:8080/MES2/Service.action",
            DashboardApiClient.BuildEndpoint("10.10.1.20:8080", "fallback", "/MES2/Service.action"),
            "IP and port get the default MES path");
        Equal("https://mes.example.local/custom/service",
            DashboardApiClient.BuildEndpoint("https://mes.example.local/custom/service", "fallback", "/MES2/Service.action"),
            "full endpoint URL is preserved");
        Equal("http://10.10.1.20:8080/wms3.0/ms/exi/wmsMaterialStore/getPage",
            DashboardApiClient.BuildWmsEndpoint("10.10.1.20:8080", "fallback", "/wms3.0/ms/exi/wmsMaterialStore/getPage"),
            "WMS inventory endpoint uses ms/exi route");
        Equal("http://10.10.1.20:8080/wms3.0/wms/exi/wmsMaterialStore/getListByPackingNum",
            DashboardApiClient.BuildWmsEndpoint("http://10.10.1.20:8080/old/path/getPage", "fallback", "/wms3.0/wms/exi/wmsMaterialStore/getListByPackingNum"),
            "full WMS URL contributes only its authority and cannot duplicate endpoint paths");
    }

    private static async Task LogsInterfaceRequestAndResponseAsync()
    {
        var traces = new List<string>();
        string? inventoryRequestBody = null;
        using var http = new HttpClient(new StubHttpHandler(request =>
        {
            inventoryRequestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"DATA\":{\"page\":[{\"mergeBpn\":\"MAT-1\",\"storeCode\":\"SLQKW\",\"storeLocation\":\"00ZJ\",\"warehouseAvailableQty\":2}]},\"RESULT\":\"PASS\"}", Encoding.UTF8, "application/json")
            };
        }));
        var client = new DashboardApiClient(http, "10.10.1.10:8080", "10.10.1.20:8080", traces.Add);
        var inventory = await client.GetInventoryAsync("CT21", "20117901", "MAT-1", 321, CancellationToken.None);
        Equal("SLQKW", inventory.Single().StoreCode, "getPage storeCode must be mapped for exclusion");
        Equal("00ZJ", inventory.Single().StoreLocation, "storeLocation remains the displayed material location");
        if (inventoryRequestBody is null ||
            !inventoryRequestBody.Contains("\"storeStatus\":\"已上架\"", StringComparison.Ordinal) ||
            !inventoryRequestBody.Contains("\"pageSize\":321", StringComparison.Ordinal))
            throw new InvalidOperationException($"WMS inventory request body is incorrect: {inventoryRequestBody}");

        var trace = traces.Single();
        foreach (var expected in new[]
        {
            "系统：WMS", "操作：WMS库存查询", "请求时间：", "响应时间：", "耗时：",
            "请求方式：POST", "请求URL：http://10.10.1.20:8080/wms3.0/ms/exi/wmsMaterialStore/getPage",
            "\"factory\":\"CT21\"", "\"warehouseId\":\"20117901\"", "\"mergeBpn\":\"MAT-1\"",
            "\"storeStatus\":\"已上架\"", "\"pageSize\":321",
            "HTTP状态：200 OK", "请求结果：PASS", "响应信息："
        })
        {
            if (!trace.Contains(expected, StringComparison.Ordinal))
                throw new InvalidOperationException($"interface trace missing: {expected}");
        }

        traces.Clear();
        using var failedHttp = new HttpClient(new StubHttpHandler(request => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            ReasonPhrase = "Not Found",
            Content = new StringContent("{\"RESULT\":\"FAIL\",\"MESSAGE\":\"route missing\"}", Encoding.UTF8, "application/json")
        }));
        var failedClient = new DashboardApiClient(failedHttp, "10.10.1.10:8080", "10.10.1.20:8080", traces.Add);
        try
        {
            await failedClient.GetInventoryAsync("CT21", "20117901", "MAT-2", 200, CancellationToken.None);
            throw new InvalidOperationException("404 response should fail");
        }
        catch (DashboardApiException)
        {
        }
        var failedTrace = traces.Single();
        foreach (var expected in new[] { "HTTP状态：404 Not Found", "请求结果：FAIL", "route missing" })
        {
            if (!failedTrace.Contains(expected, StringComparison.Ordinal))
                throw new InvalidOperationException($"failed interface trace missing: {expected}");
        }
    }

    private static DashboardSettings Settings() => new()
    {
        Factory = "CT21",
        WarehouseId = "20117901",
        Lines = [new LineSetting { Line = "LINE-1", Uph = 1200 }]
    };

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected={expected}, actual={actual}");
    }

}

internal sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(responder(request));
}

internal sealed class FakeApi : IDashboardApiClient
{
    public int LastInventoryPageSize { get; private set; }

    public Task<IReadOnlyList<ShortageRecord>> GetShortagesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ShortageRecord>>(
    [
        new() { Id = 1, ShopOrder = "SO-1", Line = "LINE-1", Location = "L1", Material = "MAT-1", OriginalDid = "DID-1", CallTime = new DateTime(2026, 9, 20, 8, 0, 0), DemandQuantity = 200 },
        new() { Id = 2, ShopOrder = "SO-2", Line = "LINE-1", Location = "L2", Material = "MAT-2", OriginalDid = "DID-2", CallTime = new DateTime(2026, 9, 20, 9, 0, 0), DemandQuantity = 100 },
        new() { Id = 3, ShopOrder = "SO-3", Line = "LINE-1", Location = "L3", Material = "MAT-3", OriginalDid = "DID-3", CallTime = new DateTime(2026, 9, 20, 10, 0, 0), DemandQuantity = 200, PullDid = "PACK-3" }
    ]);

    public Task<IReadOnlyList<InventoryRecord>> GetInventoryAsync(string factory, string warehouseId, string material, int pageSize, CancellationToken cancellationToken)
    {
        LastInventoryPageSize = pageSize;
        IReadOnlyList<InventoryRecord> rows = material switch
        {
            "MAT-1" =>
            [
                new() { Material = material, StoreCode = "EXCLUDE-CODE", StoreLocation = "2AB1", AvailableQuantity = 140, Description = "Long material description" },
                new() { Material = material, StoreCode = "NORMAL-CODE", StoreLocation = "3T2Y", AvailableQuantity = 220, Description = "Long material description" }
            ],
            "MAT-2" => [new() { Material = material, StoreCode = "NORMAL-CODE", StoreLocation = "A1", AvailableQuantity = 10, Description = "Material 2" }],
            _ => []
        };
        return Task.FromResult(rows);
    }
}
