using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassIsland.EarthquakeWarning.Eew;

/// <summary>
/// 中国地震台网（CENC）地震预警数据。数据来源：Wolfx Open API。
/// </summary>
public sealed class CencEew
{
    [JsonPropertyName("ID")] public string? Id { get; set; }
    [JsonPropertyName("EventID")] public string? EventId { get; set; }
    [JsonPropertyName("ReportTime")] public string? ReportTimeRaw { get; set; }
    [JsonPropertyName("ReportNum")] public int ReportNum { get; set; }
    [JsonPropertyName("OriginTime")] public string? OriginTimeRaw { get; set; }
    [JsonPropertyName("HypoCenter")] public string? HypoCenter { get; set; }
    [JsonPropertyName("Latitude")] public double Latitude { get; set; }
    [JsonPropertyName("Longitude")] public double Longitude { get; set; }
    [JsonPropertyName("Magnitude")] public double Magnitude { get; set; }
    [JsonPropertyName("Depth")] public double Depth { get; set; }
    [JsonPropertyName("MaxIntensity")] public double MaxIntensity { get; set; }

    [JsonIgnore] public DateTime ReportTime => TimeUtil.Parse(ReportTimeRaw);
    [JsonIgnore] public DateTime OriginTime => TimeUtil.Parse(OriginTimeRaw);
}

/// <summary>
/// 中国地震台网地震目录条目。
/// </summary>
public sealed class EqEvent
{
    public string? Type { get; init; }
    public string? EventId { get; init; }
    public DateTime Time { get; init; }
    public string? Place { get; init; }
    public double Magnitude { get; init; }
    public double Depth { get; init; }
    public double Intensity { get; init; }
}

public static class TimeUtil
{
    private static readonly string[] Formats =
    {
        "yyyy-MM-dd HH:mm:ss",
        "yyyy/MM/dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm",
    };

    public static DateTime Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.MinValue;
        }

        if (DateTime.TryParseExact(value.Trim(), Formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var exact))
        {
            return exact;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : DateTime.MinValue;
    }
}

public static class EarthquakeApi
{
    public const string EewUrl = "https://api.wolfx.jp/cenc_eew.json";
    public const string EqListUrl = "https://api.wolfx.jp/cenc_eqlist.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static readonly System.Net.Http.HttpClient Client = CreateClient();

    private static System.Net.Http.HttpClient CreateClient()
    {
        var client = new System.Net.Http.HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ClassIsland-EarthquakeWarning/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    public static async System.Threading.Tasks.Task<CencEew?> GetEewAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        var json = await Client.GetStringAsync(EewUrl, cancellationToken).ConfigureAwait(false);
        var eew = JsonSerializer.Deserialize<CencEew>(json, Options);
        if (eew == null || string.IsNullOrWhiteSpace(eew.EventId))
        {
            return null;
        }

        return eew;
    }

    public static async System.Threading.Tasks.Task<List<EqEvent>> GetEqListAsync(
        System.Threading.CancellationToken cancellationToken)
    {
        var json = await Client.GetStringAsync(EqListUrl, cancellationToken).ConfigureAwait(false);
        var result = new List<EqEvent>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (!property.Name.StartsWith("No", StringComparison.Ordinal))
            {
                continue;
            }

            var item = property.Value;
            var time = TimeUtil.Parse(GetString(item, "time"));
            if (time == DateTime.MinValue)
            {
                continue;
            }

            result.Add(new EqEvent
            {
                Type = GetString(item, "type"),
                EventId = GetString(item, "EventID"),
                Time = time,
                Place = GetString(item, "placeName") ?? GetString(item, "location"),
                Magnitude = GetDouble(item, "magnitude"),
                Depth = GetDouble(item, "depth"),
                Intensity = GetDouble(item, "intensity"),
            });
        }

        result.Sort((a, b) => b.Time.CompareTo(a.Time));
        return result;
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null,
        };
    }

    private static double GetDouble(JsonElement element, string name)
    {
        var text = GetString(element, name);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
