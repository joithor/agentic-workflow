using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Host.Commands;

/// <summary>Binds a 'seeded|unknown' flag value.</summary>
internal static class ProvenanceFlag
{
    /// <summary>Absent is fine (null); 'seeded' or 'unknown' bind; anything else is a usage error.</summary>
    internal static bool TryParse(string? text, out DbProvenance? provenance)
    {
        provenance = text switch
        {
            "seeded" => DbProvenance.Seeded,
            "unknown" => DbProvenance.Unknown,
            _ => null,
        };
        return text is null || provenance is not null;
    }
}
