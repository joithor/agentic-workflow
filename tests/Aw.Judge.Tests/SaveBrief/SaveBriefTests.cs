using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Features.SaveBrief;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.SaveBrief;

public sealed class SaveBriefTests
{
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch.AddMinutes(5));
    private readonly InMemoryBriefStore _briefs = new();

    [Fact, Trait("Lane", "unit")]
    public async Task SaveBrief_saves_the_brief_unmapped_at_the_clock_time()
    {
        var request = A.SaveBriefRequest().Named("alice-helper").Build();

        var result = await new SaveBriefService(_briefs, _clock).Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = Assert.Single(_briefs.Briefs);
        Assert.Equal(saved, result.Value.Brief);
        Assert.Equal(ToolUseId.From("toolu_1"), saved.ToolUseId);
        Assert.Null(saved.AgentId);
        Assert.Equal("alice-helper", saved.DispatchName);
        Assert.Equal(("general-purpose", "Fix the failing test", "The test passes", "npm test"), (saved.SubagentType, saved.Goal, saved.AcceptanceCriteria, saved.ProofCommand));
        Assert.Equal(_clock.GetUtcNow(), saved.SavedAt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SaveBrief_refuses_a_second_brief_for_the_same_tool_use_id()
    {
        var service = new SaveBriefService(_briefs, _clock);
        _ = await service.Handle(A.SaveBriefRequest().Build(), CancellationToken.None);

        var result = await service.Handle(A.SaveBriefRequest().Build(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(JudgeErrors.BriefExists.Code, result.Error.Code);
        _ = Assert.Single(_briefs.Briefs);
    }
}
