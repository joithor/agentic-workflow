namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>One entry's fault.</summary>
/// <param name="Index">The entry's 0-based index.</param>
/// <param name="Kind">What is wrong.</param>
/// <param name="Detail">The outcome text for <see cref="ChoiceFaultKind.BadName"/>, the length for <see cref="ChoiceFaultKind.BadLength"/>, the JSON kind for <see cref="ChoiceFaultKind.NotText"/>; empty otherwise.</param>
public sealed record ChoiceFault(int Index, ChoiceFaultKind Kind, string Detail);
