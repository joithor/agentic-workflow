namespace Aw.Sindri.Ui.Api;

/// <summary>A live session to attach to.</summary>
/// <param name="Target">The agent id.</param>
/// <param name="TabTitle">The viewer's tab title, or null.</param>
/// <param name="Since">When it started.</param>
/// <param name="Argv">The attach command, or null.</param>
internal sealed record LaunchAttachView(string Target, string? TabTitle, DateTimeOffset Since, IReadOnlyList<string>? Argv);
