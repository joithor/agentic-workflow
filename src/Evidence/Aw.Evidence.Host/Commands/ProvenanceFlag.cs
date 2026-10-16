using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Host.Commands;

/// <summary>Binds a 'seeded|unknown' flag value.</summary>
internal static class ProvenanceFlag
{
    /// <summary>Absent is fine (null); 'seeded' or 'unknown' bind; anything else is a usage error.</summary>
    internal static bool TryParse(string? text, out DbProvenance? provenance)
    {
        switch (text)
        {
            case null:
                provenance = null;
                return true;
            case "seeded":
                provenance = DbProvenance.Seeded;
                return true;
            case "unknown":
                provenance = DbProvenance.Unknown;
                return true;
            default:
                provenance = null;
                return false;
        }
    }
}
