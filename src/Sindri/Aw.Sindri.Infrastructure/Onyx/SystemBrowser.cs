using System.ComponentModel;
using System.Diagnostics;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>IBrowser through the OS opener (`open` on macOS, `xdg-open` elsewhere), asked once with the address as its only argument and not waited for. Only a loopback http address is opened.</summary>
internal sealed class SystemBrowser : IBrowser
{
    /// <inheritdoc />
    public bool Open(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (url.Scheme != Uri.UriSchemeHttp || url.Host != "127.0.0.1")
        {
            return false;
        }

        try
        {
            var start = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open", [url.AbsoluteUri])
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(start);
            return process is not null;
        }
        catch (Win32Exception)
        {
            return false;   // no opener on this box: the caller prints the address
        }
    }
}
