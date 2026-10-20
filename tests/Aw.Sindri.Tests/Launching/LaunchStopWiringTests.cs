using System.Net;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Launching;

/// <summary>Stop through the real composition: the provider's own <see cref="ILauncher"/> ends a live launch's session once, as an exit, and its row stops being live.</summary>
public sealed class LaunchStopWiringTests
{
    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task The_real_launchers_Stop_records_one_session_ended_exit_and_the_row_is_no_longer_live()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml);
        await ViewScenarios.Approve(host, ct);
        var item = await ItemScenarios.Create(host, "Try the queue", "demo");
        var launch = A.FirstLaunchId;
        await LaunchScenarios.Started(host, launch, item, ViewerOutcomeKind.Skipped, ct, LaunchScenarios.ViewerOf("none", "none", ViewerOutcomeKind.Skipped));
        await ViewScenarios.CatchUp(host, ct);
        var launcher = host.App.Services.GetRequiredService<ILauncher>();

        Assert.True((await launcher.Stop(launch, ct)).IsSuccess);
        await ViewScenarios.CatchUp(host, ct);

        var ledger = host.App.Services.GetRequiredService<ILedgerStore>();
        var ended = (await ledger.ReadAfter(0, 1000, ct)).Value.Where(e => string.Equals(e.Envelope.Type, EventTypes.SessionEnded, StringComparison.Ordinal)).ToList();
        _ = Assert.Single(ended);
        Assert.Contains("\"reason\": \"exit\"", ApiGolden.Reindent(ended[0].Envelope.Payload), StringComparison.Ordinal);
        var shown = await host.Http.Ask(Get($"/sindri/launches/{launch.Value}", ItemScenarios.Human(host)));
        Assert.Equal(HttpStatusCode.OK, shown.Status);
        Assert.Contains("\"ended\": {", ApiGolden.Reindent(shown.Body), StringComparison.Ordinal);   // an ended session is not live
    }
}
