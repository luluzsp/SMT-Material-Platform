namespace SmtMaterialDashboard.Core;

public interface IDashboardApiClient
{
    Task<IReadOnlyList<ShortageRecord>> GetShortagesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryRecord>> GetInventoryAsync(string factory, string warehouseId, string material, int pageSize, CancellationToken cancellationToken);
}
