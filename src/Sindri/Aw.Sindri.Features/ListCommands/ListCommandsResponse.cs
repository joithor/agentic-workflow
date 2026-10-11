namespace Aw.Sindri.Features.ListCommands;

/// <summary>The command catalog as the UI and doctor read it (the spec's CommandList), sorted by action id.</summary>
/// <param name="Rows">One row per action id.</param>
public sealed record ListCommandsResponse(IReadOnlyList<CommandListRow> Rows);
