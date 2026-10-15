using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>'ReadSubject' on the home's fact files: a fact that isn't stored is none, and content past 64 KiB is cut where no character is split.</summary>
[Lane(TestLane.Integration)]
public sealed class FactReadActivitiesTests : IDisposable
{
    private const int Limit = 64 * 1024;

    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private FactValue Store(byte[] bytes, string sha)
    {
        var facts = Directory.CreateDirectory(Path.Combine(_home.Path, "workflow", "facts")).FullName;
        File.WriteAllBytes(Path.Combine(facts, sha), bytes);
        return new FactValue("fact:sha256:" + sha, sha, HeadSha: null);
    }

    private Task<string?> Read(FactValue fact) =>
        new ActivityEnvironment().RunAsync(() => new FactReadActivities(new WorkflowsHome(_home.Path)).ReadSubjectAsync(fact));

    [Fact]
    public async Task A_fact_that_is_not_stored_reads_as_none() =>
        Assert.Null(await Read(new FactValue("fact:sha256:" + new string('b', 64), new string('b', 64), HeadSha: null)));

    [Fact]
    public async Task Content_over_64_KiB_is_cut_at_64_KiB_and_content_under_it_is_read_whole()
    {
        var big = Store(Encoding.UTF8.GetBytes(new string('x', Limit + 500)), new string('1', 64));
        var small = Store(Encoding.UTF8.GetBytes("héllo"), new string('2', 64));

        Assert.Equal(Limit, (await Read(big))!.Length);
        Assert.Equal("héllo", await Read(small));
    }

    [Fact]
    public async Task A_character_the_cut_splits_is_left_out_rather_than_read_as_a_replacement_character()
    {
        var bytes = Encoding.UTF8.GetBytes(new string('x', Limit - 1) + "€€");   // the first '€' (3 bytes) straddles the 64 KiB cut
        var text = await Read(Store(bytes, new string('3', 64)));

        Assert.Equal(new string('x', Limit - 1), text);
        Assert.DoesNotContain("\uFFFD", text, StringComparison.Ordinal);
    }
}
