using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Kernel.Data;

/// <summary>
/// Loads migrations embedded as resources named '&lt;prefix&gt;NNNN_name.sql', for example
/// 'Aw.Judge.Infrastructure.Migrations.0001_decisions.sql'. Reading manifest resources is AOT-safe.
/// </summary>
public static partial class EmbeddedMigrations
{
    /// <summary>Loads every '.sql' resource under <paramref name="resourcePrefix"/>, ordered by version.</summary>
    /// <param name="assembly">The assembly holding the resources.</param>
    /// <param name="resourcePrefix">The resource name prefix, including the trailing dot.</param>
    /// <returns>The migrations, or AW-DATA-005 for none found or a resource name without a version.</returns>
    public static Result<IReadOnlyList<Migration>> Load(Assembly assembly, string resourcePrefix)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var migrations = new List<Migration>();
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(resourcePrefix, StringComparison.Ordinal) || !resource.EndsWith(".sql", StringComparison.Ordinal))
            {
                continue;
            }

            var file = resource[resourcePrefix.Length..];
            var match = Match(file);
            if (match is null)
            {
                return DataErrors.MigrationSequence.With($"Migration resource '{file}' is not named NNNN_name.sql.");
            }

            migrations.Add(new Migration(match.Value.Version, match.Value.Name, Read(assembly, resource)));
        }

        // A typo in the prefix, or .sql files not marked EmbeddedResource, would otherwise migrate nothing and succeed.
        return migrations.Count == 0
            ? DataErrors.MigrationSequence.With($"No migration resources under '{resourcePrefix}': check the prefix and that the .sql files are EmbeddedResource items.")
            : migrations.OrderBy(m => m.Version).ToList();
    }

    private static (int Version, string Name)? Match(string file)
    {
        try
        {
            var match = FilePattern.Match(file);
            return match.Success
                ? (int.Parse(match.Groups["version"].Value, NumberStyles.None, CultureInfo.InvariantCulture), match.Groups["name"].Value)
                : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null; // a timeout never passes (spec §4c)
        }
    }

    private static string Read(Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Resource '{resource}' is listed but can't be opened.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    // Explicit ASCII classes and CultureInvariant, with a match timeout (spec §4c, regex parity).
    [GeneratedRegex("^(?<version>[0-9]{4})_(?<name>[A-Za-z0-9_]+)\\.sql$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex FilePattern { get; }
}
