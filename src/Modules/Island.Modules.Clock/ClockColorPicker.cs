using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Island.Modules.Clock;

/// <summary>(和外殼同一個做法)Windows 內建的「色彩」對話方塊(comdlg32 ChooseColor),不需要額外套件。</summary>
internal static class ClockColorPicker
{
    private const int CC_RGBINIT = 0x1;
    private const int CC_FULLOPEN = 0x2;

    [StructLayout(LayoutKind.Sequential)]
    private struct CHOOSECOLOR
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public int rgbResult;
        public IntPtr lpCustColors;
        public int Flags;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
    }

    [DllImport("comdlg32.dll", EntryPoint = "ChooseColorW")]
    private static extern bool ChooseColor(ref CHOOSECOLOR cc);

    /// <summary>自訂色格子在這次執行期間保留。</summary>
    private static readonly int[] Custom = new int[16];

    public static Color? Pick(Window owner, Color initial)
    {
        var handle = GCHandle.Alloc(Custom, GCHandleType.Pinned);
        try
        {
            var cc = new CHOOSECOLOR
            {
                lStructSize = Marshal.SizeOf<CHOOSECOLOR>(),
                hwndOwner = new WindowInteropHelper(owner).Handle,
                rgbResult = initial.R | (initial.G << 8) | (initial.B << 16),
                lpCustColors = handle.AddrOfPinnedObject(),
                Flags = CC_RGBINIT | CC_FULLOPEN,
            };
            if (!ChooseColor(ref cc)) return null;
            int v = cc.rgbResult;
            return Color.FromRgb((byte)v, (byte)(v >> 8), (byte)(v >> 16));
        }
        finally
        {
            handle.Free();
        }
    }
}
