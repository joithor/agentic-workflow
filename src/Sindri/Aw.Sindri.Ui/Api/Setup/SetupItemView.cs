namespace Aw.Sindri.Ui.Api.Setup;

/// <summary>One repo's line inside a step that counts repos.</summary>
/// <param name="Repo">The repo's name.</param>
/// <param name="State">Where it stands, in the step's states.</param>
/// <param name="Detail">What is true now in plain words, or null.</param>
internal sealed record SetupItemView(string Repo, string State, string? Detail);
