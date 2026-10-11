using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// macOS's reduce-motion setting: [NSWorkspace sharedWorkspace].accessibilityDisplayShouldReduceMotion, through the
/// Objective-C runtime (objc_msgSend with blittable arguments, AOT-safe). AppKit is loaded first, so it also reads outside
/// the app (in tests).
/// </summary>
[SupportedOSPlatform("macos")]
internal static partial class MacMotion
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";

    /// <summary>Reads the setting.</summary>
    /// <returns>True when the user asks for reduced motion; false when it can't be read.</returns>
    internal static bool Read()
    {
        _ = NativeLibrary.TryLoad(AppKit, out _);
        var workspaceClass = GetClass("NSWorkspace");
        if (workspaceClass == 0)
        {
            return false;
        }

        var workspace = SendPointer(workspaceClass, RegisterName("sharedWorkspace"));
        return workspace != 0 && SendByte(workspace, RegisterName("accessibilityDisplayShouldReduceMotion")) != 0;
    }

    [LibraryImport(ObjC, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint GetClass(string name);

    [LibraryImport(ObjC, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint RegisterName(string name);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial nint SendPointer(nint receiver, nint selector);

    // A BOOL return: a signed char on x86-64, a bool on arm64; either way the low byte is 0 or 1.
    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial byte SendByte(nint receiver, nint selector);
}
