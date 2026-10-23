using System.Security.Cryptography;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>'StoreLiteralFacts': a child's literal input is written under its hash, so a step reads it like any stored fact.</summary>
[Lane(TestLane.Integration)]
public sealed class LiteralFactActivitiesTests : IDisposable
{
    private const string Json = "\"First pass: no revision yet.\"";

    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static FactValue Literal(string json) =>
        new("literal:" + json, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))), HeadSha: null);

    private HomeFactStore Store => new(new WorkflowsHome(_home.Path));

    private Task Run(params FactValue[] facts) =>
        new ActivityEnvironment().RunAsync(() => new LiteralFactActivities(Store).StoreAsync(facts));

    private string FileOf(FactValue fact) => Path.Combine(_home.Path, "workflow", "facts", fact.Sha256);

    [Fact]
    public async Task A_literal_is_stored_as_its_json_under_its_hash_and_reads_back_through_the_store()
    {
        var fact = Literal(Json);

        await Run(fact);

        Assert.Equal(Json, await File.ReadAllTextAsync(FileOf(fact), TestContext.Current.CancellationToken));
        var read = await Store.Read(fact, TestContext.Current.CancellationToken);
        Assert.Equal(Json, Encoding.UTF8.GetString(read.Value.Span));
    }

    [Fact]
    public async Task Storing_a_literal_again_changes_nothing()
    {
        var fact = Literal("{\"a\":1}");
        await Run(fact);
        var first = File.GetLastWriteTimeUtc(FileOf(fact));

        await Run(fact, fact);

        Assert.Equal(first, File.GetLastWriteTimeUtc(FileOf(fact)));
        Assert.Equal("{\"a\":1}", await File.ReadAllTextAsync(FileOf(fact), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_fact_that_is_not_a_literal_is_left_alone()
    {
        var sha = new string('c', 64);

        await Run(new FactValue("fact:sha256:" + sha, sha, HeadSha: null));

        Assert.False(Directory.Exists(Path.Combine(_home.Path, "workflow", "facts")));
    }

    [Fact]
    public async Task A_literal_whose_hash_is_not_its_text_is_refused()
    {
        var wrong = Literal(Json) with { Sha256 = new string('d', 64) };

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => Run(wrong));
        Assert.False(File.Exists(FileOf(wrong)));
    }
}
