using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services.SpeechService;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using ClassIsland.EarthquakeWarning.Eew;
using Microsoft.Extensions.Logging;

namespace ClassIsland.EarthquakeWarning;

/// <summary>
/// 地震预警小组件：在 ClassIsland 主界面的一行里显示中国地震台网（CENC）地震预警与最新地震。
/// </summary>
[ComponentInfo(
    "e7c4f0a2-0d7b-4a2e-9f5e-6b1c8d3a9f21",
    "地震预警",
    "\uE9D9",
    "实时显示中国地震台网（CENC）地震预警与最新地震信息，支持语音播报。")]
public class EarthquakeWarningComponent : ComponentBase<EarthquakeWarningSettings>
{
    private static readonly IBrush AccentBrush = new SolidColorBrush(Color.Parse("#FFD54F"));
    private static readonly IBrush IdleBackground = new SolidColorBrush(Color.Parse("#E61A1D25"));

    private readonly ILogger<EarthquakeWarningComponent> _logger;
    private readonly ISpeechService _speech;

    private readonly Border _root;
    private readonly Ellipse _statusDot;
    private readonly TextBlock _title;
    private readonly Border _reportBadge;
    private readonly TextBlock _reportText;
    private readonly TextBlock _magnitudeText;
    private readonly TextBlock _placeText;
    private readonly TextBlock _updatedText;
    private readonly TextBlock _detailText;
    private readonly TextBlock _countdownText;
    private readonly TextBlock _secondaryText;
    private readonly List<TextBlock> _tintableTexts = new();

    private CancellationTokenSource? _cts;
    private readonly DispatcherTimer _watchdog;
    private long _lastEqFetchTicks;
    private long _lastSuccessTicksUtc;
    private long _lastRestartTicksUtc;
    private string? _lastSpokenEventId;
    private CencEew? _lastEew;
    private List<EqEvent> _eqEvents = new();
    private bool _alertMode;

    public EarthquakeWarningComponent(ILogger<EarthquakeWarningComponent> logger, ISpeechService speech)
    {
        _logger = logger;
        _speech = speech;

        _statusDot = new Ellipse
        {
            Width = 7,
            Height = 7,
            Fill = new SolidColorBrush(Color.Parse("#38D169")),
            VerticalAlignment = VerticalAlignment.Center,
        };

        _title = new TextBlock
        {
            Text = "地震监测",
            FontSize = 12.5,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _reportText = new TextBlock
        {
            Text = string.Empty,
            FontSize = 8.5,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _reportBadge = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#33FFFFFF")),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(3, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Child = _reportText,
        };

        _magnitudeText = new TextBlock
        {
            Text = "M --",
            FontSize = 13.5,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var headerLeft = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
        };
        headerLeft.Children.Add(_statusDot);
        headerLeft.Children.Add(_title);
        headerLeft.Children.Add(_reportBadge);
        headerLeft.Children.Add(_magnitudeText);

        _placeText = new TextBlock
        {
            Text = "正在获取地震信息…",
            FontSize = 12.5,
            Margin = new Thickness(7, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };

        _updatedText = new TextBlock
        {
            Text = string.Empty,
            FontSize = 8.5,
            Opacity = 0.5,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
        };
        header.Children.Add(headerLeft);
        Grid.SetColumn(_placeText, 1);
        header.Children.Add(_placeText);
        Grid.SetColumn(_updatedText, 2);
        header.Children.Add(_updatedText);

        _detailText = new TextBlock
        {
            Text = string.Empty,
            FontSize = 10,
            Opacity = 0.72,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };

        _countdownText = new TextBlock
        {
            Text = string.Empty,
            FontSize = 10.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = AccentBrush,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8, 0, 0, 0),
            IsVisible = false,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };

        _secondaryText = new TextBlock
        {
            Text = string.Empty,
            FontSize = 9,
            Opacity = 0.45,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8, 0, 0, 0),
            IsVisible = false,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };

        var sub = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            Margin = new Thickness(0, -1, 0, 1),
        };
        sub.Children.Add(_detailText);
        Grid.SetColumn(_countdownText, 1);
        sub.Children.Add(_countdownText);
        Grid.SetColumn(_secondaryText, 2);
        sub.Children.Add(_secondaryText);

        var content = new StackPanel
        {
            Spacing = 0,
        };
        content.Children.Add(header);
        content.Children.Add(sub);

        _root = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 2, 10, 3),
            Background = IdleBackground,
            ClipToBounds = true,
            Child = content,
        };

        _tintableTexts.Add(_title);
        _tintableTexts.Add(_reportText);
        _tintableTexts.Add(_updatedText);
        _tintableTexts.Add(_magnitudeText);
        _tintableTexts.Add(_placeText);
        _tintableTexts.Add(_detailText);
        _tintableTexts.Add(_countdownText);
        _tintableTexts.Add(_secondaryText);

        Content = _root;
        MinWidth = 240;
        ToolTip.SetTip(this, "数据：中国地震台网（CENC）/ Wolfx · 仅供参考，请以官方发布为准。");

        _watchdog = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        _watchdog.Tick += (_, _) => CheckHealth();

        AttachedToVisualTree += (_, _) => StartMonitoring();
        AttachedToVisualTree += (_, _) => MarkLoaded();
        DetachedFromVisualTree += (_, _) => StopMonitoring();
    }

    private void StartMonitoring()
    {
        if (_cts != null)
        {
            return;
        }

        ApplyIdleForeground();
        Interlocked.Exchange(ref _lastSuccessTicksUtc, DateTime.UtcNow.Ticks);
        var cts = new CancellationTokenSource();
        _cts = cts;
        _updatedText.Text = "正在连接…";
        _watchdog.Start();
        _ = Task.Run(() => MonitorAsync(cts.Token));
    }

    private void StopMonitoring()
    {
        _watchdog.Stop();
        var cts = _cts;
        _cts = null;
        try
        {
            cts?.Cancel();
        }
        catch
        {
            // ignored
        }

        cts?.Dispose();
    }

    private void CheckHealth()
    {
        try
        {
            if (_cts == null)
            {
                return;
            }

            var interval = 2;
            try
            {
                interval = Math.Clamp(Settings.RefreshIntervalSeconds, 1, 10);
            }
            catch
            {
                // ignored
            }

            var last = Interlocked.Read(ref _lastSuccessTicksUtc);
            if (last <= 0)
            {
                return;
            }

            var staleSeconds = (DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc)).TotalSeconds;
            if (staleSeconds <= Math.Max(12, interval * 5))
            {
                return;
            }

            var nowTicks = DateTime.UtcNow.Ticks;
            var lastRestart = Interlocked.Read(ref _lastRestartTicksUtc);
            if (nowTicks - lastRestart < TimeSpan.TicksPerSecond * 5)
            {
                return;
            }

            Interlocked.Exchange(ref _lastRestartTicksUtc, nowTicks);
            _logger.LogWarning("地震预警数据已 {Seconds:0} 秒未更新，重新启动轮询", staleSeconds);
            RestartMonitoring();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "检查地震预警轮询状态失败");
        }
    }

    private void RestartMonitoring()
    {
        var old = _cts;
        _cts = null;
        try
        {
            old?.Cancel();
        }
        catch
        {
            // ignored
        }

        var cts = new CancellationTokenSource();
        _cts = cts;
        _updatedText.Text = "重连中…";
        _ = Task.Run(() => MonitorAsync(cts.Token));
    }

    private void MarkLoaded()
    {
        try
        {
            // ClassIsland 的 Shimmer 载入动画在部分环境下不会结束，这里主动把它标记为已载入。
            var shimmer = this.FindAncestorOfType<Shimmer>();
            if (shimmer == null)
            {
                return;
            }

            shimmer.AutoDetectContentLoadState = true;
            shimmer.IsContentLoaded = true;
            ForceShimmerVisible(shimmer);
            for (var i = 0; i < 4; i++)
            {
                DispatcherTimer.RunOnce(() =>
                {
                    try
                    {
                        shimmer.IsContentLoaded = true;
                        ForceShimmerVisible(shimmer);
                    }
                    catch
                    {
                        // ignored
                    }
                }, TimeSpan.FromMilliseconds(300 * (i + 1)));
            }
        }
        catch
        {
            // ignored
        }
    }

    private static void ForceShimmerVisible(Visual visual)
    {
        foreach (var child in visual.GetVisualChildren())
        {
            if (child is ContentPresenter presenter && presenter.Name == "ContentPresenter")
            {
                presenter.Opacity = 1;
            }

            if (child is Border border && border.Name == "PART_MainBorder")
            {
                border.IsVisible = false;
            }

            ForceShimmerVisible(child);
        }
    }

    private async Task MonitorAsync(CancellationToken token)
    {
        var first = true;
        while (!token.IsCancellationRequested)
        {
            CencEew? eew = null;
            List<EqEvent>? events = null;
            var failed = false;

            try
            {
                eew = await EarthquakeApi.GetEewAsync(token).ConfigureAwait(false);
                Interlocked.Exchange(ref _lastSuccessTicksUtc, DateTime.UtcNow.Ticks);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException ex)
            {
                failed = true;
                _logger.LogWarning(ex, "地震预警请求超时，稍后重试");
            }
            catch (Exception ex)
            {
                failed = true;
                _logger.LogDebug(ex, "获取地震预警数据失败");
            }

            if (first || DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastEqFetchTicks) > TimeSpan.TicksPerMinute)
            {
                try
                {
                    events = await EarthquakeApi.GetEqListAsync(token).ConfigureAwait(false);
                    Interlocked.Exchange(ref _lastEqFetchTicks, DateTime.UtcNow.Ticks);
                    Interlocked.Exchange(ref _lastSuccessTicksUtc, DateTime.UtcNow.Ticks);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException ex)
                {
                    failed = true;
                    _logger.LogWarning(ex, "地震目录请求超时，稍后重试");
                }
                catch (Exception ex)
                {
                    failed = true;
                    _logger.LogDebug(ex, "获取地震目录失败");
                }

                first = false;
            }

            var snapshotEew = eew;
            var snapshotEvents = events;
            var snapshotFailed = failed;
            Dispatcher.UIThread.Post(() => ApplySnapshot(snapshotEew, snapshotEvents, snapshotFailed));

            var interval = 2;
            try
            {
                interval = Math.Clamp(Settings.RefreshIntervalSeconds, 1, 10);
            }
            catch
            {
                // Settings 尚未加载完成时使用默认值
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void ApplySnapshot(CencEew? eew, List<EqEvent>? events, bool failed)
    {
        try
        {
            if (eew != null)
            {
                _lastEew = eew;
            }

            if (events != null)
            {
                _eqEvents = events;
            }

            var now = DateTime.Now;
            var active = GetActiveEew(_lastEew, now);

            if (active != null)
            {
                RenderAlert(active, now);
            }
            else
            {
                RenderIdle();
            }

            if (failed)
            {
                _statusDot.Fill = new SolidColorBrush(Color.Parse("#FFA726"));
                _updatedText.Text = "更新失败";
            }
            else
            {
                Interlocked.Exchange(ref _lastSuccessTicksUtc, DateTime.UtcNow.Ticks);
                _statusDot.Fill = active != null
                    ? new SolidColorBrush(Color.Parse("#FFEBEE"))
                    : new SolidColorBrush(Color.Parse("#38D169"));
                _updatedText.Text = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            }

            MaybeSpeak(active, now);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新地震预警界面失败");
        }
    }

    private CencEew? GetActiveEew(CencEew? eew, DateTime now)
    {
        if (eew == null || string.IsNullOrWhiteSpace(eew.EventId))
        {
            return null;
        }

        var reportTime = eew.ReportTime;
        if (reportTime == DateTime.MinValue)
        {
            return null;
        }

        var limit = 120;
        try
        {
            limit = Math.Clamp(Settings.ActiveWindowSeconds, 30, 600);
        }
        catch
        {
            // ignored
        }

        var age = (now - reportTime).TotalSeconds;
        return age >= -120 && age <= limit ? eew : null;
    }

    private void RenderIdle()
    {
        SetAlertMode(false);
        _reportBadge.IsVisible = false;
        _countdownText.IsVisible = false;

        var latest = _eqEvents.FirstOrDefault();
        if (latest != null)
        {
            _magnitudeText.Text = FormatMagnitude(latest.Magnitude);
            _placeText.Text = latest.Place ?? "未知地点";

            var parts = new List<string>
            {
                latest.Time.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture),
                "深 " + latest.Depth.ToString("0.#", CultureInfo.InvariantCulture) + " km",
            };
            if (latest.Intensity > 0)
            {
                parts.Add("烈度 " + latest.Intensity.ToString("0.#", CultureInfo.InvariantCulture));
            }

            if (latest.Type == "automatic")
            {
                parts.Add("自动测定");
            }

            _detailText.Text = string.Join(" · ", parts);
        }
        else
        {
            _magnitudeText.Text = "M --";
            _placeText.Text = "暂无地震数据";
            _detailText.Text = "尚未获取到中国地震台网的地震信息";
        }

        var second = _eqEvents.Skip(1).FirstOrDefault();
        var showSecond = false;
        try
        {
            showSecond = Settings.ShowRecentEarthquakes;
        }
        catch
        {
            // ignored
        }

        if (second != null && showSecond)
        {
            _secondaryText.Text = "次近 " + FormatMagnitude(second.Magnitude) + " " +
                                  (second.Place ?? "未知地点") + " " +
                                  second.Time.ToString("HH:mm", CultureInfo.InvariantCulture);
            _secondaryText.IsVisible = true;
        }
        else
        {
            _secondaryText.IsVisible = false;
        }
    }

    private void RenderAlert(CencEew eew, DateTime now)
    {
        SetAlertMode(true);
        _reportBadge.IsVisible = true;
        _reportText.Text = "第 " + Math.Max(1, eew.ReportNum) + " 报";
        _countdownText.IsVisible = true;
        _secondaryText.IsVisible = false;

        _magnitudeText.Text = FormatMagnitude(eew.Magnitude);
        _placeText.Text = string.IsNullOrWhiteSpace(eew.HypoCenter) ? "未知震中" : eew.HypoCenter;

        var parts = new List<string>();
        if (eew.MaxIntensity > 0)
        {
            parts.Add("预估最大烈度 " + eew.MaxIntensity.ToString("0.#", CultureInfo.InvariantCulture));
        }

        if (eew.Depth > 0)
        {
            parts.Add("深 " + eew.Depth.ToString("0", CultureInfo.InvariantCulture) + " km");
        }

        if (eew.ReportTime != DateTime.MinValue)
        {
            parts.Add("发震 " + eew.ReportTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        }

        _detailText.Text = string.Join(" · ", parts);
        _countdownText.Text = BuildCountdown(eew, now);
    }

    private string BuildCountdown(CencEew eew, DateTime now)
    {
        var origin = eew.OriginTime;
        if (origin == DateTime.MinValue)
        {
            origin = eew.ReportTime;
        }

        if (origin == DateTime.MinValue)
        {
            return string.Empty;
        }

        double? latitude = null;
        double? longitude = null;
        try
        {
            latitude = Settings.Latitude;
            longitude = Settings.Longitude;
        }
        catch
        {
            // ignored
        }

        if (latitude is { } lat && longitude is { } lon && Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180)
        {
            var distance = GeoDistance(lat, lon, eew.Latitude, eew.Longitude);
            if (distance > 0 && distance < 3000)
            {
                var arrival = origin.AddSeconds(distance / 3.5);
                var remain = (arrival - now).TotalSeconds;
                return remain > 0
                    ? "横波 " + remain.ToString("0", CultureInfo.InvariantCulture) + " 秒后到达（约 " +
                      distance.ToString("0", CultureInfo.InvariantCulture) + " km）"
                    : "横波已到达（约 " + distance.ToString("0", CultureInfo.InvariantCulture) + " km）";
            }
        }

        var elapsed = (now - origin).TotalSeconds;
        return elapsed >= 0
            ? "发震已过 " + elapsed.ToString("0", CultureInfo.InvariantCulture) + " 秒 · 注意避险"
            : "注意避险";
    }

    private void SetAlertMode(bool alert)
    {
        if (_alertMode == alert)
        {
            return;
        }

        _alertMode = alert;
        if (alert)
        {
            _root.Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#D84343"), 0),
                    new GradientStop(Color.Parse("#7E1010"), 1),
                },
            };
            _title.Text = "地震预警";
            foreach (var text in _tintableTexts)
            {
                text.Foreground = Brushes.White;
            }
        }
        else
        {
            _root.Background = IdleBackground;
            _title.Text = "地震监测";
            ApplyIdleForeground();
        }
    }

    private void ApplyIdleForeground()
    {
        foreach (var text in _tintableTexts)
        {
            text.Foreground = ReferenceEquals(text, _countdownText) ? AccentBrush : Brushes.White;
        }
    }

    private void MaybeSpeak(CencEew? active, DateTime now)
    {
        if (active == null || string.IsNullOrWhiteSpace(active.EventId))
        {
            return;
        }

        bool enabled;
        try
        {
            enabled = Settings.EnableSpeech;
        }
        catch
        {
            return;
        }

        if (!enabled || active.EventId == _lastSpokenEventId)
        {
            return;
        }

        var age = (now - (active.OriginTime == DateTime.MinValue ? active.ReportTime : active.OriginTime))
            .TotalSeconds;
        if (age < 0 || age > 150)
        {
            return;
        }

        _lastSpokenEventId = active.EventId;

        var speech = "地震预警。" + (active.HypoCenter ?? "某地") + "发生" +
                     active.Magnitude.ToString("0.0", CultureInfo.InvariantCulture) + "级地震。";
        if (active.MaxIntensity > 0)
        {
            speech += "预计最大烈度" + active.MaxIntensity.ToString("0.#", CultureInfo.InvariantCulture) + "。";
        }

        speech += "请注意避险。";

        try
        {
            _speech.EnqueueSpeechQueue(speech);
            _logger.LogInformation("播报地震预警：{Speech}", speech);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "播报地震预警失败");
        }
    }

    private static string FormatMagnitude(double magnitude)
    {
        return magnitude > 0 ? "M " + magnitude.ToString("0.0", CultureInfo.InvariantCulture) : "M --";
    }

    private static double GeoDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadius = 6371.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
