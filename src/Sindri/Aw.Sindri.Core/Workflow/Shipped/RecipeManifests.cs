using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Core.Workflow.Shipped;

/// <summary>Builds a <see cref="RecipeManifest"/> from a recipe set (setup-once spec §5.3), with the registry's own hash function: the reader's.</summary>
public static class RecipeManifests
{
    private const string EvalsDirectory = "evals/";

    /// <summary>The manifest of <paramref name="files"/>.</summary>
    /// <param name="files">The recipe set: flat <c language="text">*.yaml</c> files and <c language="text">evals/&lt;recipe&gt;/*.jsonl</c>.</param>
    /// <param name="reader">The recipe reader; its hash is the registry's.</param>
    /// <returns>The manifest.</returns>
    public static RecipeManifest Build(IReadOnlyList<RecipeSetFile> files, IRecipeReader reader)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(reader);
        var evals = files.Where(static f => f.RelativePath.StartsWith(EvalsDirectory, StringComparison.Ordinal)).ToList();
        var recipes = new List<ManifestEntry>();
        var invalid = new List<ManifestInvalid>();
        foreach (var file in files.Where(static f => IsRecipe(f.RelativePath)).OrderBy(static f => f.RelativePath, StringComparer.Ordinal))
        {
            var reading = reader.Read(Encoding.UTF8.GetString(file.Bytes.Span), file.RelativePath);
            var error = reading.Findings.FirstOrDefault(static f => f.Severity == Validation.FindingSeverity.Error);
            if (reading.Document is not { } document || error is not null)
            {
                invalid.Add(new ManifestInvalid(file.RelativePath, error?.ToLine() ?? "it doesn't bind"));
                continue;
            }

            var mine = evals.Where(e => e.RelativePath.StartsWith($"{EvalsDirectory}{document.Recipe.Id.Value}/", StringComparison.Ordinal)).ToList();
            if (document.Recipe.DisplayName is null)
            {
                invalid.Add(new ManifestInvalid(file.RelativePath, "RCP-DISPLAY-001: the recipe has no displayName"));
            }

            recipes.Add(new ManifestEntry(document.Recipe.Id, document.Recipe.Version, document.Hash.Value, document.Recipe.DisplayName, mine.Count, EvalsHash(mine)));
        }

        recipes.Sort(static (a, b) => string.CompareOrdinal(a.Id.Value, b.Id.Value));
        return new RecipeManifest(recipes, invalid, invalid.Count == 0 ? Digest(recipes) : null);
    }

    private static bool IsRecipe(string path) => !path.Contains('/', StringComparison.Ordinal) && path.EndsWith(".yaml", StringComparison.Ordinal);

    /// <summary>The SHA-256 over a recipe's eval files, by name: each as its path, a NUL, its length and its bytes, so no two sets collide by concatenation.</summary>
    private static string EvalsHash(List<RecipeSetFile> evals)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in evals.OrderBy(static e => e.RelativePath, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(file.RelativePath + "\0" + file.Bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\0"));
            hash.AppendData(file.Bytes.Span);
        }

        return "sha256:" + Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string Digest(List<ManifestEntry> recipes)
    {
        var lines = recipes.Select(static r => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{r.Id.Value}@{r.Version}\t{r.Hash}\t{r.EvalsHash}\n"));
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(lines))));
    }
}
