namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>A source the collector couldn't read in its last pass.</summary>
/// <param name="Source">Source, as served.</param>
/// <param name="Code">Code, as served (SND-SESSION-001 and the like).</param>
/// <param name="Text">Text, as served.</param>
internal sealed record SessionProblemView(string Source, string Code, string Text);
