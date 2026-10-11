using Aw.Evidence.Features.Doctor;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Stack;

public sealed class DoctorTests
{
    private static async Task<DoctorResponse> Check(FakeStackProbe probe, Dictionary<string, string> settings, InMemoryRunFiles? files = null) =>
        (await new DoctorService(probe, new FakeSecrets(settings), files ?? new InMemoryRunFiles()).Handle(new DoctorRequest(), CancellationToken.None)).Value;

    private static Dictionary<string, string> Settings(params (string Name, string Value)[] values) =>
        values.ToDictionary(static v => v.Name, static v => v.Value, StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_is_healthy_when_the_app_database_and_api_answer()
    {
        var report = await Check(new FakeStackProbe { Title = "App" }, Settings(("UI_EVIDENCE_APP_TITLE", "App")));

        Assert.Equal((true, 0), (report.Healthy, report.Failures.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_reads_the_app_title_from_the_local_env_file()
    {
        var files = new InMemoryRunFiles().With("/home/qa/.ui-evidence.local.env", TestJson.Utf8("# local\nexport UI_EVIDENCE_APP_TITLE=\"My App\"\n"));

        var report = await Check(new FakeStackProbe { Title = "My App" }, Settings(("UI_EVIDENCE_CONFIG", "/home/qa/.ui-evidence.local.env")), files);

        Assert.True(report.Healthy);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_refuses_a_foreign_app_and_lists_every_failure()
    {
        var probe = new FakeStackProbe { Title = "Someone else", DatabaseReady = false };
        probe.OpenPorts.Clear();

        var report = await Check(probe, Settings(("UI_EVIDENCE_APP_TITLE", "App")));

        Assert.False(report.Healthy);
        Assert.Equal(3, report.Failures.Count);
        Assert.StartsWith(":3000 does not look like this app", report.Failures[0], StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_is_not_healthy_without_an_app_title()
    {
        var report = await Check(new FakeStackProbe(), Settings());

        Assert.False(report.Healthy);
        Assert.StartsWith("UI_EVIDENCE_APP_TITLE is not set", report.Failures[0], StringComparison.Ordinal);
    }
}
