using System.Globalization;

namespace Aw.Sindri.Ui.Capture;

/// <summary>
/// What the capture was asked for: '--size WxH' and one or more '--shot &lt;route&gt; &lt;png path&gt;'. Strictly parsed: an unknown flag, a missing value, a
/// repeated size or no shot is a usage error (null from <see cref="Parse"/>).
/// </summary>
/// <param name="Width">The window's width, in pixels.</param>
/// <param name="Height">The window's height, in pixels.</param>
/// <param name="Shots">Each screen's route and the PNG path to write.</param>
internal sealed record CaptureArguments(int Width, int Height, IReadOnlyList<CaptureShot> Shots)
{
    internal const string Usage = "sindri-ui-capture --size WxH --shot <route> <png path> [--shot <route> <png path> ...]   (route: page:<Page> or a deep link such as item/ITM-01JE7)";

    private const int Largest = 8192;

    internal static CaptureArguments? Parse(IReadOnlyList<string> args)
    {
        (int, int)? size = null;
        List<CaptureShot> shots = [];
        for (var i = 0; i < args.Count; i++)
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

        return size is { } found && shots.Count > 0 ? new CaptureArguments(found.Item1, found.Item2, shots) : null;
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

/// <summary>One screen to capture.</summary>
/// <param name="Route">'page:&lt;Page&gt;' or a deep link of the app.</param>
/// <param name="Path">Where to write the PNG.</param>
internal sealed record CaptureShot(string Route, string Path);
