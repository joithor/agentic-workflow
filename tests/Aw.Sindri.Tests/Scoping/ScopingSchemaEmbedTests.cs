using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The scoping Steps read their fact schemas from the Features assembly; a build that embedded none parked the line at intake.</summary>
public sealed class ScopingSchemaEmbedTests
{
    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("no AgenticWorkflow.slnx above the test binaries");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_features_assembly_carries_every_scoping_schema()
    {
        var files = Directory.GetFiles(Path.Combine(RepoRoot(), "schemas", "scoping"), "*.json").Select(f => "scoping-schemas/" + Path.GetFileName(f)).Order(StringComparer.Ordinal).ToList();
        var embedded = typeof(ScopeSchemas).Assembly.GetManifestResourceNames().Where(n => n.StartsWith("scoping-schemas/", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();

        Assert.NotEmpty(files);
        Assert.Equal(files, embedded);
    }
}
