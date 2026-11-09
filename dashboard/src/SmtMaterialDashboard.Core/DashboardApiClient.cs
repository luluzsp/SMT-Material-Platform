using System.Globalization;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SmtMaterialDashboard.Core;

public sealed class DashboardApiClient : IDashboardApiClient
{
    private const string DefaultMesAddress = "10.131.30.150:20010";
    private const string DefaultWmsAddress = "10.128.30.78:20018";
    private readonly HttpClient httpClient;
    private readonly string mesUrl;
    private readonly string wmsInventoryUrl;
    private readonly Action<string>? trace;
    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public DashboardApiClient(HttpClient httpClient)
        : this(httpClient, DefaultMesAddress, DefaultWmsAddress, null)
    {
    }

    public DashboardApiClient(HttpClient httpClient, string mesAddress, string wmsAddress, Action<string>? trace = null)
    {
        this.httpClient = httpClient;
        this.trace = trace;
        mesUrl = BuildEndpoint(mesAddress, DefaultMesAddress, "/MES2/Service.action");
        wmsInventoryUrl = BuildWmsEndpoint(wmsAddress, DefaultWmsAddress, "/wms3.0/ms/exi/wmsMaterialStore/getPage");
    }

    public async Task<IReadOnlyList<ShortageRecord>> GetShortagesAsync(CancellationToken cancellationToken)
    {
        using var document = await GetMesAsync("MatShortageInfo", new { LOGIN_ID = "-1", CLIENT_ID = "1" }, cancellationToken);
        EnsureSuccess(document.RootElement, "MES缺料信息");
        if (!TryArray(document.RootElement, "DATA", out var data)) return [];
        return data.EnumerateArray().Select(x => new ShortageRecord
        {
            Id = GetInt(x, "ID"),
            ShopOrder = GetString(x, "SHOPORDER"),
            Line = GetString(x, "LINE"),
            Location = GetString(x, "LOCATION"),
            Material = GetString(x, "MAT"),
            OriginalDid = GetString(x, "ORIGINAL_DID"),
            CallTime = GetDateTime(x, "CREATE_TIME") ?? GetDateTime(x, "SUBMITTIME") ?? DateTime.MinValue,
            DemandQuantity = GetDecimal(x, "DEMAND_QTY"),
            PullDid = GetString(x, "PULL_DID"),
            PullOutTime = GetDateTime(x, "PULL_OUT_TIME")
        }).ToList();
    }

    public async Task<IReadOnlyList<InventoryRecord>> GetInventoryAsync(
        string factory, string warehouseId, string material, int pageSize, CancellationToken cancellationToken)
    {
        var requestBody = JsonSerializer.Serialize(new
        {
            factory,
            warehouseId,
            mergeBpn = material,
            storeStatus = "已上架",
            pageSize = Math.Clamp(pageSize, 1, 10000)
        }, RequestJsonOptions);
        using var payload = await SendJsonAsync(
            "WMS", "WMS库存查询", HttpMethod.Post, wmsInventoryUrl, requestBody, cancellationToken);
        EnsureSuccess(payload.RootElement, "WMS库存查询");
        if (!payload.RootElement.TryGetProperty("DATA", out var data) || !TryArray(data, "page", out var page)) return [];
        return page.EnumerateArray().Select(x => new InventoryRecord
        {
            Material = GetString(x, "mergeBpn"),
            StoreCode = GetString(x, "storeCode"),
            StoreLocation = GetString(x, "storeLocation"),
            AvailableQuantity = GetDecimal(x, "warehouseAvailableQty"),
            Description = GetString(x, "describes")
        }).ToList();
    }

    private async Task<JsonDocument> GetMesAsync(string method, object parameters, CancellationToken cancellationToken)
    {
        var paramJson = JsonSerializer.Serialize(parameters);
        var url = $"{mesUrl}?method={Uri.EscapeDataString(method)}&param={Uri.EscapeDataString(paramJson)}";
        return await SendJsonAsync("MES", method, HttpMethod.Get, url, paramJson, cancellationToken);
    }

    private async Task<JsonDocument> SendJsonAsync(
        string system,
        string operation,
        HttpMethod method,
        string url,
        string parameters,
        CancellationToken cancellationToken)
    {
        var requestedAt = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (method == HttpMethod.Post)
            {
                request.Content = new StringContent(parameters, Encoding.UTF8, "application/json");
            }

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            var respondedAt = DateTimeOffset.Now;
            stopwatch.Stop();

            JsonDocument? document = null;
            string result;
            try
            {
                document = JsonDocument.Parse(text);
                result = GetString(document.RootElement, "RESULT");
                if (result.Length == 0) result = "未返回RESULT";
            }
            catch (JsonException)
            {
                result = "INVALID_JSON";
            }

            WriteTrace(FormatTrace(
                system, operation, method, url, parameters, requestedAt, respondedAt,
                stopwatch.ElapsedMilliseconds,
                $"{(int)response.StatusCode} {response.ReasonPhrase}", result, text));

            if (!response.IsSuccessStatusCode)
            {
                document?.Dispose();
                throw new DashboardApiException($"{operation}失败：HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }
            if (document is null)
            {
                throw new DashboardApiException($"{operation}返回内容不是有效JSON");
            }
            return document;
        }
        catch (DashboardApiException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var respondedAt = DateTimeOffset.Now;
            stopwatch.Stop();
            WriteTrace(FormatTrace(
                system, operation, method, url, parameters, requestedAt, respondedAt,
                stopwatch.ElapsedMilliseconds, "未收到HTTP响应", "EXCEPTION",
                $"{exception.GetType().Name}: {exception.Message}"));
            throw;
        }
    }

    private static string FormatTrace(
        string system,
        string operation,
        HttpMethod method,
        string url,
        string parameters,
        DateTimeOffset requestedAt,
        DateTimeOffset respondedAt,
        long elapsedMilliseconds,
        string httpStatus,
        string result,
        string responseText) =>
        $"接口请求记录\n" +
        $"系统：{system}\n" +
        $"操作：{operation}\n" +
        $"请求时间：{requestedAt:yyyy-MM-dd HH:mm:ss.fff zzz}\n" +
        $"响应时间：{respondedAt:yyyy-MM-dd HH:mm:ss.fff zzz}\n" +
        $"耗时：{elapsedMilliseconds} ms\n" +
        $"请求方式：{method.Method}\n" +
        $"请求URL：{url}\n" +
        $"请求参数：{parameters}\n" +
        $"HTTP状态：{httpStatus}\n" +
        $"请求结果：{result}\n" +
        $"响应信息：{responseText}\n" +
        "------------------------------------------------------------";

    private void WriteTrace(string message)
    {
        try
        {
            trace?.Invoke(message);
        }
        catch
        {
            // Interface tracing must never stop dashboard operation.
        }
    }

    private static void EnsureSuccess(JsonElement root, string operation)
    {
        if (IsSuccess(root)) return;
        var message = GetString(root, "MESSAGE");
        throw new DashboardApiException($"{operation}失败{(message.Length == 0 ? string.Empty : "：" + message)}");
    }

    private static bool IsSuccess(JsonElement root)
    {
        var result = GetString(root, "RESULT");
        return result.Length == 0 || result.Equals("PASS", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryArray(JsonElement parent, string property, out JsonElement array)
    {
        if (parent.TryGetProperty(property, out array) && array.ValueKind == JsonValueKind.Array) return true;
        array = default;
        return false;
    }

    private static string GetString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return string.Empty;
        return value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() ?? string.Empty : value.ToString().Trim();
    }

    private static decimal GetDecimal(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        return decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number) ? number : 0;
    }

    private static int GetInt(JsonElement element, string property) =>
        int.TryParse(GetString(element, property), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static DateTime? GetDateTime(JsonElement element, string property) =>
        DateTime.TryParse(GetString(element, property), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value) ? value : null;

    public static string BuildEndpoint(string address, string fallbackAddress, string defaultPath)
    {
        var value = string.IsNullOrWhiteSpace(address) ? fallbackAddress : address.Trim();
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "http://" + value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new DashboardApiException($"接口地址格式无效：{address}");
        }

        var hasPath = uri.AbsolutePath.Length > 1;
        return value.TrimEnd('/') + (hasPath ? string.Empty : defaultPath);
    }

    public static string BuildWmsEndpoint(string address, string fallbackAddress, string endpointPath)
    {
        var value = string.IsNullOrWhiteSpace(address) ? fallbackAddress : address.Trim();
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "http://" + value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new DashboardApiException($"接口地址格式无效：{address}");
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + endpointPath;
    }
}

public sealed class DashboardApiException(string message, Exception? innerException = null) : Exception(message, innerException);
