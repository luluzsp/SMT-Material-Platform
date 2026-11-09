namespace SmtMaterialDashboard.Core;

public sealed class DashboardDataService
{
    private readonly IDashboardApiClient api;
    private readonly Func<DateTime> nowProvider;

    public DashboardDataService(IDashboardApiClient api, Func<DateTime>? nowProvider = null)
    {
        this.api = api;
        this.nowProvider = nowProvider ?? (() => DateTime.Now);
    }

    public async Task<DashboardSnapshot> LoadAsync(DashboardSettings settings, CancellationToken cancellationToken)
    {
        var shortages = await api.GetShortagesAsync(cancellationToken);
        if (shortages.Count == 0)
        {
            return new DashboardSnapshot { Rows = [], UpdatedAt = DateTime.Now, Message = "MES未返回缺料明细" };
        }

        var exclusions = settings.ExcludedLocations.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inventoryTasks = shortages.Select(x => x.Material).Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x, x => api.GetInventoryAsync(settings.Factory, settings.WarehouseId, x, settings.WmsPageSize, cancellationToken), StringComparer.OrdinalIgnoreCase);
        await Task.WhenAll(inventoryTasks.Values);

        var lineUph = settings.Lines.ToDictionary(x => x.Line, x => x.Uph, StringComparer.OrdinalIgnoreCase);
        var rows = new List<DashboardRow>(shortages.Count);
        foreach (var shortage in shortages)
        {
            var allInventory = inventoryTasks.TryGetValue(shortage.Material, out var inventoryTask)
                ? inventoryTask.Result.ToList()
                : [];
            var inventory = allInventory.Where(x => !exclusions.Contains(x.StoreCode)).ToList();
            var locations = inventory.Select(x => x.StoreCode).Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase);
            var available = inventory.Sum(x => x.AvailableQuantity);
            var description = allInventory.Select(x => x.Description).FirstOrDefault(x => x.Length > 0) ?? string.Empty;
            var demand = shortage.DemandQuantity;
            lineUph.TryGetValue(shortage.Line, out var uph);

            var status = DetermineStatus(shortage, available, demand, settings.IssuanceTimeMinutes, nowProvider());

            rows.Add(new DashboardRow
            {
                Id = shortage.Id,
                ShopOrder = shortage.ShopOrder,
                Line = shortage.Line,
                Uph = uph,
                Location = shortage.Location,
                Material = shortage.Material,
                Description = description,
                ShortageDid = shortage.OriginalDid,
                CallTime = shortage.CallTime,
                DemandQuantity = demand,
                StoreLocations = string.Join(",", locations),
                AvailableStock = available,
                Status = status
            });
        }

        return new DashboardSnapshot
        {
            Rows = Sort(rows, settings).ToList(),
            UpdatedAt = DateTime.Now,
            Message = $"已加载 {rows.Count} 条缺料记录"
        };
    }

    public static IEnumerable<DashboardRow> Sort(IEnumerable<DashboardRow> rows, DashboardSettings settings)
    {
        IOrderedEnumerable<DashboardRow> ordered = settings.SortDirection == SortDirection.Descending
            ? rows.OrderByDescending(x => x.CallTime)
            : rows.OrderBy(x => x.CallTime);
        return ordered.ThenBy(x => x.Id);
    }

    public static FeedStatus DetermineStatus(
        ShortageRecord shortage,
        decimal availableStock,
        decimal demandQuantity,
        int issuanceTimeMinutes,
        DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(shortage.PullDid) || shortage.PullOutTime.HasValue) return FeedStatus.Issued;
        if (shortage.CallTime != DateTime.MinValue && now > shortage.CallTime.AddMinutes(Math.Max(1, issuanceTimeMinutes)))
            return FeedStatus.TimedOut;
        return availableStock < demandQuantity ? FeedStatus.Insufficient : FeedStatus.Pending;
    }
}
