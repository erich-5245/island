using System.Runtime.InteropServices;

namespace Island.Modules.Osd;

/// <summary>
/// 監看系統主音量(預設播放裝置)。完全事件驅動:
/// IAudioEndpointVolume 的變更通知(按音量鍵、在任何地方調音量、靜音)+
/// IMMNotificationClient 的「預設裝置改變」(插拔耳機)時重新掛到新的裝置。
/// 通知來自系統的背景執行緒。
/// </summary>
internal sealed class AudioWatcher : IDisposable
{
    private readonly object _gate = new();
    private IMMDeviceEnumerator? _enumerator;
    private DeviceClient? _deviceClient;
    private IAudioEndpointVolume? _endpoint;
    private VolumeCallback? _callback;
    private bool _disposed;

    /// <summary>音量 0~1、是否靜音。</summary>
    public event Action<float, bool>? Changed;

    public bool Start()
    {
        try
        {
            _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            _deviceClient = new DeviceClient(this);
            _enumerator.RegisterEndpointNotificationCallback(_deviceClient);
            OpenDefault();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void OpenDefault()
    {
        lock (_gate)
        {
            if (_disposed || _enumerator is null) return;
            CloseEndpoint();
            try
            {
                if (_enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out var device) != 0) return;
                var iid = typeof(IAudioEndpointVolume).GUID;
                if (device.Activate(ref iid, 0x17, IntPtr.Zero, out var obj) == 0)
                {
                    _endpoint = (IAudioEndpointVolume)obj;
                    _callback = new VolumeCallback(this);
                    _endpoint.RegisterControlChangeNotify(_callback);
                }
                Marshal.ReleaseComObject(device);
            }
            catch
            {
                CloseEndpoint();
            }
        }
    }

    private void CloseEndpoint()
    {
        if (_endpoint is null) return;
        try { if (_callback is not null) _endpoint.UnregisterControlChangeNotify(_callback); } catch { }
        Marshal.ReleaseComObject(_endpoint);
        _endpoint = null;
        _callback = null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            CloseEndpoint();
            if (_enumerator is not null)
            {
                try { if (_deviceClient is not null) _enumerator.UnregisterEndpointNotificationCallback(_deviceClient); } catch { }
                Marshal.ReleaseComObject(_enumerator);
                _enumerator = null;
            }
        }
    }

    private sealed class VolumeCallback(AudioWatcher owner) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(IntPtr data)
        {
            try
            {
                // AUDIO_VOLUME_NOTIFICATION_DATA:GUID guidEventContext; BOOL bMuted; float fMasterVolume; ...
                bool muted = Marshal.ReadInt32(data, 16) != 0;
                float level = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(data, 20));
                owner.Changed?.Invoke(level, muted);
            }
            catch { }
            return 0;
        }
    }

    private sealed class DeviceClient(AudioWatcher owner) : IMMNotificationClient
    {
        public int OnDefaultDeviceChanged(int flow, int role, string? deviceId)
        {
            // 不要在系統的通知執行緒裡做 COM 呼叫,丟到執行緒池
            if (flow == 0 && role == 1) ThreadPool.QueueUserWorkItem(_ => owner.OpenDefault());
            return 0;
        }
        public int OnDeviceStateChanged(string? deviceId, int newState) => 0;
        public int OnDeviceAdded(string? deviceId) => 0;
        public int OnDeviceRemoved(string? deviceId) => 0;
        public int OnPropertyValueChanged(string? deviceId, PropertyKey key) => 0;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
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
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct PropertyKey
{
    public Guid FormatId;
    public int PropertyId;
}

/// <summary>音量變更通知。必須是 public:COM 只能對公開介面做 QueryInterface。</summary>
[ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAudioEndpointVolumeCallback
{
    [PreserveSig] int OnNotify(IntPtr notifyData);
}

/// <summary>音訊裝置變更通知(預設裝置切換)。必須是 public。</summary>
[ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMMNotificationClient
{
    [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string? deviceId, int newState);
    [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string? deviceId);
    [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string? deviceId);
    [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string? deviceId);
    [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string? deviceId, PropertyKey key);
}
