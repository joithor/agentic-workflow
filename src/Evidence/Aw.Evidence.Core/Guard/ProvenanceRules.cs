namespace Aw.Evidence.Core.Guard;

/// <summary>Combines where a run executed with what the database check said.</summary>
public static class ProvenanceRules
{
    /// <summary>
    /// A preview is a scrubbed extract unless the caller said unknown (keep it local). A local run is only as safe as its
    /// database check; no check is unknown.
    /// </summary>
    /// <param name="kind">Where the run executed.</param>
    /// <param name="database">The database check, if any.</param>
    /// <returns>The provenance.</returns>
    public static Provenance For(HostKind kind, DbProvenance? database) => (kind, database) switch
    {
        (HostKind.Preview, DbProvenance.Unknown) => Provenance.Unknown,
        (HostKind.Preview, _) => Provenance.Scrubbed,
        (_, DbProvenance.Seeded) => Provenance.Seeded,
        _ => Provenance.Unknown,
    };

    /// <summary>A run against anything but this machine never counts as seeded data.</summary>
    /// <param name="host">The origin the run executed against.</param>
    /// <param name="claimed">The provenance the caller claims for the local database.</param>
    /// <returns>The claim for a local host; unknown otherwise.</returns>
    public static DbProvenance Effective(string host, DbProvenance claimed) =>
        HostGuard.IsLocal(host) ? claimed : DbProvenance.Unknown;
}
