using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Island.Core;
using Windows.Media.Control;
using GsmtcManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager;
using GsmtcSession = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;
using PlaybackStatus = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;

namespace Island.Modules.Media;

/// <summary>
/// 正在播放。使用 Windows 的全域媒體控制(和音量提示框裡的媒體卡片同一套),
/// Spotify、瀏覽器(YouTube 等)、媒體播放器、VLC… 只要有接系統媒體控制的都能顯示與控制。
///
/// 效能:完全事件驅動。系統通知「換歌 / 播放狀態 / 時間軸」改變時才更新;
/// 進度條只有在展開且停在這個分頁、而且正在播放時,才每秒更新一次。
/// </summary>
public sealed class MediaModule : IslandModuleBase, IIslandActivity, IIslandExpandAware, IIslandCompact
{
    /// <summary>暫停後多久才算「沒有活動」(換歌時狀態會短暫跳動,避免膠囊閃爍)。</summary>
    private static readonly TimeSpan InactiveGrace = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RefreshDebounce = TimeSpan.FromMilliseconds(60);

    private GsmtcManager? _manager;
    private readonly List<GsmtcSession> _subscribed = [];
    private GsmtcSession? _session;
    private string? _sessionId;

    private DispatcherTimer? _refreshTimer;
    private DispatcherTimer? _graceTimer;
    private DispatcherTimer? _progressTimer;
    private bool _mediaDirty = true;
    private int _mediaVersion;
    private bool _expanded;
    private bool _activityActive;

    private MediaView? _view;
    private readonly SystemVolume _volume = new();
    private bool _volumeOpen;
    private string? _trackKey;   // 用來判斷「換歌」:來源 + 標題 + 歌手
    private static readonly TimeSpan AnnounceDuration = TimeSpan.FromSeconds(3);
    private MediaActivityView? _activityView;
    private MediaCompactView? _compactView;

    public override string Id => "media";
    public override string Title => "播放";

    /// <summary>目前顯示的內容(只在 UI 執行緒讀寫)。</summary>
    public MediaSnapshot Current { get; private set; } = MediaSnapshot.Empty;

    public event EventHandler? ActivityChanged;
    public bool IsActivityActive => _activityActive;

    protected override void OnInitialize()
    {
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = RefreshDebounce };
        _refreshTimer.Tick += (_, _) => { _refreshTimer.Stop(); _ = RefreshAsync(); };
        _graceTimer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = InactiveGrace };
        _graceTimer.Tick += (_, _) => { _graceTimer.Stop(); SetActivity(false); };
        _progressTimer = new DispatcherTimer(DispatcherPriority.Background, Host.Dispatcher) { Interval = TimeSpan.FromSeconds(1) };
        _progressTimer.Tick += (_, _) => _view?.UpdateProgress(Current);

        _volume.ExternalChange += (level, muted) =>
            Host.Dispatcher.BeginInvoke(() => { if (_volumeOpen) _view?.ShowVolume(level, muted, true); });

        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        try
        {
            _manager = await GsmtcManager.RequestAsync();
            _manager.SessionsChanged += (_, _) => ScheduleRefresh(sessionsChanged: true);
            _manager.CurrentSessionChanged += (_, _) => ScheduleRefresh();
            Host.Dispatcher.Invoke(() => ScheduleRefresh(sessionsChanged: true));
        }
        catch (Exception ex)
        {
            Host.Log($"媒體:無法取得系統媒體控制:{ex.Message}");
        }
    }

    public FrameworkElement CreateActivityView() => _activityView ??= new MediaActivityView(Current);

    public bool IsCompactRelevant => _activityActive;

    /// <summary>點小圓「直接執行」(設定 → 即時狀態可以選;預設是打開分頁)。</summary>
    public string? CompactActionLabel => "播放 / 暫停";
    public void RunCompactAction(int index) { TogglePlayPause(); }
    public FrameworkElement CreateCompactView() => _compactView ??= new MediaCompactView(Current);

    public override FrameworkElement CreateExpandedView() => _view ??= new MediaView(this);

    // ── 系統事件 → 去抖動後在 UI 執行緒重新整理 ─────────────

    private bool _resubscribe;

    private void ScheduleRefresh(bool sessionsChanged = false, bool mediaChanged = false)
    {
        // 系統事件來自背景執行緒
        Host.Dispatcher.BeginInvoke(() =>
        {
            _resubscribe |= sessionsChanged;
            _mediaDirty |= mediaChanged;
            _refreshTimer!.Stop();
            _refreshTimer.Start();
        });
    }

    private void Resubscribe()
    {
        foreach (var s in _subscribed)
        {
            s.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            s.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            s.TimelinePropertiesChanged -= OnTimelineChanged;
        }
        _subscribed.Clear();
        if (_manager is null) return;

        // 訂閱所有工作階段的播放狀態:任何一個開始播放都能立刻切過去
        foreach (var s in _manager.GetSessions())
        {
            s.PlaybackInfoChanged += OnPlaybackInfoChanged;
            s.MediaPropertiesChanged += OnMediaPropertiesChanged;
            s.TimelinePropertiesChanged += OnTimelineChanged;
            _subscribed.Add(s);
        }
    }

    private void OnPlaybackInfoChanged(GsmtcSession s, PlaybackInfoChangedEventArgs e) => ScheduleRefresh();
    private void OnMediaPropertiesChanged(GsmtcSession s, MediaPropertiesChangedEventArgs e) => ScheduleRefresh(mediaChanged: true);

    /// <summary>
    /// 播放中瀏覽器大約每 4～5 秒就會送一次時間軸更新。進度只有展開時才看得到,
    /// 所以收合時直接忽略(展開時會整個重新讀一次),閒置時不被喚醒。
    /// </summary>
    private void OnTimelineChanged(GsmtcSession s, TimelinePropertiesChangedEventArgs e)
    {
        if (Volatile.Read(ref _expandedFlag) != 0) ScheduleRefresh();
    }

    private int _expandedFlag;   // 給背景執行緒讀的 _expanded

    /// <summary>選出要顯示的工作階段:優先「正在播放」的,其次系統認定的目前工作階段。</summary>
    private GsmtcSession? PickSession()
    {
        if (_manager is null) return null;
        var sessions = _manager.GetSessions();
        foreach (var s in sessions)
        {
            try
            {
                if (s.GetPlaybackInfo()?.PlaybackStatus == PlaybackStatus.Playing) return s;
            }
            catch { /* 工作階段可能剛好關閉 */ }
        }
        // 目前顯示的那個還在的話維持不變(暫停時不要跳到別的)
        if (_sessionId is not null)
        {
            var same = sessions.FirstOrDefault(s => s.SourceAppUserModelId == _sessionId);
            if (same is not null) return same;
        }
        return _manager.GetCurrentSession();
    }

    private async Task RefreshAsync()
    {
        try
        {
            if (_resubscribe)
            {
                _resubscribe = false;
                Resubscribe();
            }

            var session = PickSession();
            var id = session?.SourceAppUserModelId;
            if (id != _sessionId)
            {
                _sessionId = id;
                _mediaDirty = true;
            }
            _session = session;

            if (session is null)
            {
                Apply(MediaSnapshot.Empty);
                return;
            }

            var snapshot = Current with { AppId = id ?? "", AppName = FriendlyAppName(id) };
            snapshot = ReadPlayback(session, snapshot);

            if (_mediaDirty)
            {
                _mediaDirty = false;
                int version = ++_mediaVersion;
                var props = await session.TryGetMediaPropertiesAsync();
                if (version != _mediaVersion) return;   // 等待期間又換歌了,交給下一次
                var thumb = await LoadThumbnailAsync(props?.Thumbnail);
                if (version != _mediaVersion) return;
                var albumColor = AlbumColor.Extract(thumb as BitmapSource);
                Host.Log($"媒體:{props?.Title} 封面主色 {(albumColor?.ToString() ?? "無(灰階/無封面)")}");
                snapshot = ReadPlayback(session, snapshot with
                {
                    Title = props?.Title ?? "",
                    Artist = string.IsNullOrEmpty(props?.Artist) ? props?.AlbumArtist ?? "" : props!.Artist,
                    Thumbnail = thumb,
                    AlbumColor = albumColor,
                });
            }

            Apply(snapshot);
            AnnounceIfTrackChanged(snapshot);
        }
        catch (Exception ex)
        {
            Host.Log($"媒體:更新失敗:{ex.Message}");
        }
    }

    private static MediaSnapshot ReadPlayback(GsmtcSession session, MediaSnapshot s)
    {
        var info = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        var controls = info?.Controls;
        return s with
        {
            HasSession = true,
            IsPlaying = info?.PlaybackStatus == PlaybackStatus.Playing,
            CanPrevious = controls?.IsPreviousEnabled ?? false,
            CanNext = controls?.IsNextEnabled ?? false,
            CanSeek = controls?.IsPlaybackPositionEnabled ?? false,
            CanPlayPause = (controls?.IsPlayPauseToggleEnabled ?? false)
                           || (controls?.IsPlayEnabled ?? false) || (controls?.IsPauseEnabled ?? false),
            Position = timeline is null ? TimeSpan.Zero : timeline.Position - timeline.StartTime,
            Duration = timeline is null ? TimeSpan.Zero : timeline.EndTime - timeline.StartTime,
            PositionUpdatedAt = timeline?.LastUpdatedTime ?? DateTimeOffset.Now,
        };
    }

    private static async Task<ImageSource?> LoadThumbnailAsync(Windows.Storage.Streams.IRandomAccessStreamReference? reference)
    {
        if (reference is null) return null;
        try
        {
            using var winStream = await reference.OpenReadAsync();
            using var stream = winStream.AsStreamForRead();
            var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            buffer.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 160;          // 只需要小圖,省記憶體
            image.StreamSource = buffer;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private void Apply(MediaSnapshot snapshot)
    {
        Current = snapshot;
        UpdateAccent();
        _view?.Update(snapshot);
        _activityView?.Update(snapshot);
        _compactView?.Update(snapshot);
        UpdateProgressTimer();

        bool active = snapshot.HasSession && snapshot.IsPlaying && !string.IsNullOrEmpty(snapshot.Title);
        if (active)
        {
            _graceTimer!.Stop();
            SetActivity(true);
        }
        else if (_activityActive && !_graceTimer!.IsEnabled)
        {
            _graceTimer.Start();
        }
    }

    /// <summary>換歌(而且正在播放)時讓膠囊短暫出現。啟動時讀到的第一首不算。</summary>
    private void AnnounceIfTrackChanged(MediaSnapshot s)
    {
        if (string.IsNullOrEmpty(s.Title)) return;
        var key = $"{s.AppId}|{s.Title}|{s.Artist}";
        if (key == _trackKey) return;
        bool first = _trackKey is null;
        _trackKey = key;
        if (!first && s.IsPlaying) Host.Announce(Id, AnnounceDuration);
    }

    private void SetActivity(bool active)
    {
        if (_activityActive == active) return;
        _activityActive = active;
        UpdateAccent();
        Host.Log(active ? $"媒體:開始播放 {Current.AppName}" : "媒體:停止播放");
        ActivityChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>播放中(含暫停後的短暫緩衝)用專輯主色當整個島的強調色;否則回到中性。</summary>
    private void UpdateAccent()
    {
        Color? accent = _activityActive || Current.IsPlaying ? Current.AlbumColor : null;
        if (Nullable.Equals(accent, _accent)) return;
        _accent = accent;
        Host.SetAccent(accent);
    }

    private Color? _accent;

    // ── 控制 ─────────────────────────────────────────────

    /// <summary>跳到整首的某個比例(0~1)。畫面先樂觀更新,系統之後會送時間軸通知校正。</summary>
    public async void Seek(double fraction)
    {
        var session = _session;
        if (session is null || Current.Duration <= TimeSpan.Zero) return;
        try
        {
            var timeline = session.GetTimelineProperties();
            var target = TimeSpan.FromTicks((long)(Current.Duration.Ticks * Math.Clamp(fraction, 0, 1)));
            Current = Current with { Position = target, PositionUpdatedAt = DateTimeOffset.Now };
            _view?.UpdateProgress(Current);
            bool ok = await session.TryChangePlaybackPositionAsync((timeline.StartTime + target).Ticks);
            if (!ok) Host.Log("媒體:來源拒絕跳轉");
        }
        catch (Exception ex)
        {
            Host.Log($"媒體:跳轉失敗:{ex.Message}");
        }
    }

    public void TogglePlayPause() => Run(s => s.TryTogglePlayPauseAsync().AsTask());
    public void Next() => Run(s => s.TrySkipNextAsync().AsTask());
    public void Previous() => Run(s => s.TrySkipPreviousAsync().AsTask());

    private async void Run(Func<GsmtcSession, Task<bool>> action)
    {
        var session = _session;
        if (session is null) return;
        try { await action(session); }
        catch (Exception ex) { Host.Log($"媒體:控制失敗:{ex.Message}"); }
    }

    // ── 展開時才更新進度條 ───────────────────────────────

    // ── 音量(系統主音量) ───────────────────────────────

    /// <summary>設定音量(0~1)。調整音量時順便解除靜音。回傳目前是否靜音。</summary>
    public bool SetVolume(float level)
    {
        if (!_volumeOpen) return false;
        _volume.Level = level;
        if (_volume.Muted && level > 0) _volume.Muted = false;
        return _volume.Muted;
    }

    /// <summary>切換靜音,回傳切換後是否靜音。</summary>
    public bool ToggleMute()
    {
        if (!_volumeOpen) return false;
        _volume.Muted = !_volume.Muted;
        return _volume.Muted;
    }

    public void OnExpanded()
    {
        _expanded = true;
        _volumeOpen = _volume.Open();   // 每次重新取得預設裝置,插拔耳機後也正確
        _view?.ShowVolume(_volume.Level, _volume.Muted, _volumeOpen);
        Volatile.Write(ref _expandedFlag, 1);
        _view?.Update(Current);
        UpdateProgressTimer();
        ScheduleRefresh();   // 收合期間略過了時間軸更新,展開時重新讀一次
    }

    public void OnCollapsed()
    {
        _expanded = false;
        _volumeOpen = false;
        _volume.Close();
        Volatile.Write(ref _expandedFlag, 0);
        UpdateProgressTimer();
    }

    private void UpdateProgressTimer()
    {
        bool run = _expanded && Current.IsPlaying && Current.Duration > TimeSpan.Zero;
        if (run && !_progressTimer!.IsEnabled) _progressTimer.Start();
        else if (!run) _progressTimer?.Stop();
    }

    private static string FriendlyAppName(string? appId)
    {
        if (string.IsNullOrEmpty(appId)) return "";
        var lower = appId.ToLowerInvariant();
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("chrome")) return "Chrome";
        if (lower.Contains("msedge")) return "Edge";
        if (lower.Contains("firefox")) return "Firefox";
        if (lower.Contains("zunemusic")) return "媒體播放器";
        if (lower.Contains("applemusic") || lower.Contains("appleinc.")) return "Apple Music";
        if (lower.Contains("vlc")) return "VLC";
        var name = appId.Split('!')[0].Split('_')[0];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        var dot = name.LastIndexOf('.');
        return dot >= 0 && dot < name.Length - 1 ? name[(dot + 1)..] : name;
    }

    public override void Dispose()
    {
        _refreshTimer?.Stop();
        _graceTimer?.Stop();
        _progressTimer?.Stop();
        _volume.Dispose();
        _manager = null;
        _subscribed.Clear();
        base.Dispose();
    }
}

/// <summary>某一刻的播放資訊(不可變)。</summary>
public sealed record MediaSnapshot
{
    public static readonly MediaSnapshot Empty = new();

    public bool HasSession { get; init; }
    public string AppId { get; init; } = "";
    public string AppName { get; init; } = "";
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public ImageSource? Thumbnail { get; init; }
    /// <summary>封面代表色;黑白封面為 null。</summary>
    public Color? AlbumColor { get; init; }
    public bool IsPlaying { get; init; }
    public bool CanPrevious { get; init; }
    public bool CanNext { get; init; }
    public bool CanPlayPause { get; init; }
    /// <summary>來源是否允許跳轉播放位置。</summary>
    public bool CanSeek { get; init; }
    public TimeSpan Position { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTimeOffset PositionUpdatedAt { get; init; }

    /// <summary>依「上次更新的位置 + 經過時間」推算現在的位置(系統不會每秒通知)。</summary>
    public TimeSpan EstimatedPosition
    {
        get
        {
            var pos = Position;
            if (IsPlaying) pos += DateTimeOffset.Now - PositionUpdatedAt;
            if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
            if (Duration > TimeSpan.Zero && pos > Duration) pos = Duration;
            return pos;
        }
    }
}
