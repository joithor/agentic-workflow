namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>A Step's merged injection plan, by metadata only.</summary>
/// <param name="Entries">The sections in the order they were first filled.</param>
/// <param name="Warnings">The replacements the merge made.</param>
public sealed record InjectionPlan(IReadOnlyList<InjectionEntry> Entries, IReadOnlyList<InjectionWarning> Warnings)
{
    /// <summary>No sections.</summary>
    public static InjectionPlan Empty { get; } = new([], []);
}
