using Aw.Kernel.Testing;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class DirectoryLaneFilesContractTests : LaneFilesContract, IDisposable
{
    private readonly TempDirectory _dir = new();

    // Writes what scripts/meta-lane.sh and the session-id hook write: <lane> holds the id, <lane>.agent the facts, <lane>.ambiguous marks a refusal.
    protected override ValueTask<(ILaneFiles Files, Func<LaneRecord, Task> Put)> Create() =>
        ValueTask.FromResult<(ILaneFiles, Func<LaneRecord, Task>)>((new DirectoryLaneFiles(_dir.Path), Put));

    private async Task Put(LaneRecord record)
    {
        if (record.Session is { } session)
        {
            await File.WriteAllTextAsync(Path.Combine(_dir.Path, record.Lane), session.Value + "\n", TestContext.Current.CancellationToken);
        }

        if (record.Agent is { } agent)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["agent"] = agent.Agent,
                ["processName"] = agent.ProcessName,
                ["hooks"] = agent.Hooks,
                ["transcripts"] = agent.Transcripts,
            });
            await File.WriteAllTextAsync(Path.Combine(_dir.Path, record.Lane + ".agent"), json, TestContext.Current.CancellationToken);
        }

        if (record.Ambiguous)
        {
            await File.WriteAllTextAsync(Path.Combine(_dir.Path, record.Lane + ".ambiguous"), string.Empty, TestContext.Current.CancellationToken);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_lane_name_that_could_escape_the_directory_is_never_read()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "secret"), "x", TestContext.Current.CancellationToken);
        var files = new DirectoryLaneFiles(Path.Combine(_dir.Path, "lanes"));

        var record = await files.Record("../secret", CancellationToken.None);

        Assert.Null(record.Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_broken_agent_file_counts_as_absent()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "api.agent"), "{ not json", TestContext.Current.CancellationToken);

        var record = await new DirectoryLaneFiles(_dir.Path).Record("api", CancellationToken.None);

        Assert.Null(record.Value);
    }

    public void Dispose() => _dir.Dispose();
}
