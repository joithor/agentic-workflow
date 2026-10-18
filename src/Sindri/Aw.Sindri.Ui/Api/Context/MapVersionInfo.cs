namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A map version.</summary>
/// <param name="Number">Number, as served.</param>
/// <param name="Repo">Repo, as served.</param>
/// <param name="Commit">Commit, as served.</param>
/// <param name="KeyVersion">KeyVersion, as served.</param>
/// <param name="Created">Created, as served.</param>
/// <param name="Status">Status, as served.</param>
internal sealed record MapVersionInfo(
    int Number,
    string Repo,
    string? Commit,
    int KeyVersion,
    DateTimeOffset Created,
    string Status);
