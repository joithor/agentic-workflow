namespace Aw.Context.Core.Engine;

/// <summary>
/// One recognizer the profile adds ('context.extract.recognizers[i]', S-SK): the files whose path matches <see cref="Glob"/> are read
/// (never a denied path), and each match of <see cref="Pattern"/> names the surface 'surface:&lt;kind&gt;/&lt;value of the keyFrom group&gt;'.
/// Checked by <see cref="SettingsReader"/>; what it finds is suggested, never confirmed.
/// </summary>
/// <param name="kind">A surface kind (<see cref="SurfaceKindList.All"/>).</param>
/// <param name="glob">A repo-relative glob.</param>
/// <param name="pattern">A .NET regex with a named group.</param>
/// <param name="keyFrom">The name of that group.</param>
public sealed class ProfileRecognizer(string kind, string glob, string pattern, string keyFrom)
{
    /// <summary>The surface kind of what it finds.</summary>
    public string Kind { get; } = kind;

    /// <summary>The repo-relative glob of the files it reads.</summary>
    public string Glob { get; } = glob;

    /// <summary>The regex.</summary>
    public string Pattern { get; } = pattern;

    /// <summary>The regex group whose value becomes the surface name.</summary>
    public string KeyFrom { get; } = keyFrom;
}
