namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The root-wide head counts.</summary>
/// <param name="Open">Open, as served.</param>
/// <param name="NeedsYou">NeedsYou, as served.</param>
/// <param name="WithYou">WithYou, as served.</param>
/// <param name="Running">Running, as served.</param>
/// <param name="Done">Done, as served.</param>
internal sealed record ListCounts(
    int Open,
    int NeedsYou,
    int WithYou,
    int Running,
    int Done);
