namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The pull request babysitter's own figures in a recipe's stats (GET /workflow/recipes/{id}/stats, only for workflow.pr-babysitter; api #778).</summary>
/// <param name="Pulls">Pull requests it watched in the window.</param>
/// <param name="SinceStartToReadySeconds">Time from start until a pull request was ready for you.</param>
/// <param name="SinceStartToMergeSeconds">Time from start until it merged.</param>
/// <param name="TokensPerPull">Tokens used per pull request.</param>
/// <param name="Comments">What happened to review comments.</param>
/// <param name="CiFix">CI repair attempts.</param>
/// <param name="Rechecks">Rechecks of a red check.</param>
/// <param name="OwnerActionsAtReady">What you did when a pull request was ready.</param>
/// <param name="Docs">How the documentation updates went.</param>
/// <param name="RecordErrors">Merge records that couldn't be read.</param>
internal sealed record BabysitterStatsView(
    int Pulls,
    StatsSpreadView SinceStartToReadySeconds,
    StatsSpreadView SinceStartToMergeSeconds,
    StatsSpreadView TokensPerPull,
    BabysitterCommentsView Comments,
    BabysitterCiFixView CiFix,
    BabysitterRechecksView Rechecks,
    BabysitterOwnerActionsView OwnerActionsAtReady,
    BabysitterDocsView Docs,
    int RecordErrors);
