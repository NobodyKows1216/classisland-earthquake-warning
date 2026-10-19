using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services.SpeechService;

namespace ClassIsland.EarthquakeWarning;

/// <summary>
/// 地震预警组件的设置界面。
/// </summary>
public class EarthquakeWarningSettingsControl : ComponentBase<EarthquakeWarningSettings>
{
    private readonly ISpeechService _speech;
    private readonly NumericUpDown _refresh;
    private readonly NumericUpDown _activeWindow;
    private readonly ToggleSwitch _speechToggle;
    private readonly ToggleSwitch _recentToggle;
    private readonly TextBox _latitude;
    private readonly TextBox _longitude;
    private bool _loading;

    public EarthquakeWarningSettingsControl(ISpeechService speech)
    {
        _speech = speech;

        _refresh = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 10,
            Increment = 1,
            Value = 2,
            Width = 130,
            FormatString = "0",
        };
        _refresh.ValueChanged += (_, e) =>
        {
            if (_loading)
            {
                return;
            }

            Settings.RefreshIntervalSeconds = (int)Math.Clamp((double)(e.NewValue ?? 2m), 1, 10);
        };

        _activeWindow = new NumericUpDown
        {
            Minimum = 30,
            Maximum = 600,
            Increment = 30,
            Value = 120,
            Width = 130,
            FormatString = "0",
        };
        _activeWindow.ValueChanged += (_, e) =>
        {
            if (_loading)
            {
                return;
            }

            Settings.ActiveWindowSeconds = (int)Math.Clamp((double)(e.NewValue ?? 120m), 30, 600);
        };

        _speechToggle = new ToggleSwitch
        {
            IsChecked = true,
        };
        _speechToggle.PropertyChanged += (_, e) =>
        {
            if (_loading || e.Property != ToggleSwitch.IsCheckedProperty)
            {
                return;
            }

            Settings.EnableSpeech = _speechToggle.IsChecked == true;
        };

        _recentToggle = new ToggleSwitch
        {
            IsChecked = true,
        };
        _recentToggle.PropertyChanged += (_, e) =>
        {
            if (_loading || e.Property != ToggleSwitch.IsCheckedProperty)
            {
                return;
            }

            Settings.ShowRecentEarthquakes = _recentToggle.IsChecked == true;
        };

        _latitude = new TextBox
        {
            Width = 110,
            Watermark = "如 30.27",
        };
        _latitude.TextChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }

            Settings.Latitude = ParseCoordinate(_latitude.Text, 90);
        };

        _longitude = new TextBox
        {
            Width = 110,
            Watermark = "如 120.15",
        };
        _longitude.TextChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }

            Settings.Longitude = ParseCoordinate(_longitude.Text, 180);
        };

        var testButton = new Button
        {
            Content = "测试语音",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        testButton.Click += (_, _) =>
        {
            try
            {
                _speech?.EnqueueSpeechQueue("地震预警播报测试。请注意避险。");
            }
            catch
            {
                // ignored
            }
        };

        var coordinates = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        coordinates.Children.Add(_latitude);
        coordinates.Children.Add(_longitude);

        var panel = new StackPanel
        {
            Spacing = 14,
            Margin = new Thickness(4, 4, 4, 12),
        };

        panel.Children.Add(Section("监测"));
        panel.Children.Add(Row("刷新间隔（秒）", "地震预警数据的轮询间隔，建议 1–3 秒。", _refresh));
        panel.Children.Add(Row("预警有效时长（秒）", "发震后多长时间内仍然显示为“预警中”。", _activeWindow));

        panel.Children.Add(Section("播报与内容"));
        panel.Children.Add(Row("语音播报", "收到新的地震预警时，通过 ClassIsland 的 TTS 服务播报。", _speechToggle));
        panel.Children.Add(Row("显示次近地震", "没有预警时，在主界面行右侧显示再上一次地震的简要信息。", _recentToggle));
        panel.Children.Add(Row("所在位置（可选）", "填写纬度和经度后，会估算横波到达倒计时；留空则显示发震已过时间。", coordinates));

        panel.Children.Add(Section("其他"));
        panel.Children.Add(Row("语音测试", "使用当前语音服务播报一段测试内容。", testButton));

        var note = new TextBlock
        {
            Text = "数据来源：中国地震台网（CENC），由 Wolfx Open API 转发。地震预警信息仅供参考，请以官方发布为准。",
            FontSize = 11,
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        };
        panel.Children.Add(note);

        Content = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        AttachedToVisualTree += (_, _) => LoadSettings();
    }

    private void LoadSettings()
    {
        if (Settings == null)
        {
            return;
        }

        _loading = true;
        try
        {
            _refresh.Value = Math.Clamp(Settings.RefreshIntervalSeconds, 1, 10);
            _activeWindow.Value = Math.Clamp(Settings.ActiveWindowSeconds, 30, 600);
            _speechToggle.IsChecked = Settings.EnableSpeech;
            _recentToggle.IsChecked = Settings.ShowRecentEarthquakes;
            _latitude.Text = Settings.Latitude?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;
            _longitude.Text = Settings.Longitude?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;
        }
        catch
        {
            // Settings 尚未初始化时忽略
        }
        finally
        {
            _loading = false;
        }
    }

    private static double? ParseCoordinate(string? text, double limit)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
               Math.Abs(value) <= limit
            ? value
            : null;
    }

    private static TextBlock Section(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 4, 0, 0),
        };
    }

    private static Control Row(string title, string description, Control control)
    {
        var label = new TextBlock
        {
            Text = title,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        };
        var desc = new TextBlock
        {
            Text = description,
            FontSize = 11,
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0),
        };
        var left = new StackPanel
        {
            Spacing = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };
        left.Children.Add(label);
        left.Children.Add(desc);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        grid.Children.Add(left);
        Grid.SetColumn(control, 1);
        control.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(control);
        return grid;
    }
}
