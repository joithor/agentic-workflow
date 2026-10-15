namespace Aw.Meta.Core.Obligations;

/// <summary>One open obligation.</summary>
/// <param name="Kind">What kind.</param>
/// <param name="Key">Identifies it within its kind: a message id, a map version, a short sha, a question number.</param>
/// <param name="Summary">What is owed, in a line.</param>
/// <param name="NextCommand">The exact next step that closes it.</param>
public sealed record Obligation(ObligationKind Kind, string Key, string Summary, string NextCommand);
