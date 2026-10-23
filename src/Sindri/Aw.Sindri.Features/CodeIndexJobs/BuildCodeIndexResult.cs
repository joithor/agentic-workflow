namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>What a code index build job did: counts and words only, never a name, path or text.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Commit">The commit that was published, or null when nothing was.</param>
/// <param name="Outcome">published, not-on-main or deferred (every heavy-job slot stayed busy: nothing changed, and the job is not a failure).</param>
/// <param name="Files">Indexed files.</param>
/// <param name="Symbols">Symbols.</param>
/// <param name="Edges">Resolved edges.</param>
/// <param name="Incremental">True when it started from the live index.</param>
/// <param name="Milliseconds">How long it ran.</param>
public sealed record BuildCodeIndexResult(string Repo, string? Commit, string Outcome, int Files, int Symbols, int Edges, bool Incremental, long Milliseconds)
{
    /// <summary>Layers that are partly built and continue on a later tick (code index spec §6.3): 'embeddings' while some symbols have no vector.</summary>
    public IReadOnlyList<string> LayersPending { get; init; } = [];

    /// <summary>Symbols with a vector after the build; the continuation decision compares it with the build before.</summary>
    public int Embedded { get; init; }

    /// <summary>The outcome of a build that ended because every heavy-job slot stayed busy.</summary>
    public const string Deferred = "deferred";
}
