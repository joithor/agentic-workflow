namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The size of the whole map.</summary>
/// <param name="Nodes">Every node of the map.</param>
/// <param name="Edges">Every edge of the map.</param>
/// <param name="ByKind">Nodes per kind (feature, surface, action, thing, flow, decision, record, code).</param>
internal sealed record WholeCounts(int Nodes, int Edges, IReadOnlyDictionary<string, int> ByKind);
