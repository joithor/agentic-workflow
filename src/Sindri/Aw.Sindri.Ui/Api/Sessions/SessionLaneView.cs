namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>The /meta lane a session belongs to.</summary>
/// <param name="Team">Team, as served.</param>
/// <param name="Lane">Lane, as served.</param>
internal sealed record SessionLaneView(string Team, string Lane);
