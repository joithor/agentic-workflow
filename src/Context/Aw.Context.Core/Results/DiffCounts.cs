namespace Aw.Context.Core.Results;

/// <summary>The changed files by place in the map; Changed is always Mapped + Suggested + Unmapped + Denied, whatever was cut from the lists.</summary>
/// <param name="Changed">Every changed path.</param>
/// <param name="Mapped">Those with a confirmed 'implements' edge.</param>
/// <param name="Suggested">Those with only unconfirmed ones.</param>
/// <param name="Unmapped">Those no edge reaches.</param>
/// <param name="Denied">Those under the repo's deny or secret globs: counted, never named.</param>
public sealed record DiffCounts(int Changed, int Mapped, int Suggested, int Unmapped, int Denied);
