namespace Aw.Sindri.Features.GetItemScope;

/// <summary>A starting point: a node key and the map version its lens read it at.</summary>
/// <param name="Key">The node key ('feature:tabs.tab-manager', 'code:src/a.ts').</param>
/// <param name="MapVersion">'v&lt;N&gt;'.</param>
public sealed record ScopeAnchor(string Key, string MapVersion);
