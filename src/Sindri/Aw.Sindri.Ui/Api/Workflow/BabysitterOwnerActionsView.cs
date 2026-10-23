namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>What you did at the moment a pull request was ready.</summary>
/// <param name="Merge">Merged it.</param>
/// <param name="Rework">Sent it back for rework.</param>
/// <param name="Comments">Dealt with comments.</param>
/// <param name="ResyncDocs">Asked for the docs to be redone.</param>
/// <param name="Check">Ran a check.</param>
internal sealed record BabysitterOwnerActionsView(int Merge, int Rework, int Comments, int ResyncDocs, int Check);
