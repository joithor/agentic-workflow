using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Launches;

/// <summary>Prices and the budget facts (screens spec E-28). Pure.</summary>
public static class Budgets
{
    /// <summary>The price of <paramref name="model"/> in USD per million tokens, matched by the longest id prefix, or null when the table lacks the model.</summary>
    /// <param name="model">The model id a transcript names.</param>
    /// <returns>The price, or null.</returns>
    public static ModelPrice? PriceOf(string model) => throw new NotSupportedException("Budgets.PriceOf: make the budget tests pass");

    /// <summary>Prices the day's usage.</summary>
    /// <param name="usage">The tokens per model.</param>
    /// <returns>The priced spend; tokens only when any model is unpriced.</returns>
    public static AgentSpend Spend(IEnumerable<ModelTokens> usage) => throw new NotSupportedException("Budgets.Spend: make the budget tests pass");

    /// <summary>The budget facts of <paramref name="target"/>.</summary>
    /// <param name="agents">The approved profile's 'agents:' block, or null.</param>
    /// <param name="target">The agent.</param>
    /// <param name="spend">Today's spend, or null when the agent has no usage reader.</param>
    /// <returns>The facts.</returns>
    public static BudgetFacts For(AgentSettings? agents, AgentId target, AgentSpend? spend) => throw new NotSupportedException("Budgets.For: make the budget tests pass");

    /// <summary>The facts over all of <paramref name="targets"/>.</summary>
    /// <param name="agents">The approved profile's 'agents:' block, or null.</param>
    /// <param name="targets">Every target's facts.</param>
    /// <returns>The total.</returns>
    public static BudgetFacts Total(AgentSettings? agents, IReadOnlyList<BudgetFacts> targets) => throw new NotSupportedException("Budgets.Total: make the budget tests pass");
}
