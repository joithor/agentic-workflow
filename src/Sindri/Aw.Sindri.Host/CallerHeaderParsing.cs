using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;

namespace Aw.Sindri.Host;

/// <summary>Reads the caller headers (API spec §6, plan G12): pure, so every case is a unit test.</summary>
[SuppressMessage("Design", "MA0182:Unused internal type", Justification = "Red stub: the caller middleware in the next commit uses it.")]
internal static class CallerHeaderParsing
{
    /// <summary>Parses the four caller header values (each null when the header is missing).</summary>
    internal static Result<ParsedCaller> Parse(string? caller, string? terminal, string? client, string? repo) =>
        throw new NotSupportedException("red: CallerHeaderParsing.Parse");
}
