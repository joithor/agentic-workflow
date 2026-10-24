namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One repo the whole map holds nodes of.</summary>
/// <param name="Name">The repo's short name.</param>
/// <param name="NodeCount">How many nodes of the map belong to it.</param>
internal sealed record WholeRepo(string Name, int NodeCount);
