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
        var repo = new ContextRepo(ContextRepoName.From("split-panes"), fixture.Path);
        var doc = Path.Combine(fixture.Path, "docs", "architecture.md");
        await File.AppendAllTextAsync(doc, "\n## Deploy\n\nThe orientation deploy key is " + Planted.Secret + "\n", TestContext.Current.CancellationToken);
        _ = Git(fixture.Path, "commit", "-q", "-am", "plant");

        var first = (await home.Index.Refresh(repo, ["docs/**/*.md"], CancellationToken.None)).Value;
        var source = new RepoDocsSource([repo], home.Database, home.Git, home.Scrubber, home.Clock);
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
        var missing = new ContextRepo(ContextRepoName.From("gone"), Path.Combine(Path.GetTempPath(), "aw-ctx-no-such-repo"));

        var refreshed = (await home.Index.Refresh(missing, ["docs/**/*.md"], CancellationToken.None)).Value;
        var status = (await home.Index.Status(missing, CancellationToken.None)).Value;
        var fetched = await new RepoDocsSource([], home.Database, home.Git, home.Scrubber, home.Clock).Fetch(
            new SourceRef(SourceId.From("repo-docs"), ExternalId.From("gone/docs/a.md#L1-L2"), SourceVersion.From("abcdef1"), Permalink: null,
                RecordKey.From("repo:gone/docs/a.md")), CancellationToken.None);

        Assert.Equal("SND-CONTEXT-010", refreshed.Code);
        Assert.Null(refreshed.Head);
        Assert.Equal("SND-CONTEXT-010", status.Code);
        Assert.Equal("SND-CONTEXT-003", fetched.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_key_or_a_hidden_comment_across_a_chunk_boundary_never_reaches_the_index_or_a_fetch()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("split-panes"), fixture.Path);
        var body = Enumerable.Range(0, 30).Select(i => new string((char)('A' + (i % 26)), 60) + "QZ" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        var key = string.Join('\n', ["-----BEGIN " + "RSA PRIVATE KEY-----", .. body, "-----END " + "RSA PRIVATE KEY-----"]);
        var filler = string.Join('\n', Enumerable.Range(0, 30).Select(i => "Orientation filler line " + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var doc = "# Keys\n" + filler + "\n" + key + "\n<!--\n# a heading inside a comment\nhidden-instruction-text\n-->\nend orientation\n";
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "docs", "keys.md"), doc, TestContext.Current.CancellationToken);
        _ = Git(fixture.Path, "add", "-A");
        _ = Git(fixture.Path, "commit", "-q", "-m", "keys");

        _ = (await home.Index.Refresh(repo, ["docs/**/*.md"], CancellationToken.None)).Value;
        var source = new RepoDocsSource([repo], home.Database, home.Git, home.Scrubber, home.Clock);
        var hits = (await source.Search(new SourceQuery(["orientation", "filler"], AsOf: null, 50, TimeSpan.FromSeconds(5)), CancellationToken.None)).Value;
        var fetched = new List<string>();
        foreach (var hit in hits.Where(h => h.Ref.Canonical.Value.EndsWith("keys.md", StringComparison.Ordinal)))
        {
            fetched.Add((await source.Fetch(hit.Ref, CancellationToken.None)).Value.Text);
        }

        Assert.True(Count(home, "SELECT count(*) FROM doc_chunk WHERE path = 'docs/keys.md'") >= 2);   // the doc spans chunks
        Assert.Equal(0L, Count(home, "SELECT count(*) FROM doc_chunk WHERE text LIKE '%QZ1%' OR text LIKE '%hidden-instruction%'"));
        Assert.DoesNotContain(fetched, t => t.Contains("QZ1", StringComparison.Ordinal) || t.Contains("hidden-instruction", StringComparison.Ordinal));
        Assert.NotEmpty(fetched);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_oversized_doc_is_skipped_and_kept_and_an_unreadable_one_keeps_its_earlier_rows()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("split-panes"), fixture.Path);
        _ = (await home.Index.Refresh(repo, ["docs/**/*.md"], CancellationToken.None)).Value;
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "docs", "huge.md"), "# Huge\n" + new string('x', 1_100_000), TestContext.Current.CancellationToken);
        await File.AppendAllTextAsync(Path.Combine(fixture.Path, "docs", "architecture.md"), "\nA later line.\n", TestContext.Current.CancellationToken);
        _ = Git(fixture.Path, "add", "-A");
        _ = Git(fixture.Path, "commit", "-q", "-m", "huge and edit");
        var newBlob = Git(fixture.Path, "rev-parse", "HEAD:docs/architecture.md");
        File.Delete(Path.Combine(fixture.Path, ".git", "objects", newBlob[..2], newBlob[2..]));   // git can't read the edit's blob this run

        var refreshed = (await home.Index.Refresh(repo, ["docs/**/*.md"], CancellationToken.None)).Value;
        var again = (await home.Index.Refresh(repo, ["docs/**/*.md"], CancellationToken.None)).Value;

        Assert.Equal(2, refreshed.Skipped);   // the oversized doc and the unreadable edit
        Assert.Equal(1L, Count(home, "SELECT count(*) FROM doc WHERE path = 'docs/huge.md'"));
        Assert.Equal(0L, Count(home, "SELECT count(*) FROM doc_chunk WHERE path = 'docs/huge.md'"));
        Assert.True(Count(home, "SELECT count(*) FROM doc_chunk WHERE path = 'docs/architecture.md'") > 0);   // its earlier rows stay
        Assert.Equal((1, 1), (again.Kept - 1, again.Skipped));   // huge.md is kept now; the edit is still unreadable
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Git_maps_a_missing_object_to_not_found_and_an_unreadable_repo_to_unavailable()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();

        var missing = await home.Git.Size(fixture.Path, new string('0', 40), CancellationToken.None);
        var noRepo = await home.Git.Head(Path.Combine(fixture.Path, "no-such-dir"), CancellationToken.None);

        Assert.Equal(("SND-CONTEXT-003", Kernel.ErrorKind.NotFound), (missing.Error.Code, missing.Error.Kind));
        Assert.Equal("SND-CONTEXT-010", noRepo.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_fetched_span_still_matches_the_blob_s_lines_after_a_comment_was_removed()
    {
        using var home = new TempHome();
        using var fixture = FixtureRepo.Build();
        var repo = new ContextRepo(ContextRepoName.From("split-panes"), fixture.Path);
        var doc = "# Notes\n<!--\nhidden\nlines\n-->\nintro\n\n## Orientation\nthe zebra line\nlast line\n";
        await File.WriteAllTextAsync(Path.Combine(fixture.Path, "docs", "notes.md"), doc, TestContext.Current.CancellationToken);
        _ = Git(fixture.Path, "add", "-A");
        _ = Git(fixture.Path, "commit", "-q", "-m", "notes");
        _ = (await home.Index.Refresh(repo, ["docs/**/*.md"], CancellationToken.None)).Value;
        var source = new RepoDocsSource([repo], home.Database, home.Git, home.Scrubber, home.Clock);

        var hit = (await source.Search(new SourceQuery(["zebra"], AsOf: null, 5, TimeSpan.FromSeconds(5)), CancellationToken.None)).Value.Single();
        var record = (await source.Fetch(hit.Ref, CancellationToken.None)).Value;

        Assert.EndsWith("#L8-L11", hit.Ref.Id.Value, StringComparison.Ordinal);
        Assert.StartsWith("## Orientation\nthe zebra line\nlast line", record.Text, StringComparison.Ordinal);
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
        foreach (var arg in (string[])["-C", repo, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false", .. args])
        {
            start.ArgumentList.Add(arg);
        }

        using var git = Process.Start(start)!;   // why: git is on the test machine's PATH
        var output = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        return output;
    }
}
