namespace Aw.Sindri.Ui.Api.CodeIndex;

/// <summary>GET /sindri/code-index/status: one row per repo in the caller's scope (code-index spec §7.3).</summary>
/// <param name="Repos">The repos' indexes.</param>
internal sealed record CodeIndexStatusView(IReadOnlyList<CodeIndexRepoView> Repos);

/// <summary>One repo's index.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="State">'missing', 'ok' or 'damaged' (later 'stale', 'building' or 'waiting'); a word this app doesn't know is shown as served.</param>
/// <param name="Commit">The commit the index describes, or null.</param>
/// <param name="BuiltAt">When it was built, or null.</param>
/// <param name="Layers">Each layer's state.</param>
/// <param name="Counts">Files, symbols and edges, or null.</param>
internal sealed record CodeIndexRepoView(string Repo, string State, string? Commit, DateTimeOffset? BuiltAt, IReadOnlyList<CodeIndexLayerView> Layers, CodeIndexCountsView? Counts = null);

/// <summary>One layer of an index.</summary>
/// <param name="Layer">'structure', 'clones', 'deps', 'graph' or 'embeddings'.</param>
/// <param name="State">'ok', 'unavailable', 'disabled' or 'pending'.</param>
/// <param name="Code">The SND-CODEIDX code when it is unavailable, or null.</param>
/// <param name="Detail">A short detail; embeddings say "N of M embedded".</param>
internal sealed record CodeIndexLayerView(string Layer, string State, string? Code, string Detail);

/// <summary>What an index holds.</summary>
/// <param name="Files">Files.</param>
/// <param name="Symbols">Symbols.</param>
/// <param name="Edges">Edges.</param>
internal sealed record CodeIndexCountsView(int Files, int Symbols, int Edges);
