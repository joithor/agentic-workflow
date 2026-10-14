namespace Aw.Context.Core.Results;

/// <summary>A typed disagreement (D10): code is the truth for behavior, decisions for intent. Never cut by the budget.</summary>
/// <param name="Node">The node they disagree about.</param>
/// <param name="Intent">Citation markers on the intent side.</param>
/// <param name="Behavior">Citation markers on the behavior side.</param>
/// <param name="Summary">One line on the disagreement.</param>
public sealed record ResultConflict(string Node, IReadOnlyList<string> Intent, IReadOnlyList<string> Behavior, string Summary);
