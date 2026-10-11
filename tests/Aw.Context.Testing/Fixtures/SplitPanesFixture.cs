namespace Aw.Context.Testing.Fixtures;

/// <summary>
/// The split-panes fixture (spec §10): its embedded files (paths relative to 'Fixtures/split-panes/', '/'-separated),
/// its history and its probes. See that folder's README for what each record is for.
/// </summary>
public static class SplitPanesFixture
{
    /// <summary>The Linear team key every fixture issue carries.</summary>
    public const string TeamKey = "SPL";

    /// <summary>Every embedded file, ordinal order.</summary>
    public static IReadOnlyList<string> ResourcePaths => throw new NotSupportedException("red: list the embedded fixture files");

    /// <summary>The repo history from 'history.json'.</summary>
    public static FixtureHistory History => throw new NotSupportedException("red: read history.json");

    /// <summary>The probes from 'probes.json'.</summary>
    public static IReadOnlyList<Probe> Probes => throw new NotSupportedException("red: read probes.json");

    /// <summary>Reads an embedded file as UTF-8 text.</summary>
    /// <param name="path">A path from <see cref="ResourcePaths"/>.</param>
    /// <returns>The text.</returns>
    public static string ReadText(string path) => throw new NotSupportedException($"red: read {path}");
}
