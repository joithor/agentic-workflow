namespace Aw.Sindri.Ui.Api;

/// <summary>The 'open' target of a needs-you row (q65): the one place the owner acts on it.</summary>
/// <param name="Route">The deep link ("decision/DEC-…", "workflow/&lt;address&gt;", "service/&lt;name&gt;", "recipe/&lt;id&gt;", "needs-you/&lt;rowId&gt;", "setup").</param>
/// <param name="Label">The button's plain words.</param>
/// <param name="Pr">The pull request the row is about; null (omitted) until the API serves one.</param>
/// <param name="Artifact">The artifact the row is about; null (omitted) until the API serves one.</param>
/// <param name="Session">The agent session ("provider/id"); null (omitted) until the API serves one.</param>
/// <param name="ReasonCode">The machine-readable reason; null (omitted) until the API serves one.</param>
internal sealed record NeedsYouOpenView(
    string? Route = null,
    string? Label = null,
    NeedsYouOpenPrView? Pr = null,
    NeedsYouOpenArtifactView? Artifact = null,
    string? Session = null,
    string? ReasonCode = null);

/// <summary>A pull request an 'open' target names.</summary>
/// <param name="Url">Its address.</param>
/// <param name="Number">Its number.</param>
internal sealed record NeedsYouOpenPrView(string? Url = null, int? Number = null);

/// <summary>An artifact an 'open' target names.</summary>
/// <param name="Kind">The kind of artifact.</param>
/// <param name="Fact">The fact it evidences.</param>
/// <param name="Path">Its path, when it has one.</param>
/// <param name="Url">Its address, when it has one.</param>
internal sealed record NeedsYouOpenArtifactView(string? Kind = null, string? Fact = null, string? Path = null, string? Url = null);
