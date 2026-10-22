namespace Aw.Sindri.Ui.Api.CodeIndex;

/// <summary>One repo's index.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="State">'missing', 'ok' or 'damaged' (later 'stale', 'building' or 'waiting'); a word this app doesn't know is shown as served.</param>
/// <param name="Commit">The commit the index describes, or null.</param>
/// <param name="BuiltAt">When it was built, or null.</param>
/// <param name="Layers">Each layer's state.</param>
/// <param name="Counts">Files, symbols and edges, or null.</param>
internal sealed record CodeIndexRepoView(string Repo, string State, string? Commit, DateTimeOffset? BuiltAt, IReadOnlyList<CodeIndexLayerView> Layers, CodeIndexCountsView? Counts = null);
