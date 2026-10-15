namespace Aw.Context.Infrastructure.Extract;

/// <summary>How graphify is found and held.</summary>
/// <param name="Pin">The version this build accepts (sindri/package.json's graphifyPin).</param>
/// <param name="Binary">The graphify command name or absolute path.</param>
/// <param name="Timeout">How long one run may take.</param>
/// <param name="MaxGraphBytes">The most graph.json bytes read.</param>
internal sealed record GraphifyOptions(string Pin, string Binary, TimeSpan Timeout, long MaxGraphBytes)
{
    /// <summary>The shipped options.</summary>
    public static GraphifyOptions Default { get; } = new("0.9.67", "graphify", TimeSpan.FromMinutes(10), 128L * 1024 * 1024);
}
