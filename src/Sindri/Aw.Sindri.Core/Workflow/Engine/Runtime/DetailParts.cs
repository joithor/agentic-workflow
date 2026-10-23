using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The parts of the instance detail that join in what only the read service knows (q65).</summary>
internal static class DetailParts
{
    /// <summary>The desk form with each review's path and url filled from the targets read for it; the form as it is without any.</summary>
    public static DeskForm? WithTargets(DeskForm? desk, IReadOnlyDictionary<string, DeskReviewTarget>? targets) =>
        desk is { Reviews: { } reviews } ? desk with { Reviews = DeskReviewTargets.Fill(reviews, targets) } : desk;

    /// <summary>The status with the open agent run's session; a status with no agent run has no run to carry it.</summary>
    public static StatusFields WithSession(StatusFields status, SessionHandle? session) =>
        status is { Run: { } run } && session is not null ? status with { Run = run with { Session = session } } : status;
}
