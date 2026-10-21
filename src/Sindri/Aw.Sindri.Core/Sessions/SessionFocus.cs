namespace Aw.Sindri.Core.Sessions;

/// <summary>Whether Focus can bring a session forward (collector spec CA-1). Focus ships in CO-4a; until then every row says it can't, and why.</summary>
/// <param name="Method">The method focus would use, or 'none'.</param>
/// <param name="Possible">True when focus can run.</param>
/// <param name="Why">Why not, when it can't; null otherwise.</param>
public sealed record SessionFocus(string Method, bool Possible, string? Why)
{
    /// <summary>What every row says until CO-4a: the stable code 'not-built' (the catalog's FocusSession row carries the phase label 'pending:CO-4a').</summary>
    public static SessionFocus Pending { get; } = new("none", Possible: false, "not-built");
}
