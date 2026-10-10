namespace Aw.Kernel.Testing;

/// <summary>
/// Golden-file checks for CLI output and JSON contracts (spec §4b.5, §4c). The golden file holds normalized
/// output; set 'AW_GOLDEN_UPDATE=1' to rewrite it from the current output, then review the diff.
/// </summary>
public static class Golden
{
    /// <summary>The environment variable that switches to update mode when it is '1'.</summary>
    public const string UpdateVariable = "AW_GOLDEN_UPDATE";

    /// <summary>Checks <paramref name="actual"/> against the golden file after normalization.</summary>
    /// <param name="actual">The output.</param>
    /// <param name="goldenPath">The golden file.</param>
    /// <param name="normalization">The declared normalization.</param>
    /// <exception cref="GoldenMismatchException">The output differs, or the golden file is missing.</exception>
    public static void Match(string actual, string goldenPath, Normalization normalization) => throw new NotSupportedException("red: Aw.Kernel.Testing C2");

    internal static void Match(string actual, string goldenPath, Normalization normalization, bool update) => throw new NotSupportedException("red: Aw.Kernel.Testing C2");
}
