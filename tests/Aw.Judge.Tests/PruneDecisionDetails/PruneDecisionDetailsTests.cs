using Aw.Judge.Features.PruneDecisionDetails;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.PruneDecisionDetails;

public sealed class PruneDecisionDetailsTests
{
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch.AddDays(100));
    private readonly InMemoryDecisionLog _decisions = new();

    [Fact, Trait("Lane", "unit")]
    public async Task PruneDecisionDetails_drops_the_details_older_than_30_days_and_keeps_every_decision()
    {
        await Decision("old", TimeSpan.FromDays(30) + TimeSpan.FromSeconds(1));
        await Decision("edge", TimeSpan.FromDays(30));
        await Decision("new", TimeSpan.FromDays(1));

        var result = await new PruneDecisionDetailsService().Handle(new PruneDecisionDetailsRequest(), CancellationToken.None);

        Assert.Equal(1, result.Value.Pruned);
        Assert.Equal(["old:pruned", "edge:kept", "new:kept"], _decisions.Decisions.Select(d => $"{d.Id.Value}:{(d.Details is null ? "pruned" : "kept")}").ToList());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task PruneDecisionDetails_returns_the_store_error_when_the_store_cant_be_used()
    {
        var result = await new PruneDecisionDetailsService().Handle(new PruneDecisionDetailsRequest(), CancellationToken.None);

        Assert.True(result.IsError);
    }

    private async Task Decision(string id, TimeSpan ago) =>
        Assert.True((await _decisions.Record(A.Decision().WithId(id).At(_clock.GetUtcNow() - ago).Build(), CancellationToken.None)).IsSuccess);
}
