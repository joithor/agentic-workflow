using Aw.Sindri.Core;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Tests.Views;

/// <summary>The SND-API-011 wait names a projector that can't run without an event number.</summary>
public sealed class ProjectorProgressTextTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task A_wait_on_views_that_cannot_be_updated_fails_at_once_naming_why()
    {
        var progress = new ProjectorProgress(new SliceHarness().Clock);
        progress.Publish(new ViewsStatus(3, 10, ProjectedAt: null, Rebuilding: false, Stalled: true, StalledAt: null, StalledType: "SND-VIEW-001: views.db can't be opened"));

        var result = await progress.WaitFor(10, TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.ViewsBehind.Code, result.Error.Code);
        Assert.Contains("views can't be updated (SND-VIEW-001: views.db can't be opened)", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("stalled at", result.Error.Message, StringComparison.Ordinal);
    }
}
