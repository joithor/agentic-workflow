using Aw.Kernel.Json;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The terminals' process names from config/launcher/terminals.json, embedded in this assembly (collector spec CO-2b): each row's 'id' and 'processNames'.</summary>
internal sealed class TerminalProcessNames : ITerminalNames
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(LauncherJsonContext.Default);

    private TerminalProcessNames(IReadOnlyList<TerminalName> all)
    {
        All = all;
    }

    public IReadOnlyList<TerminalName> All { get; }

    /// <summary>Reads the embedded file.</summary>
    /// <returns>The terminals.</returns>
    internal static TerminalProcessNames Embedded()
    {
        using var stream = typeof(TerminalProcessNames).Assembly.GetManifestResourceStream("terminals.json")
            ?? throw new InvalidOperationException("terminals.json isn't embedded in Aw.Sindri.Infrastructure");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return Parse(copy.ToArray());
    }

    /// <summary>Reads terminals.json text.</summary>
    /// <param name="utf8">The file.</param>
    /// <returns>The terminals; none when the text is not the expected shape.</returns>
    internal static TerminalProcessNames Parse(byte[] utf8)
    {
        var document = AwJson.Deserialize<TerminalTemplatesFile>(utf8, _json);
        return new TerminalProcessNames(document.IsSuccess
            ? [.. document.Value.Terminals.Select(static t => new TerminalName(t.Id, t.ProcessNames ?? []))]
            : []);
    }
}
