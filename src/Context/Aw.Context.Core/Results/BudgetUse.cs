namespace Aw.Context.Core.Results;

/// <summary>The token budget of a result.</summary>
/// <param name="Asked">The budget asked for (after the cap).</param>
/// <param name="Used">The tokens the packed items take.</param>
/// <param name="Estimate">True: tokens are estimated as UTF-8 characters / 4.</param>
public sealed record BudgetUse(int Asked, int Used, bool Estimate);
