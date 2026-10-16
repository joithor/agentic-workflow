namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A parsed choice list: the entries that parse and the faults of those that don't.</summary>
/// <param name="Entries">The good entries, in order.</param>
/// <param name="Faults">The faults, in order.</param>
internal sealed record ChoiceParse(IReadOnlyList<Choice> Entries, IReadOnlyList<ChoiceFault> Faults);
