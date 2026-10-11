namespace Aw.Evidence.Core.Usage;

/// <summary>What a model call reported about itself; a null count is unknown, never zero.</summary>
/// <param name="Model">The model.</param>
/// <param name="InputTokens">Input tokens.</param>
/// <param name="OutputTokens">Output tokens.</param>
public sealed record InvocationUsage(string? Model, long? InputTokens, long? OutputTokens);
