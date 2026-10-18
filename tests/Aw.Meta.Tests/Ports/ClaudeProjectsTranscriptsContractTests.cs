using Aw.Kernel.Testing;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class ClaudeProjectsTranscriptsContractTests : ClaudeTranscriptsContract, IDisposable
{
    private readonly TempDirectory _dir = new();

    protected override (IClaudeTranscripts Reader, Action<string, string> Put) Create() =>
        (new ClaudeProjectsTranscripts(_dir.Path), (session, content) =>
        {
            var project = Directory.CreateDirectory(Path.Combine(_dir.Path, "-home-x-repo"));
            File.WriteAllText(Path.Combine(project.FullName, session + ".jsonl"), content);
        }
    );

    [Fact]
    public async Task A_session_id_that_is_a_path_is_never_looked_up()
    {
        var tail = await new ClaudeProjectsTranscripts(_dir.Path).Tail(SessionId.From("../x"), 100, TestContext.Current.CancellationToken);

        Assert.Null(tail.Value);
    }

    public void Dispose() => _dir.Dispose();
}
