using System.Diagnostics;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Testing.Fixtures;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Tests.Infrastructure;

public sealed class RepoDocsTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task A_planted_secret_never_reaches_the_index_and_an_edit_reads_as_a_new_version()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo("split-panes", fixture.Path);
        var doc = Path.Combine(fixture.Path, "docs", "architecture.md");
        await File.AppendAllTextAsync(doc, "\n## Deploy\n\nThe orientation deploy key is " + Planted.Secret + "\n", TestContext.Current.CancellationToken);
        _ = Git(fixture.Path, "commit", "-q", "-am", "plant");

        var first = (await home.Index.Refresh(repo, ["docs/**/*.md"], CancellationToken.None)).Value;
        var source = new RepoDocsSource([repo], home.Database, home.Git, home.Clock);
        var hits = (await source.Search(new SourceQuery(["deploy"], AsOf: null, 10, TimeSpan.FromSeconds(5)), CancellationToken.None)).Value;
        await File.AppendAllTextAsync(doc, "\nedited\n", TestContext.Current.CancellationToken);
        _ = Git(fixture.Path, "commit", "-q", "-am", "edit");
        var versions = (await source.Versions([hits[0].Ref], CancellationToken.None)).Value;

        Assert.Equal(2, first.Added);
        Assert.Equal(0L, Count(home, $"SELECT count(*) FROM doc_chunk WHERE text LIKE '%{Planted.Secret}%'"));
        Assert.Equal(1L, Count(home, "SELECT count(*) FROM doc_chunk WHERE text LIKE '%[REDACTED:github-token]%'"));
        Assert.Equal("repo:split-panes/docs/architecture.md", hits[0].Ref.Canonical.Value);
        Assert.Equal(Git(fixture.Path, "rev-parse", "HEAD:docs/architecture.md"), versions[0]?.Value);
        Assert.NotEqual(hits[0].Ref.Version, versions[0]);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_repo_git_cant_read_is_reported_with_its_code_and_a_source_outside_scope_is_not_found()
    {
        using var home = new TempHome();
        var missing = new ContextRepo("gone", Path.Combine(Path.GetTempPath(), "aw-ctx-no-such-repo"));

        var refreshed = (await home.Index.Refresh(missing, ["docs/**/*.md"], CancellationToken.None)).Value;
        var status = (await home.Index.Status(missing, CancellationToken.None)).Value;
        var fetched = await new RepoDocsSource([], home.Database, home.Git, home.Clock).Fetch(
            new SourceRef(SourceId.From("repo-docs"), ExternalId.From("gone/docs/a.md#L1-L2"), SourceVersion.From("abcdef1"), Permalink: null,
                RecordKey.From("repo:gone/docs/a.md")), CancellationToken.None);

        Assert.Equal("SND-CONTEXT-010", refreshed.Code);
        Assert.Null(refreshed.Head);
        Assert.Equal("SND-CONTEXT-010", status.Code);
        Assert.Equal("SND-CONTEXT-003", fetched.Error.Code);
    }

    private static long Count(TempHome home, string sql)
    {
        using var connection = new SqliteConnection("Data Source=" + home.Settings.DatabasePath + ";Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;   // why: count(*) always answers a row
    }

    private static string Git(string repo, params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var arg in (string[])["-C", repo, "-c", "user.name=t", "-c", "user.email=t@example.invalid", .. args])
        {
            start.ArgumentList.Add(arg);
        }

        using var git = Process.Start(start)!;   // why: git is on the test machine's PATH
        var output = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        return output;
    }
}
