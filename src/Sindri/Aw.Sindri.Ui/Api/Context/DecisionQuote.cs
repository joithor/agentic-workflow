namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The words a decision was taken from.</summary>
/// <param name="Record">Record, as served.</param>
/// <param name="Text">Text, as served.</param>
/// <param name="Trust">Trust, as served.</param>
internal sealed record DecisionQuote(
    string Record,
    string Text,
    string Trust);
