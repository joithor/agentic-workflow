namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What GET /sindri/context/code is asked.</summary>
/// <param name="Anchors">One to 20 anchor keys.</param>
/// <param name="Draft">Read the draft map.</param>
/// <param name="MapVersion">A map version to read, or null for the live one.</param>
internal sealed record CodeQuery(IReadOnlyList<string> Anchors, bool Draft, string? MapVersion);
