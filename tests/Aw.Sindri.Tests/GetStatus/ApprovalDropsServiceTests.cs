using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CheckProfile;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>Journey 6b: a failed service an approval dropped leaves the health too, not only GET /services (plan G5, G10).</summary>
public sealed class ApprovalDropsServiceTests
{
    private const string WithGhost = GoldenHost.ProfileYaml + "  ghost: { every: 1h }\n";

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Approving_a_profile_without_the_failed_service_makes_status_healthy_again()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct, WithGhost);
        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.Append(host, EventTypes.ServiceRan,
            new ServiceRan(CommandId.From(new Guid(0xa2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)), ServiceName.From("ghost"), KnownInstants.Epoch, ServiceOutcome.Failed, "SND-SCHED-001"), "service.ran:ghost", ct);
        await ViewScenarios.CatchUp(host, ct);
        Assert.Equal("degraded", await Word(host));

        await File.WriteAllTextAsync(Path.Combine(host.Home, "profile", "profile.yaml"), GoldenHost.ProfileYaml, ct);
        await using (var scope = ViewScenarios.Service(host, out var grant))
        {
            _ = (await scope.ServiceProvider.GetRequiredService<IService<CheckProfileRequest, CheckProfileResponse>>().Handle(new CheckProfileRequest(grant), ct)).Value;
        }

        await ViewScenarios.Approve(host, ct);
        await ViewScenarios.CatchUp(host, ct);

        Assert.Equal("healthy", await Word(host));
        Assert.DoesNotContain("ghost", await ViewScenarios.Read(host, "/sindri/services"), StringComparison.Ordinal);
    }

    private static async Task<string?> Word(GoldenHost host)
    {
        using var status = JsonDocument.Parse(await ViewScenarios.Read(host, "/sindri/status"));
        return status.RootElement.GetProperty("health").GetProperty("word").GetString();
    }
}
