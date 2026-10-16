namespace Aw.Sindri.Core.Launch;

/// <summary>One agent in the catalog, as far as the session collector reads it (launcher spec SL3; SL-2 adds the launch fields).</summary>
/// <param name="Id">The entry's id.</param>
/// <param name="Label">What a person calls it ('Claude Code').</param>
/// <param name="Bin">The executable the entry launches ('claude').</param>
/// <param name="ProcessName">The name its process runs as, or null (the basename of <paramref name="Bin"/> stands in).</param>
/// <param name="Transcripts">The glob of its transcript files with '{sessionId}', or null.</param>
/// <param name="Resume">The argv template that resumes a session ('{bin}', '{sessionId}'), or null.</param>
public sealed record AgentEntry(AgentId Id, string Label, string Bin, string? ProcessName, string? Transcripts, IReadOnlyList<string>? Resume)
{
    /// <summary>The regex whose first group is the session id in a transcript path (the entry's reportedId.capture), or null.</summary>
    public string? IdCapture { get; init; }

    /// <summary>How the entry takes a Step's model id (launcher L-17); none by default, as for a user-added entry. P10a's addition.</summary>
    public ModelForm ModelForm { get; init; } = ModelForm.None;

    /// <summary>How the entry takes a Step's policy (launcher L-17); none by default. P10a's addition.</summary>
    public PolicyForm PolicyForm { get; init; } = PolicyForm.None;

    /// <summary>Where the entry came from (launcher L-10); a built-in by default. P10a's addition.</summary>
    public CatalogSource Source { get; init; } = CatalogSource.Builtin;

    /// <summary>The hash of the entry's argv template (launcher SL8, L-11), or null until the catalog computes it. P10a's addition.</summary>
    public string? ArgvHash { get; init; }

    /// <summary>The name the entry's processes are matched by: <see cref="ProcessName"/>, else the basename of <see cref="Bin"/>.</summary>
    public string MatchName => ProcessName is { Length: > 0 } name ? name : BaseName(Bin);

    private static string BaseName(string path) => path[(path.LastIndexOf('/') + 1)..];
}
