namespace Aw.Context.Core.Models;

/// <summary>A model's answer, with the tokens it cost (for the labeling caps).</summary>
/// <param name="Text">The answer text (untrusted until a human accepts what it produces).</param>
/// <param name="InputTokens">Tokens sent.</param>
/// <param name="OutputTokens">Tokens received.</param>
public sealed record ModelAnswer(string Text, int InputTokens, int OutputTokens);
