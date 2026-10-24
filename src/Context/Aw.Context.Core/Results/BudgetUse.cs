namespace Aw.Context.Core.Results;

/// <summary>The size of a result.</summary>
/// <param name="Asked">Always 0: no budget is asked any more.</param>
/// <param name="Used">The tokens the packed items take.</param>
/// <param name="Estimate">True: tokens are estimated as UTF-8 characters / 4.</param>
public sealed record BudgetUse(int Asked, int Used, bool Estimate);
