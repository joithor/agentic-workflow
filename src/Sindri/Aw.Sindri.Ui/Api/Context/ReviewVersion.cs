namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The draft's version.</summary>
/// <param name="Number">Number, as served.</param>
/// <param name="Commit">Commit, as served.</param>
internal sealed record ReviewVersion(
    int Number,
    string? Commit);
