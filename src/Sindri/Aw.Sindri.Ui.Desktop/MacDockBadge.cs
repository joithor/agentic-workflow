using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// macOS's dock badge: [[NSApplication sharedApplication] dockTile] setBadgeLabel:, through the Objective-C runtime
/// (objc_msgSend with blittable arguments, AOT-safe, as <see cref="MacMotion"/>). The label is the count as text; zero
/// clears it (a nil label). AppKit is loaded first, so it also runs outside the app (in tests).
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed partial class MacDockBadge : IDockBadge
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";

    public int Shown { get; private set; }

    public void Show(int count)
    {
        count = Math.Max(count, 0);
        _ = NativeLibrary.TryLoad(AppKit, out _);
        var application = SendPointer(GetClass("NSApplication"), RegisterName("sharedApplication"));
        var tile = application == 0 ? 0 : SendPointer(application, RegisterName("dockTile"));
        if (tile == 0)
        {
            return;
        }

        var label = count == 0 ? 0 : SendText(GetClass("NSString"), RegisterName("stringWithUTF8String:"), count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SendPointerArgument(tile, RegisterName("setBadgeLabel:"), label);
        Shown = count;
    }

    [LibraryImport(ObjC, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint GetClass(string name);

    [LibraryImport(ObjC, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint RegisterName(string name);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial nint SendPointer(nint receiver, nint selector);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint SendText(nint receiver, nint selector, string text);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial void SendPointerArgument(nint receiver, nint selector, nint argument);
}
