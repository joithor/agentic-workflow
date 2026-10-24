using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>
/// The 'desktop' part of the targets config: how desktop-run seeds the app's data, captures its screens and where the repo's
/// screens manifest is. Nothing in it names an app: the commands are the repo's own.
/// </summary>
/// <param name="Manifest">The screens manifest (docs: ui-evidence.desktop-screens/1), relative to the repo.</param>
/// <param name="HomeEnv">The variable that carries the scratch directory the fixture and capture commands keep their state in.</param>
/// <param name="Fixture">The command that builds and tears down the seeded data.</param>
/// <param name="Capture">The command that renders the app's screens to PNGs.</param>
/// <param name="Sizes">The window sizes to capture, 'WxH'; default 1440x900 and 900x900.</param>
/// <param name="Threshold">The default limit of differing pixels, in percent; default 10.</param>
/// <param name="TimeoutSeconds">How long each command may run; default 300.</param>
/// <param name="Env">Extra environment for every command (literal values, never secrets).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesktopTargets(
    string Manifest,
    string HomeEnv,
    DesktopFixture Fixture,
    DesktopCapture Capture,
    IReadOnlyList<string>? Sizes = null,
    double? Threshold = null,
    int? TimeoutSeconds = null,
    IReadOnlyDictionary<string, string>? Env = null)
{
    /// <summary>The limit of differing pixels when neither the config nor a screen sets one, in percent.</summary>
    public const double DefaultThreshold = 10;

    /// <summary>How long each command may run when the config sets no limit.</summary>
    public const int DefaultTimeoutSeconds = 300;

    /// <summary>The window sizes captured when the config names none.</summary>
    public static IReadOnlyList<string> DefaultSizes { get; } = ["1440x900", "900x900"];
}

/// <summary>The seeded-data command: a program and the arguments of each step, run in the repo with the scratch directory in the home variable.</summary>
/// <param name="Command">The program and its leading arguments; a relative program is the repo's.</param>
/// <param name="Up">The steps that build the data, each a list of arguments appended to <paramref name="Command"/>, run in order.</param>
/// <param name="Down">The arguments that tear it down (run last, even after a failure); null runs nothing.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesktopFixture(IReadOnlyList<string> Command, IReadOnlyList<IReadOnlyList<string>> Up, IReadOnlyList<string>? Down = null);

/// <summary>The capture command: it renders the app's screens, headless, at one window size per run.</summary>
/// <param name="Command">The program and its arguments; '{size}' becomes 'WxH' and the element '{shots}' becomes '--shot &lt;route&gt; &lt;png path&gt;' for each screen.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesktopCapture(IReadOnlyList<string> Command);
