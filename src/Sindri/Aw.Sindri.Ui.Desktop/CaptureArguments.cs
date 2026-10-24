using System.Globalization;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// Capture mode's command line: '--capture --size WxH --shot &lt;route&gt; &lt;png path&gt; [--shot ...]'. Strictly parsed: an unknown flag, a missing value, a
/// repeated size or no shot is a usage error (null from <see cref="Parse"/>).
/// </summary>
internal static class CaptureArguments
{
    internal const string Flag = "--capture";
    internal const string Usage = "sindri-ui --capture --size WxH --shot <route> <png path> [--shot <route> <png path> ...]   (route: page:<Page> or a deep link such as item/ITM-01JE7)";

    private const int Largest = 8192;

    /// <summary>True when the command line asks for capture mode.</summary>
    /// <param name="args">The command line.</param>
    /// <returns>Whether the first argument is '--capture'.</returns>
    internal static bool Asked(IReadOnlyList<string> args) => args.Count > 0 && args[0] == Flag;

    /// <summary>Reads capture mode's command line.</summary>
    /// <param name="args">The command line, starting with '--capture'.</param>
    /// <param name="report">Where a problem during the capture is reported.</param>
    /// <returns>The request, or null for a usage mistake.</returns>
    internal static CaptureRequest? Parse(IReadOnlyList<string> args, Action<string> report)
    {
        (int, int)? size = null;
        List<CaptureShot> shots = [];
        for (var i = 1; i < args.Count; i++)
        {
            if (args[i] == "--size" && size is null && i + 1 < args.Count && SizeOf(args[i + 1]) is { } parsed)
            {
                size = parsed;
                i++;
            }
            else if (args[i] == "--shot" && i + 2 < args.Count && args[i + 1].Length > 0 && args[i + 2].Length > 0)
            {
                shots.Add(new CaptureShot(args[i + 1], args[i + 2]));
                i += 2;
            }
            else
            {
                return null;
            }
        }

        return size is { } found && shots.Count > 0 ? new CaptureRequest(found.Item1, found.Item2, shots, report) : null;
    }

    private static (int, int)? SizeOf(string text)
    {
        var parts = text.Split('x');
        return parts is [var w, var h]
            && int.TryParse(w, NumberStyles.None, CultureInfo.InvariantCulture, out var width) && int.TryParse(h, NumberStyles.None, CultureInfo.InvariantCulture, out var height)
            && width is >= 1 and <= Largest && height is >= 1 and <= Largest
            ? (width, height)
            : null;
    }
}
