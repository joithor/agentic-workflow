namespace Aw.CodeIndex.Core.Planning;

/// <summary>The structure layer's stamp: what changes the rows a parse stores (spec §5.2).</summary>
public static class StructureStamp
{
    /// <summary>Builds the stamp from the parser's id and the settings that shape stored rows.</summary>
    /// <param name="parserId">The parser version.</param>
    /// <param name="utilityGlobs">The profile's utility globs.</param>
    /// <param name="scrubPatterns">The profile's extra scrub patterns.</param>
    /// <returns>The parser id and the first 8 hex digits of a hash of the two lists; order and repeats don't matter.</returns>
    public static string Compute(string parserId, IReadOnlyList<string> utilityGlobs, IReadOnlyList<string> scrubPatterns) => throw new NotSupportedException();
}
