using System.Globalization;
using System.Reflection;

namespace Aw.Kernel.Testing;

/// <summary>
/// Golden-file checks for CLI output and JSON contracts (spec §4b.5, §4c). The golden file holds normalized
/// output; set 'AW_GOLDEN_UPDATE=1' to rewrite it from the current output, then review the diff.
/// </summary>
public static class Golden
{
    /// <summary>The environment variable that switches to update mode when it is '1'.</summary>
    public const string UpdateVariable = "AW_GOLDEN_UPDATE";

    /// <summary>
    /// The assembly-metadata key that holds a test project's source directory. Declare it in the test project:
    /// '&lt;AssemblyMetadata Include="AwGoldenSourceRoot" Value="$(MSBuildProjectDirectory)" /&gt;'.
    /// </summary>
    public const string SourceRootKey = "AwGoldenSourceRoot";

    /// <summary>
    /// The path of a golden file in the test project's source tree, so update mode rewrites the committed file
    /// rather than a copy in the build output.
    /// </summary>
    /// <param name="testAssembly">The test assembly; it declares <see cref="SourceRootKey"/>.</param>
    /// <param name="relativePath">The golden file, relative to the test project directory.</param>
    /// <returns>The absolute path.</returns>
    /// <exception cref="InvalidOperationException">The assembly doesn't declare its source root.</exception>
    public static string SourcePath(Assembly testAssembly, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(testAssembly);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        var root = testAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, SourceRootKey, StringComparison.Ordinal))?.Value;
        return string.IsNullOrEmpty(root)
            ? throw new InvalidOperationException(
                $"{testAssembly.GetName().Name} doesn't declare its source root. Add "
                + $"<AssemblyMetadata Include=\"{SourceRootKey}\" Value=\"$(MSBuildProjectDirectory)\" /> to its project file.")
            : Path.Combine(root, relativePath);
    }

    /// <summary>Checks <paramref name="actual"/> against the golden file after normalization.</summary>
    /// <param name="actual">The output.</param>
    /// <param name="goldenPath">The golden file.</param>
    /// <param name="normalization">The declared normalization.</param>
    /// <exception cref="GoldenMismatchException">The output differs, or the golden file is missing.</exception>
    public static void Match(string actual, string goldenPath, Normalization normalization) =>
        Match(actual, goldenPath, normalization, string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "1", StringComparison.Ordinal));

    internal static void Match(string actual, string goldenPath, Normalization normalization, bool update)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentException.ThrowIfNullOrEmpty(goldenPath);
        ArgumentNullException.ThrowIfNull(normalization);
        var normalized = normalization.Apply(actual);
        if (update)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(goldenPath));
            if (dir is not null)
            {
                _ = Directory.CreateDirectory(dir);
            }

            File.WriteAllText(goldenPath, normalized);
            return;
        }

        if (!File.Exists(goldenPath))
        {
            throw new GoldenMismatchException(
                $"Golden file {goldenPath} is missing. Run with {UpdateVariable}=1 to create it from this output, then review the diff.");
        }

        var expected = normalization.Apply(File.ReadAllText(goldenPath));
        if (!string.Equals(expected, normalized, StringComparison.Ordinal))
        {
            throw new GoldenMismatchException(Describe(goldenPath, expected, normalized));
        }
    }

    private static string Describe(string goldenPath, string expected, string actual)
    {
        var want = expected.Split('\n');
        var got = actual.Split('\n');
        var line = 0;
        while (line < want.Length && line < got.Length && string.Equals(want[line], got[line], StringComparison.Ordinal))
        {
            line++;
        }

        var wantLine = line < want.Length ? want[line] : "<end of file>";
        var gotLine = line < got.Length ? got[line] : "<end of file>";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{goldenPath} differs at line {line + 1} (after normalization).\nexpected: {wantLine}\nactual:   {gotLine}\nRun with {UpdateVariable}=1 to accept the new output, then review the diff.");
    }
}
