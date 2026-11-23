using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Island.Modules.Media;

/// <summary>
/// 系統輸出的音量峰值(Core Audio IAudioMeterInformation)。只讀一個數字,不錄音、不擷取聲音內容。
/// 只在律動條看得到時開啟,隱藏時立即釋放。
/// </summary>
internal sealed class AudioMeter : IDisposable
{
    private IAudioMeterInformation? _meter;

    public bool Open()
    {
        if (_meter is not null) return true;
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            try
            {
                if (enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out var device) != 0) return false;
                var iid = typeof(IAudioMeterInformation).GUID;
                int hr = device.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out var obj);
                Marshal.ReleaseComObject(device);
                if (hr != 0) return false;
                _meter = (IAudioMeterInformation)obj;
                return true;
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>0–1;讀不到時是 0。</summary>
    public float Peak()
    {
        if (_meter is null) return 0;
        try { return _meter.GetPeakValue(out var v) == 0 ? Math.Clamp(v, 0, 1) : 0; }
        catch { return 0; }
    }

    public void Close()
    {
        if (_meter is null) return;
        try { Marshal.ReleaseComObject(_meter); } catch { }
        _meter = null;
    }

    public void Dispose() => Close();

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object anInterface);
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float peak);
    }
}

/// <summary>
/// 音樂律動:長條右邊 4 根跟著音量跳動的小柱子(取代原本的喇叭圖示)。
/// 效能:每秒 20 次,只在畫面看得到時跑;隱藏時計時器停止、音量計關閉,不耗任何資源。
/// 4 根柱子用同一個峰值,配上不同相位的起伏,看起來像頻譜但不需要擷取聲音。
/// </summary>
internal sealed class AudioBars : FrameworkElement
{
    private const int Count = 4;
    private const double BarWidth = 2.5, Gap = 2, MaxHeight = 13, MinHeight = 2.5;

    private readonly AudioMeter _meter = new();
    private readonly DispatcherTimer _timer;
    private readonly double[] _heights = new double[Count];
    private double _level;
    private double _phase;
    private Brush _brush = Brushes.White;

    public AudioBars()
    {
        Width = Count * BarWidth + (Count - 1) * Gap;
        Height = MaxHeight;
        VerticalAlignment = VerticalAlignment.Center;
        for (int i = 0; i < Count; i++) _heights[i] = MinHeight;
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) => Step();
        IsVisibleChanged += (_, _) => UpdateRunning();
        SetResourceReference(BrushProperty, Island.Core.IslandTheme.Accent);
    }

    public static readonly DependencyProperty BrushProperty = DependencyProperty.Register(
        nameof(Brush), typeof(Brush), typeof(AudioBars),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, e) => ((AudioBars)d)._brush = (Brush)e.NewValue));

    public Brush Brush
    {
        get => (Brush)GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    private bool _playing = true;

    /// <summary>暫停時柱子降到最低並停止取樣。</summary>
    public void SetPlaying(bool playing)
    {
        _playing = playing;
        UpdateRunning();
    }

    private void UpdateRunning()
    {
        bool run = IsVisible && _playing;
        if (run == _timer.IsEnabled) return;
        if (run)
        {
            _meter.Open();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _meter.Close();
            for (int i = 0; i < Count; i++) _heights[i] = MinHeight;
            _level = 0;
            InvalidateVisual();
        }
    }

    private void Step()
    {
        // 峰值 → 感覺上比較線性的大小(平方根),上升快、下降慢
        double target = Math.Sqrt(_meter.Peak());
        _level = target > _level ? _level + (target - _level) * 0.6 : _level + (target - _level) * 0.25;
        _phase += 0.55;
        for (int i = 0; i < Count; i++)
        {
            double wobble = 0.55 + 0.45 * Math.Abs(Math.Sin(_phase * (0.8 + i * 0.37) + i * 1.3));
            double h = MinHeight + (MaxHeight - MinHeight) * Math.Clamp(_level * wobble * 1.15, 0, 1);
            _heights[i] = _heights[i] + (h - _heights[i]) * 0.5;
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        for (int i = 0; i < Count; i++)
        {
            double h = _heights[i];
            double x = i * (BarWidth + Gap);
            dc.DrawRoundedRectangle(_brush, null, new Rect(x, (MaxHeight - h) / 2, BarWidth, h), BarWidth / 2, BarWidth / 2);
        }
    }
}
