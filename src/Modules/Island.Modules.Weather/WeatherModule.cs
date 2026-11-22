using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Island.Core;
using Microsoft.Win32;

namespace Island.Modules.Weather;

/// <summary>
/// 天氣(Open-Meteo,免費、不需金鑰)。
/// - 每 15 分鐘抓一次(只有那一下連網),收合時右側小提示顯示「☀ 24°」。
/// - 展開的「天氣」分頁:現在溫度、狀況、體感、今日高低溫與降雨機率、接下來 6 小時。
/// - 30 分鐘內可能下雨(而現在沒下)時,膠囊主動出現提醒,同一場雨只提醒一次。
/// 位置存在 %LOCALAPPDATA%\Island\modules\weather\location.json,只送出經緯度。
/// </summary>
public sealed class WeatherModule : IslandModuleBase, IIslandActivity, IIslandExpandAware, IIslandCompact, IIslandSettingsPage
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StaleOnExpand = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AlertDuration = TimeSpan.FromSeconds(5);
    /// <summary>一格 15 分鐘的降水量達到多少才算「會下雨」(mm)。</summary>
    private const double RainThreshold = 0.1;

    private static readonly HttpClient Http = CreateHttp();

    private DispatcherTimer? _timer;
    private DispatcherTimer? _alertTimer;
    private WeatherView? _view;
    private WeatherAlertView? _alertView;
    private System.Windows.Controls.TextBlock? _compact;
    private bool _fetching;
    private bool _alertActive;
    private bool _rainAlerted;   // 這一場雨已經提醒過

    public override string Id => "weather";
    public override string Title => "天氣";

    public WeatherLocation Location { get; private set; } = WeatherLocation.Default;
    public WeatherSnapshot? Current { get; private set; }
    public string? Error { get; private set; }

    public event EventHandler? ActivityChanged;
    public bool IsActivityActive => _alertActive;

    protected override void OnInitialize()
    {
        Location = WeatherLocation.Load(Host.GetModuleDataDirectory(Id));
        _timer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => _ = RefreshAsync();
        _timer.Start();   // 啟動 2 秒後第一次抓,之後每 15 分鐘
        _alertTimer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = AlertDuration + TimeSpan.FromMilliseconds(300) };
        _alertTimer.Tick += (_, _) => { _alertTimer.Stop(); SetAlert(false); };
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public FrameworkElement CreateActivityView() => _alertView ??= new WeatherAlertView();

    public bool IsCompactRelevant => Current is not null;

    /// <summary>小圓:符號 + 溫度,例如「☾16°」。</summary>
    public FrameworkElement CreateCompactView()
    {
        _compact ??= new System.Windows.Controls.TextBlock
        {
            FontSize = 11,
            Foreground = System.Windows.Media.Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        UpdateCompact();
        return _compact;
    }

    private void UpdateCompact()
    {
        if (_compact is null || Current is not { } c) return;
        _compact.Text = $"{WeatherCodes.Symbol(c.Code, c.IsDay)}{Math.Round(c.Temperature):0}°";
    }
    public override FrameworkElement CreateExpandedView() => _view ??= new WeatherView(this);

    public string SettingsTitle => "天氣";
    public FrameworkElement CreateSettingsView() => new WeatherSettingsView(this);

    /// <summary>換城市:存檔並立刻重新抓取。</summary>
    public void SetLocation(WeatherLocation location)
    {
        Location = location;
        location.Save(Host.GetModuleDataDirectory(Id));
        Host.Log($"天氣:位置改為 {location.Name} ({location.Latitude}, {location.Longitude})");
        _rainAlerted = false;
        _ = RefreshAsync();
    }

    public void OnExpanded()
    {
        _view?.Update();
        if (Current is null || DateTime.Now - Current.FetchedAt > StaleOnExpand) _ = RefreshAsync();
    }

    public void OnCollapsed() { }

    private async Task RefreshAsync()
    {
        if (_fetching) return;
        _fetching = true;
        _timer!.Stop();
        try
        {
            var json = await Http.GetStringAsync(Location.BuildUrl());
            Current = WeatherSnapshot.Parse(json, DateTime.Now);
            Error = null;
            _timer.Interval = RefreshInterval;
            CheckRain(Current);
        }
        catch (Exception ex)
        {
            Error = "無法取得天氣";
            Host.Log($"天氣:更新失敗:{ex.Message}");
            _timer.Interval = RetryInterval;
        }
        finally
        {
            _fetching = false;
            _timer.Start();
            UpdateBadge();
            _view?.Update();
        }
    }

    private void UpdateBadge()
    {
        if (Current is not { } c)
        {
            SetBadge(null);
            return;
        }
        UpdateCompact();
        SetBadge(new IslandBadge($"{WeatherCodes.Symbol(c.Code, c.IsDay)} {Math.Round(c.Temperature):0}°"));
    }

    /// <summary>30 分鐘內(接下來兩格 15 分)有降水、而現在沒在下 → 提醒一次;雨停/預報轉晴後重設。</summary>
    private void CheckRain(WeatherSnapshot s)
    {
        bool rainingNow = s.PrecipitationNow >= RainThreshold || WeatherCodes.IsWet(s.Code);
        bool rainSoon = s.Next15Min.Skip(1).Take(2).Any(v => v >= RainThreshold);

        if (rainingNow)
        {
            _rainAlerted = true;    // 已經在下了,不用再提醒
            return;
        }
        if (!rainSoon)
        {
            _rainAlerted = false;   // 預報轉晴,下次再有雨可以重新提醒
            return;
        }
        if (_rainAlerted) return;

        _rainAlerted = true;
        Host.Log("天氣:30 分鐘內可能下雨,提醒");
        _alertView ??= new WeatherAlertView();
        _alertView.Show("30 分鐘內可能下雨", $"{Location.Name} · 記得帶傘");
        SetAlert(true);
        Host.Announce(Id, AlertDuration);
        _alertTimer!.Stop();
        _alertTimer.Start();
    }

    private void SetAlert(bool active)
    {
        if (_alertActive == active) return;
        _alertActive = active;
        ActivityChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>從睡眠喚醒:資料可能已經舊了,稍等網路連上後重新抓。</summary>
    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume) return;
        Host.Dispatcher.BeginInvoke(() =>
        {
            _timer!.Stop();
            _timer.Interval = TimeSpan.FromSeconds(10);
            _timer.Start();
        });
    }

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Island/0.1");
        return http;
    }

    public override void Dispose()
    {
        _timer?.Stop();
        _alertTimer?.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        base.Dispose();
    }
}

/// <summary>天氣位置設定(只存地名與經緯度)。</summary>
public sealed record WeatherLocation(string Name, double Latitude, double Longitude)
{
    public static readonly WeatherLocation Default = new("新竹市東區", 24.80, 120.99);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string BuildUrl() =>
        "https://api.open-meteo.com/v1/forecast" +
        $"?latitude={Latitude.ToString("0.####", CultureInfo.InvariantCulture)}" +
        $"&longitude={Longitude.ToString("0.####", CultureInfo.InvariantCulture)}" +
        "&current=temperature_2m,apparent_temperature,weather_code,is_day,precipitation" +
        "&hourly=temperature_2m,weather_code,precipitation_probability" +
        "&minutely_15=precipitation" +
        "&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max" +
        "&timezone=auto&forecast_days=2";

    public void Save(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "location.json"), JsonSerializer.Serialize(this, JsonOptions));
        }
        catch { /* 存不了就算了,下次啟動用舊的 */ }
    }

    /// <summary>讀 location.json;沒有就寫入預設值,方便之後手動修改。</summary>
    public static WeatherLocation Load(string directory)
    {
        var file = Path.Combine(directory, "location.json");
        try
        {
            if (File.Exists(file))
            {
                var loaded = JsonSerializer.Deserialize<WeatherLocation>(File.ReadAllText(file), JsonOptions);
                if (loaded is not null) return loaded;
            }
            Directory.CreateDirectory(directory);
            File.WriteAllText(file, JsonSerializer.Serialize(Default, JsonOptions));
        }
        catch { /* 讀寫失敗就用預設 */ }
        return Default;
    }
}
