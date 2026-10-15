namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>Builds the list row (<see cref="InstanceSummary"/>) from an <see cref="InstanceView"/> (UI screens E-20).</summary>
public static class InstanceSummaries
{
    /// <summary>The longest 'now' text, ellipsis included.</summary>
    public const int NowLength = 80;

    /// <summary>The summary of <paramref name="view"/>.</summary>
    /// <param name="view">The instance view.</param>
    /// <returns>The row: the view's identity, recipe, state, layout cells, and its 'next' cut to <see cref="NowLength"/>.</returns>
    public static InstanceSummary Of(InstanceView view) => throw new NotSupportedException();
}
