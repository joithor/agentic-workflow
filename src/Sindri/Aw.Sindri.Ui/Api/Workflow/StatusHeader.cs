namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The header line of a status.</summary>
/// <param name="Address">Address, as served.</param>
/// <param name="Recipe">Recipe, as served.</param>
/// <param name="Hash12">Hash12, as served.</param>
/// <param name="Version">The instance's version, the one `--version` takes.</param>
/// <param name="State">State, as served.</param>
/// <param name="Detail">Detail, as served.</param>
/// <param name="Since">Since, as served.</param>
/// <param name="AgeSeconds">AgeSeconds, as served.</param>
internal sealed record StatusHeader(
    string Address,
    string Recipe,
    string Hash12,
    int Version,
    string State,
    string? Detail,
    DateTimeOffset Since,
    long AgeSeconds);
