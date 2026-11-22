using System.Runtime.InteropServices;

namespace Island.Modules.Media;

/// <summary>
/// 系統主音量(預設播放裝置),透過 Core Audio 的 IAudioEndpointVolume。
/// 只在展開播放分頁時使用:每次開啟都重新取得預設裝置(插拔耳機後會自動跟上),
/// 開啟期間註冊變更通知(用鍵盤音量鍵調整時滑桿會跟著動),收合時取消註冊。
/// </summary>
internal sealed class SystemVolume : IDisposable
{
    /// <summary>自己調整音量時帶的識別碼,收到通知時用來忽略自己造成的變更。</summary>
    private static readonly Guid OwnContext = new("5B2B6D0C-6B5A-4C55-9A41-0D3C6E2B7A11");

    private IAudioEndpointVolume? _endpoint;
    private Callback? _callback;

    /// <summary>音量或靜音被「其他地方」改變時觸發(背景執行緒)。參數:音量 0~1、是否靜音。</summary>
    public event Action<float, bool>? ExternalChange;

    public bool Open()
    {
        Close();
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            try
            {
                Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device));
                var iid = typeof(IAudioEndpointVolume).GUID;
                Marshal.ThrowExceptionForHR(device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out var obj));
                _endpoint = (IAudioEndpointVolume)obj;
                Marshal.ReleaseComObject(device);
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }

            _callback = new Callback(this);
            _endpoint.RegisterControlChangeNotify(_callback);
            return true;
        }
        catch
        {
            Close();
            return false;
        }
    }

    public float Level
    {
        get => _endpoint is not null && _endpoint.GetMasterVolumeLevelScalar(out var v) == 0 ? v : 0;
        set
        {
            if (_endpoint is null) return;
            var ctx = OwnContext;
            _endpoint.SetMasterVolumeLevelScalar(Math.Clamp(value, 0f, 1f), ref ctx);
        }
    }

    public bool Muted
    {
        get => _endpoint is not null && _endpoint.GetMute(out var m) == 0 && m;
        set
        {
            if (_endpoint is null) return;
            var ctx = OwnContext;
            _endpoint.SetMute(value, ref ctx);
        }
    }

    public void Close()
    {
        if (_endpoint is not null)
        {
            if (_callback is not null)
            {
                try { _endpoint.UnregisterControlChangeNotify(_callback); } catch { }
            }
            Marshal.ReleaseComObject(_endpoint);
            _endpoint = null;
        }
        _callback = null;
    }

    public void Dispose() => Close();

    private sealed class Callback(SystemVolume owner) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(IntPtr pNotify)
        {
            try
            {
                // AUDIO_VOLUME_NOTIFICATION_DATA:GUID guidEventContext; BOOL bMuted; float fMasterVolume; ...
                var context = Marshal.PtrToStructure<Guid>(pNotify);
                if (context == OwnContext) return 0;
                bool muted = Marshal.ReadInt32(pNotify, 16) != 0;
                float level = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(pNotify, 20));
                owner.ExternalChange?.Invoke(level, muted);
            }
            catch { /* 通知失敗不影響播放 */ }
            return 0;
        }
    }

    // ── Core Audio COM 定義 ─────────────────────────────

    private const int CLSCTX_ALL = 0x17;

    private enum EDataFlow { eRender = 0 }
    private enum ERole { eMultimedia = 1 }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object anInterface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}

/// <summary>
/// Core Audio 的音量變更通知介面。必須是 public:COM 只能對公開介面做 QueryInterface,
/// 否則系統拿不到我們的回呼。
/// </summary>
[ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioEndpointVolumeCallback
{
    [PreserveSig] int OnNotify(IntPtr notifyData);
}
