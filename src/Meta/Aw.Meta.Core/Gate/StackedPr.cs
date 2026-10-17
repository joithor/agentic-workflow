namespace Aw.Meta.Core.Gate;

/// <summary>A pull request merged under a stacked gate.</summary>
/// <param name="Pr">The pull request number.</param>
/// <param name="Head">The head that was merged.</param>
public sealed record StackedPr(int Pr, string Head);
