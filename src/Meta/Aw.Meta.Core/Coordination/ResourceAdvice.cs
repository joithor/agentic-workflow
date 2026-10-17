using Aw.Meta.Core.Health;

namespace Aw.Meta.Core.Coordination;

/// <summary>The heavy slots and the builders advice.</summary>
/// <param name="Heavy">The heavy reservation; null when none was computed or the team turned it off.</param>
/// <param name="Builders">The builders per lane.</param>
public sealed record ResourceAdvice(HeavySlots? Heavy, BuilderAdvice Builders);
