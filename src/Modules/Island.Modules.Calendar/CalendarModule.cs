using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Island.Core;

namespace Island.Modules.Calendar;

/// <summary>
/// 行程提醒:讀日曆的 iCal「私人網址」(Google 日曆:設定 → 你的日曆 → 整合日曆 → iCal 格式的私人網址),
/// 行程開始前 N 分鐘島跳出「10 分鐘後 · 週會」,中間長條一直顯示到行程開始。
/// 展開「行程」分頁看今天和明天的行程;滑鼠移過去那一排右邊顯示「下個行程 14:00」。
/// 預設關閉(設定 →「模組」打開,再到「行程」頁貼上網址)。網址只存在這台電腦,不會寫進記錄檔。
/// 效能:每 15 分鐘下載一次;提醒用單一計時器排到下一個提醒時間,其餘時間不耗資源。
/// </summary>
public sealed class CalendarModule : IslandModuleBase, IIslandActivity, IIslandSettingsPage, IIslandCompact
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AnnounceDuration = TimeSpan.FromSeconds(6);

    private CalendarOptions _options = new();
    private string _dir = "";
    private DispatcherTimer? _refreshTimer, _reminderTimer, _minuteTimer;
    private List<CalEvent> _events = [];
    private CalEvent? _upcoming;          // 正在提醒的行程(提醒時間 ~ 開始)
    private readonly HashSet<(DateTime, string)> _reminded = [];
    private CalendarView? _view;
    private CalendarActivityView? _activityView;
    private TextBlock? _status;

    public override string Id => "calendar";
    public override string Title => "行程";
    public override bool EnabledByDefault => false;

    public event EventHandler? ActivityChanged;
    public bool IsActivityActive => _upcoming is not null;
    public IReadOnlyList<CalEvent> Events => _events;
    public string LastStatus { get; private set; } = "還沒設定日曆網址";

    protected override void OnInitialize()
    {
        _dir = Host.GetModuleDataDirectory(Id);
        _options = CalendarOptions.Load(_dir);
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = RefreshEvery };
        _refreshTimer.Tick += (_, _) => _ = RefreshAsync();
        _reminderTimer = new DispatcherTimer(DispatcherPriority.Normal, Host.Dispatcher);
        _reminderTimer.Tick += (_, _) => { _reminderTimer.Stop(); Evaluate(); };
        _minuteTimer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = TimeSpan.FromSeconds(30) };
        _minuteTimer.Tick += (_, _) => Evaluate();
        if (!string.IsNullOrWhiteSpace(_options.IcsUrl))
        {
            _ = RefreshAsync();
            _refreshTimer.Start();
        }
    }

    // ── 下載 / 解析 ──

    private async Task RefreshAsync()
    {
        var url = _options.IcsUrl?.Trim();
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)) url = "https://" + url["webcal://".Length..];
            var ics = await Http.GetStringAsync(url);
            var now = DateTime.Now;
            _events = IcsParser.Parse(ics, now.Date.AddDays(-1), now.Date.AddDays(8));
            LastStatus = $"已更新({now:HH:mm}):接下來 7 天有 {_events.Count(e => e.Start >= now.Date)} 個行程";
            Host.Log($"行程:讀到 {_events.Count} 個行程");
        }
        catch (Exception ex)
        {
            // 不記網址(是私人連結)
            LastStatus = $"讀取失敗:{(ex is HttpRequestException h && h.StatusCode is { } code ? $"HTTP {(int)code}" : ex.GetType().Name)}";
            Host.Log($"行程:{LastStatus}");
        }
        if (_status is not null) _status.Text = LastStatus;
        _view?.Refresh();
        Evaluate();
    }

    // ── 提醒 ──

    /// <summary>決定現在要不要提醒、下一次什麼時候醒來。</summary>
    private void Evaluate()
    {
        var now = DateTime.Now;
        var remind = TimeSpan.FromMinutes(_options.RemindMinutes);
        var next = CalendarLogic.NextReminder(_events, now, remind);
        var wasActive = IsActivityActive;

        _upcoming = next is { } n && n.Start - remind <= now && n.Start > now ? n : null;
        if (_upcoming is { } up)
        {
            _activityView ??= new CalendarActivityView();
            _activityView.Show(up, now);
            if (_reminded.Add((up.Start, up.Title))) Host.Announce(Id, AnnounceDuration);
            _minuteTimer!.Start();   // 提醒期間每 30 秒更新「N 分鐘後」
        }
        else _minuteTimer!.Stop();
        if (wasActive != IsActivityActive) ActivityChanged?.Invoke(this, EventArgs.Empty);

        // 排下一次醒來:下一個提醒時間
        _reminderTimer!.Stop();
        var wake = CalendarLogic.NextWake(_events, now, remind);
        if (wake is { } w)
        {
            var delay = w - now;
            if (delay < TimeSpan.FromSeconds(1)) delay = TimeSpan.FromSeconds(1);
            if (delay > TimeSpan.FromHours(6)) delay = TimeSpan.FromHours(6);   // 很久以後的行程:中途再檢查一次就好
            _reminderTimer.Interval = delay;
            _reminderTimer.Start();
        }
        UpdateBadge(now);
    }

    private void UpdateBadge(DateTime now)
    {
        var next = _events.FirstOrDefault(e => !e.AllDay && e.Start > now && e.Start.Date == now.Date);
        SetBadge(next is null ? null : new IslandBadge($"下個行程 {next.Start:HH:mm}"));
    }

    public FrameworkElement CreateActivityView() => _activityView ??= new CalendarActivityView();
    public override FrameworkElement CreateExpandedView() => _view ??= new CalendarView(this);

    // ── 小圓:提醒期間顯示剩幾分鐘 ──
    private TextBlock? _circleText;
    public bool IsCompactRelevant => _upcoming is not null;

    public FrameworkElement CreateCompactView()
    {
        var g = new Grid { Width = 30, Height = 30 };
        _circleText = new TextBlock { FontSize = 10, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _circleText.SetResourceReference(TextBlock.ForegroundProperty, IslandTheme.Accent);
        g.Children.Add(_circleText);
        g.IsVisibleChanged += (_, _) => { if (_upcoming is { } up) _circleText.Text = $"{Math.Max(0, (int)Math.Ceiling((up.Start - DateTime.Now).TotalMinutes))}′"; };
        return g;
    }

    // ── 設定頁 ──
    public string SettingsTitle => "行程";

    public FrameworkElement CreateSettingsView()
    {
        var p = new StackPanel();
        var caption = new TextBlock
        {
            Text = "貼上日曆的 iCal 私人網址。Google 日曆:電腦版 → 設定 → 點左邊你的日曆 → 「整合日曆」→「iCal 格式的私人網址」。Outlook:設定 → 日曆 → 共用的行事曆 → 發佈行事曆 → ICS。網址只存在這台電腦。",
            TextWrapping = TextWrapping.Wrap,
        };
        caption.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsCaptionText);
        p.Children.Add(caption);

        var card = new Border { Margin = new Thickness(0, 10, 0, 0) };
        card.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsCard);
        var cp = new StackPanel();
        var title = new TextBlock { Text = "日曆網址(.ics)" };
        title.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsTitleText);
        cp.Children.Add(title);
        var row = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var save = new Button { Content = "儲存並更新", Margin = new Thickness(8, 0, 0, 0) };
        save.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsAccentButton);
        DockPanel.SetDock(save, Dock.Right);
        row.Children.Add(save);
        var box = new TextBox { Text = _options.IcsUrl ?? "" };
        box.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsTextBox);
        row.Children.Add(box);
        cp.Children.Add(row);
        _status = new TextBlock { Text = LastStatus, Margin = new Thickness(0, 8, 0, 0) };
        _status.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsCaptionText);
        cp.Children.Add(_status);
        save.Click += (_, _) =>
        {
            _options.IcsUrl = box.Text.Trim();
            _options.Save(_dir);
            _status.Text = string.IsNullOrEmpty(_options.IcsUrl) ? "已清除網址" : "更新中…";
            if (string.IsNullOrEmpty(_options.IcsUrl)) { _events = []; _refreshTimer!.Stop(); Evaluate(); _view?.Refresh(); return; }
            _refreshTimer!.Stop();
            _refreshTimer.Start();
            _ = RefreshAsync();
        };
        card.Child = cp;
        p.Children.Add(card);

        var remindCard = new Border();
        remindCard.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsCard);
        var rp = new StackPanel();
        var rt = new TextBlock { Text = "提前多久提醒" };
        rt.SetResourceReference(FrameworkElement.StyleProperty, IslandTheme.SettingsTitleText);
        rp.Children.Add(rt);
        var choices = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        foreach (var m in new[] { 5, 10, 15, 30 })
        {
            var rb = new RadioButton { Content = $"{m} 分鐘", GroupName = "CalRemind", IsChecked = _options.RemindMinutes == m, Margin = new Thickness(0, 0, 14, 0) };
            if (Application.Current.TryFindResource("Settings.Choice") is Style st) rb.Style = st;
            int minutes = m;
            rb.Checked += (_, _) => { _options.RemindMinutes = minutes; _options.Save(_dir); Evaluate(); };
            choices.Children.Add(rb);
        }
        rp.Children.Add(choices);
        remindCard.Child = rp;
        p.Children.Add(remindCard);
        return p;
    }

    public override void Dispose()
    {
        _refreshTimer?.Stop();
        _reminderTimer?.Stop();
        _minuteTimer?.Stop();
        base.Dispose();
    }
}

/// <summary>提醒時間的純計算(方便測試)。</summary>
public static class CalendarLogic
{
    /// <summary>下一個還沒開始的(非全天)行程。</summary>
    public static CalEvent? NextReminder(IEnumerable<CalEvent> events, DateTime now, TimeSpan remind) =>
        events.Where(e => !e.AllDay && e.Start > now).OrderBy(e => e.Start).FirstOrDefault();

    /// <summary>下一次需要醒來的時間:下個行程的「提醒時刻」,已經在提醒中則是它的開始時刻。</summary>
    public static DateTime? NextWake(IEnumerable<CalEvent> events, DateTime now, TimeSpan remind)
    {
        DateTime? best = null;
        foreach (var e in events)
        {
            if (e.AllDay || e.Start <= now) continue;
            var at = e.Start - remind > now ? e.Start - remind : e.Start;
            if (best is null || at < best) best = at;
        }
        return best;
    }

    /// <summary>"10 分鐘後"、"1 分鐘後"、"即將開始"。</summary>
    public static string Until(DateTime start, DateTime now)
    {
        int minutes = (int)Math.Ceiling((start - now).TotalMinutes);
        return minutes <= 0 ? "即將開始" : $"{minutes} 分鐘後";
    }

    /// <summary>分頁上的時間欄:"14:00–15:00"、"全天"。</summary>
    public static string TimeRange(CalEvent e) => e.AllDay ? "全天" : $"{e.Start:HH:mm}–{e.End:HH:mm}";
}

/// <summary>長條:「📅 10 分鐘後 · 週會」。</summary>
internal sealed class CalendarActivityView : StackPanel
{
    private readonly TextBlock _when = new() { FontSize = 12.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _title = new() { FontSize = 12.5, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };

    public CalendarActivityView()
    {
        Orientation = Orientation.Horizontal;
        HorizontalAlignment = HorizontalAlignment.Center;
        var icon = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        icon.SetResourceReference(TextBlock.ForegroundProperty, IslandTheme.Accent);
        _when.SetResourceReference(TextBlock.ForegroundProperty, IslandTheme.Accent);
        Children.Add(icon);
        Children.Add(_when);
        Children.Add(new TextBlock { Text = " · ", FontSize = 12.5, Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center });
        Children.Add(_title);
    }

    public void Show(CalEvent e, DateTime now)
    {
        _when.Text = CalendarLogic.Until(e.Start, now);
        _title.Text = e.Title;
    }
}

/// <summary>「行程」分頁:今天、明天。</summary>
internal sealed class CalendarView : ScrollViewer
{
    private readonly CalendarModule _module;
    private readonly StackPanel _list = new() { Margin = new Thickness(4, 0, 4, 0) };

    public CalendarView(CalendarModule module)
    {
        _module = module;
        VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        Focusable = false;
        Content = _list;
        IsVisibleChanged += (_, _) => { if (IsVisible) Refresh(); };
        Refresh();
    }

    public void Refresh()
    {
        _list.Children.Clear();
        var now = DateTime.Now;
        if (_module.Events.Count == 0 && _module.LastStatus.StartsWith("還沒"))
        {
            _list.Children.Add(Dim("到 設定 →「行程」貼上日曆網址"));
            return;
        }
        foreach (var (label, day) in new[] { ("今天", now.Date), ("明天", now.Date.AddDays(1)) })
        {
            var events = _module.Events.Where(e => e.Start.Date == day || (e.Start < day && e.End > day)).Where(e => e.AllDay || e.End > now).ToList();
            _list.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)), Margin = new Thickness(0, 4, 0, 2) });
            if (events.Count == 0) { _list.Children.Add(Dim("沒有行程")); continue; }
            foreach (var e in events)
            {
                var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
                var time = new TextBlock { Text = CalendarLogic.TimeRange(e), Width = 84, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
                time.SetResourceReference(TextBlock.ForegroundProperty, IslandTheme.AccentSoft);
                DockPanel.SetDock(time, Dock.Left);
                row.Children.Add(time);
                row.Children.Add(new TextBlock
                {
                    Text = e.Location is null ? e.Title : $"{e.Title} · {e.Location}",
                    FontSize = 12.5,
                    Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                _list.Children.Add(row);
            }
        }
    }

    private static TextBlock Dim(string text) => new() { Text = text, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)), Margin = new Thickness(0, 2, 0, 2) };
}

/// <summary>%LOCALAPPDATA%\Island\modules\calendar\options.json</summary>
internal sealed class CalendarOptions
{
    public string? IcsUrl { get; set; }
    public int RemindMinutes { get; set; } = 10;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static CalendarOptions Load(string dir)
    {
        try
        {
            var file = Path.Combine(dir, "options.json");
            if (File.Exists(file))
            {
                var o = JsonSerializer.Deserialize<CalendarOptions>(File.ReadAllText(file), JsonOptions) ?? new();
                o.RemindMinutes = Math.Clamp(o.RemindMinutes, 1, 120);
                return o;
            }
        }
        catch { }
        return new();
    }

    public void Save(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "options.json"), JsonSerializer.Serialize(this, JsonOptions));
        }
        catch { }
    }
}
