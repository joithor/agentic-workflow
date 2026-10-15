using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// On the pinned dev server: the Query carries instants, not ages (the composers measure to the caller's clock, so an age
/// moves between two reads of an idle workflow), and the workflow names the subject from the first input's stored content on
/// start, so the 'Subject' search attribute and the view's subject agree (API spec §3.8 'subject', 'status').
/// </summary>
[Lane(TestLane.Integration)]
public sealed class QueryClockAndSubjectTests : IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    private async Task<InstanceStart> StartWith(IServiceProvider sindri, string item, string content, CancellationToken ct)
    {
        var started = await WorkflowsHarness.StepStart(sindri, item, ct);
        return started with { Inputs = new Dictionary<FactKey, InputFact> { [FactKey.From("fact:spec")] = new("notes.txt", _harness.Fact(content)) } };
    }

    [Fact(Timeout = 120_000)]
    public async Task Two_reads_of_an_idle_instance_differ_in_age_and_timeout_because_the_composer_measures_to_the_callers_clock()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, ct);
        var handle = await WorkflowsHarness.Start(env.Client, await StartWith(served.Sindri, "T-1", "Fix the crash on launch", ct));
        _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

        var first = await handle.QueryAsync(static wf => wf.GetInstance());
        var firstRead = DateTimeOffset.UtcNow;
        await Task.Delay(TimeSpan.FromSeconds(2.2), ct);
        var second = await handle.QueryAsync(static wf => wf.GetInstance());
        var secondRead = DateTimeOffset.UtcNow;

        var before = InstanceViews.Status(first, firstRead);
        var after = InstanceViews.Status(second, secondRead);
        Assert.Equal((first.Since, first.Task!.TimeoutAt), (second.Since, second.Task!.TimeoutAt));   // the view holds instants, and they did not move
        Assert.True(after.Header.AgeSeconds >= before.Header.AgeSeconds + 2, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"age {before.Header.AgeSeconds} then {after.Header.AgeSeconds}"));
        Assert.True(before.Timeout!.InSeconds - after.Timeout!.InSeconds >= 2, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"timeout {before.Timeout.InSeconds} then {after.Timeout.InSeconds}"));
        await handle.TerminateAsync("test over");
    }

    [Fact(Timeout = 120_000)]
    public async Task The_subject_is_named_from_the_stored_content_and_the_search_attribute_is_the_same_key()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, ct);
        var handle = await WorkflowsHarness.Start(env.Client, await StartWith(served.Sindri, "T-1", "Fix the crash on launch of the tab manager when two windows are open\nsecond line", ct));

        var view = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

        const string line = "Fix the crash on launch of the tab manager when two windows are open";
        var key = line[..40];
        Assert.Equal(new SubjectRef("payload", key, line, Href: null), view.Subject);
        Assert.Equal(key, (await handle.DescribeAsync()).TypedSearchAttributes.Get(SindriTemporal.SubjectKey));
        await handle.TerminateAsync("test over");
    }
}
