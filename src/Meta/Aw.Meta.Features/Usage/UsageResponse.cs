namespace Aw.Meta.Features.Usage;

/// <summary>Every subject's standing.</summary>
/// <param name="Subjects">The lanes, then the meta.</param>
/// <param name="Attention">Whether any is limited.</param>
public sealed record UsageResponse(IReadOnlyList<UsageRow> Subjects, bool Attention);
