using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// Windows' taskbar badge: ITaskbarList3::SetOverlayIcon on the main window, a small red dot with the count as its
/// description for screen readers, none at zero. The COM object is called through its vtable (AOT-safe, no marshalling of
/// interfaces). With no window yet, or when the shell has no taskbar, the count is still kept and nothing is drawn.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe partial class WindowsDockBadge(Func<nint> window) : IDockBadge
{
    private const int HrInitSlot = 3;
    private const int SetOverlayIconSlot = 18;
    private const int DotSize = 16;
    private nint _list;
    private nint _icon;

    public int Shown { get; private set; }

    public void Show(int count)
    {
        count = Math.Max(count, 0);
        Shown = count;
        var handle = window();
        if (handle == 0 || Taskbar() is not { } list)
        {
            return;
        }

        var setOverlay = (delegate* unmanaged[Stdcall]<nint, nint, nint, char*, int>)(*(nint**)list)[SetOverlayIconSlot];
        if (count == 0)
        {
            _ = setOverlay(list, handle, 0, null);
            return;
        }

        _icon = _icon == 0 ? Dot() : _icon;
        var description = count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " need you";
        fixed (char* text = description)
        {
            _ = setOverlay(list, handle, _icon, text);
        }
    }

    private nint? Taskbar()
    {
        if (_list != 0)
        {
            return _list;
        }

        var clsid = new Guid(0x56FDF344, 0xFD6D, 0x11d0, 0x95, 0x8A, 0x00, 0x60, 0x97, 0xC9, 0xA0, 0x90);
        var iid = new Guid(0xEA1AFB91, 0x9E28, 0x4B86, 0x90, 0xE9, 0x9E, 0x9F, 0x8A, 0x5E, 0xEF, 0xAF);
        if (CoCreateInstance(in clsid, 0, 1, in iid, out var created) != 0 || created == 0)
        {
            return null;
        }

        var init = (delegate* unmanaged[Stdcall]<nint, int>)(*(nint**)created)[HrInitSlot];
        if (init(created) != 0)
        {
            return null;
        }

        _list = created;
        return _list;
    }

    // A filled red circle, 16 x 16, from ARGB pixels and an all-opaque mask.
    private static nint Dot()
    {
        var pixels = new uint[DotSize * DotSize];
        var mask = new byte[DotSize * DotSize / 8];
        for (var y = 0; y < DotSize; y++)
        {
            for (var x = 0; x < DotSize; x++)
            {
                var dx = x - 7.5;
                var dy = y - 7.5;
                if ((dx * dx) + (dy * dy) <= 49)
                {
                    pixels[(y * DotSize) + x] = 0xFFD92D20;
                }
            }
        }

        fixed (uint* color = pixels)
        fixed (byte* bits = mask)
        {
            var info = new IconInfo { IsIcon = 1, Mask = CreateBitmap(DotSize, DotSize, 1, 1, bits), Color = CreateBitmap(DotSize, DotSize, 1, 32, color) };
            var icon = CreateIconIndirect(in info);
            _ = DeleteObject(info.Mask);
            _ = DeleteObject(info.Color);
            return icon;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        public int IsIcon;
        public int HotspotX;
        public int HotspotY;
        public nint Mask;
        public nint Color;
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint created);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateBitmap(int width, int height, uint planes, uint bitsPerPixel, void* bits);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint handle);

    [LibraryImport("user32.dll")]
    private static partial nint CreateIconIndirect(in IconInfo info);
}
