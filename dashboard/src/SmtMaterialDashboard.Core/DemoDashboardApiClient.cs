namespace SmtMaterialDashboard.Core;

public sealed class DemoDashboardApiClient : IDashboardApiClient
{
    private static readonly DateTime Now = DateTime.Now;

    public Task<IReadOnlyList<ShortageRecord>> GetShortagesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ShortageRecord>>(
        [
            new() { Id = 1, ShopOrder = "5030059140T", Line = "H5-2F-S02", Location = "12-4E", Material = "13247855-00", OriginalDid = "CT2126072800001ADA1C1", CallTime = Now.AddMinutes(-60), DemandQuantity = 378 },
            new() { Id = 2, ShopOrder = "4311563736T", Line = "H5-2F-S02", Location = "12-11", Material = "14382059-00", OriginalDid = "126073000644561", CallTime = Now.AddMinutes(-10), DemandQuantity = 10000 },
            new() { Id = 3, ShopOrder = "5030059218T", Line = "H5-2F-S03", Location = "9-11", Material = "14569495-00", OriginalDid = "CT06260829529001", CallTime = Now.AddMinutes(-5), DemandQuantity = 200 },
            new() { Id = 4, ShopOrder = "5030059251T", Line = "H5-2F-S03", Location = "7-3A", Material = "21536845-00", OriginalDid = "CT06260829529002", CallTime = Now.AddMinutes(-20), DemandQuantity = 200, PullDid = "CT06260829529002", PullOutTime = Now.AddMinutes(-2) }
        ]);

    public Task<IReadOnlyList<InventoryRecord>> GetInventoryAsync(string factory, string warehouseId, string material, int pageSize, CancellationToken cancellationToken)
    {
        IReadOnlyList<InventoryRecord> result = material switch
        {
            "13247855-00" =>
            [
                new() { Material = material, StoreCode = "SLQKW", StoreLocation = "2AB1", AvailableQuantity = 140, Description = "厚膜电阻_CQ02WGF330JTCE_33Ω_±1%_1/16W_-55-155℃_0402_车规_M01014" },
                new() { Material = material, StoreCode = "NORMAL", StoreLocation = "3T2Y", AvailableQuantity = 220, Description = "厚膜电阻_CQ02WGF330JTCE_33Ω_±1%_1/16W_-55-155℃_0402_车规_M01014" }
            ],
            "14382059-00" => [new() { Material = material, StoreCode = "DSJKW", StoreLocation = "2080", AvailableQuantity = 189, Description = "多层片式陶瓷电容器_10μF_±10%_25V_X7R_0603_车规" }],
            "14569495-00" => [new() { Material = material, StoreCode = "SMTCK", StoreLocation = "2002", AvailableQuantity = 420, Description = "厚膜电阻_33Ω_0402_车规" }],
            _ => [new() { Material = material, StoreCode = "SLQKW", StoreLocation = "00ZJ", AvailableQuantity = 1240, Description = "汽车PCBA_CCPB-DE2X-NORU5302(HB)_CCPB-DE2X主板_测试_V1.0" }]
        };
        return Task.FromResult(result);
    }
}
