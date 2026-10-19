namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>The launch row behind a session: the workflow Step an engine launch started it for. The members are null for an attended (human-started) launch.</summary>
/// <param name="Instance">The workflow instance address, ITM-…/step/N, or null.</param>
/// <param name="State">The state the Step is in, or null.</param>
/// <param name="Attempt">Which attempt of the Step this launch is, or null.</param>
internal sealed record SessionLaunchView(string? Instance, string? State, int? Attempt);
