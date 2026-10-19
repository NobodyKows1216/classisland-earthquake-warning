namespace ClassIsland.EarthquakeWarning;

/// <summary>
/// 地震预警组件的设置。每个组件实例都有独立的一份设置。
/// </summary>
public class EarthquakeWarningSettings
{
    /// <summary>
    /// 地震预警轮询间隔（秒）。
    /// </summary>
    public int RefreshIntervalSeconds { get; set; } = 2;

    /// <summary>
    /// 预警在发震后多少秒内仍视为“预警中”。
    /// </summary>
    public int ActiveWindowSeconds { get; set; } = 120;

    /// <summary>
    /// 是否在收到新的地震预警时进行语音播报。
    /// </summary>
    public bool EnableSpeech { get; set; } = true;

    /// <summary>
    /// 是否在空闲时显示最近的地震信息。
    /// </summary>
    public bool ShowRecentEarthquakes { get; set; } = true;

    /// <summary>
    /// 所在位置的纬度（可选）。填写后可以估算横波到达时间。
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// 所在位置的经度（可选）。
    /// </summary>
    public double? Longitude { get; set; }
}
