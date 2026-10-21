namespace Aw.Sindri.Core.Setup;

/// <summary>One repo's line inside a setup step that counts repos.</summary>
/// <param name="Repo">The repo's name.</param>
/// <param name="State">Where it stands.</param>
/// <param name="Detail">What is true now, in plain words, or null.</param>
public sealed record SetupItem(string Repo, SetupState State, string? Detail);
