using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Jobs;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.GetJob;

/// <summary>GET /sindri/jobs/{jobId} over HTTP (API P6 unit G): read-your-writes right after a start, and the malformed id.</summary>
[Lane(TestLane.Integration)]
public sealed class GetJobHttpTests
{
    [Fact(Timeout = 30_000)]
    public async Task Get_after_start_with_X_Sindri_After_sees_running()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await JobHost.Start(ct);
        var (job, sequence) = await JobHost.StartEcho(host, lines: 100, delayMs: 100);

        var answer = await host.Http.Ask(Get($"/sindri/jobs/{job.Value}", [.. JobHost.Human(host), (SindriHeaders.After, sequence.ToString(System.Globalization.CultureInfo.InvariantCulture))]));
        _ = await host.Http.Ask(Post($"/sindri/jobs/{job.Value}/cancel", json: null, JobHost.Human(host)));
        await JobHost.Settle(host, ct);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        using var json = JsonDocument.Parse(answer.Body);
        Assert.Equal(("running", "EchoJob"), (json.RootElement.GetProperty("state").GetString(), json.RootElement.GetProperty("kind").GetString()));
        Assert.Equal(["CancelJob"], json.RootElement.GetProperty("actions").EnumerateArray().Select(static a => a.GetString()), StringComparer.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task Get_bad_id_is_400()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await JobHost.Start(ct);

        var answer = await JobHost.Read(host, "/sindri/jobs/not-a-job");

        Assert.Equal((HttpStatusCode.BadRequest, "SND-CLI-003"), (answer.Status, answer.Code));
    }
}
