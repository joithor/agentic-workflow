using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Core.Desktop;

/// <summary>Fills the capture command's placeholders.</summary>
public static class DesktopCommandLine
{
    /// <summary>The flag that introduces a screen to capture: '--shot &lt;route&gt; &lt;png path&gt;'.</summary>
    public const string ShotFlag = "--shot";

    /// <summary>Replaces '{size}' in every element and the element '{shots}' by one '--shot route path' triple per screen.</summary>
    /// <param name="template">The configured command.</param>
    /// <param name="size">The window size, 'WxH'.</param>
    /// <param name="shots">Each screen's route and the PNG path to write.</param>
    /// <returns>The command to run.</returns>
    public static IReadOnlyList<string> Capture(IReadOnlyList<string> template, string size, IReadOnlyList<(string Route, string Path)> shots)
    {
        var command = new List<string>();
        foreach (var element in template)
        {
            if (string.Equals(element, DesktopTargetsRules.ShotsToken, StringComparison.Ordinal))
            {
                foreach (var (route, path) in shots)
                {
                    command.AddRange([ShotFlag, route, path]);
                }
            }
            else
            {
                command.Add(element.Replace(DesktopTargetsRules.SizeToken, size, StringComparison.Ordinal));
            }
        }

        return command;
    }
}
