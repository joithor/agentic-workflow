namespace Aw.Meta.Core.Messages;

/// <summary>A reply obligation set aside without a reply.</summary>
/// <param name="Kind">Dismissed or deferred.</param>
/// <param name="Reason">Why.</param>
/// <param name="Until">When a deferred obligation reopens.</param>
public sealed record MessageResolution(MessageResolutionKind Kind, string Reason, DateTimeOffset? Until = null);
