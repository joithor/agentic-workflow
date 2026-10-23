using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Judging;

namespace Aw.Evidence.Infrastructure.Judging;

/// <summary>
/// Stub (cutover unit 1.3 fills it): the judge through the C# judge CLI, judge &lt;question&gt; with the input record
/// on stdin and the envelope parsed from stdout (docs/csharp/judge.md, 'Questions'). Until then it says it is not wired, as
/// <see cref="UnavailableJudge"/> did, so callers neither call it nor record a model invocation.
/// </summary>
internal sealed class JudgeCliJudge(ICliRunner runner, IExecutableLocator locator, Func<string, string?>? environment = null) : IJudge
{
    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    public ValueTask<SelectorRepair?> RepairSelectorAsync(SelectorRepairRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("the judge CLI adapter is not built yet (cutover unit 1.3)");

    /// <inheritdoc/>
    public ValueTask<VisualVerdict?> CritiqueAsync(VisualCritiqueRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("the judge CLI adapter is not built yet (cutover unit 1.3)");
}
