namespace Aw.Evidence.Core.Usage;

/// <summary>One model call (or verdict-cache hit standing in for one), for the cost baseline. Null numbers are unknown, never zero.</summary>
/// <param name="Phase">Why the model was called.</param>
/// <param name="Model">The model, when known.</param>
/// <param name="ElapsedMs">Wall time, when measured.</param>
/// <param name="Ok">Whether the call produced a usable answer.</param>
/// <param name="InputTokens">Input tokens, when reported.</param>
/// <param name="OutputTokens">Output tokens, when reported.</param>
/// <param name="CacheHit">True when a cached verdict answered instead of a model.</param>
public sealed record ModelInvocation(
    InvocationPhase Phase,
    string? Model,
    long? ElapsedMs,
    bool Ok,
    long? InputTokens,
    long? OutputTokens,
    bool CacheHit);
