using Aw.Judge.Core.Ports;
using Aw.Judge.Host;
using Aw.Judge.Infrastructure.Config;
using Aw.Judge.Testing;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Config;

[Lane(TestLane.Integration)]
public sealed class FileJudgeConfigStoreContractTests : JudgeConfigStoreContract, IDisposable
{
    private readonly TempStateDirectory _state = new();

    protected override ValueTask<IJudgeConfigStore> Create(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IJudgeConfigStore>(Store(Path.Combine(_state.Path, "judge")));

    protected override async ValueTask<IJudgeConfigStore> CreateUnavailable(CancellationToken cancellationToken)
    {
        var file = Path.Combine(_state.Path, "not-a-directory");
        await File.WriteAllTextAsync(file, "x", cancellationToken);
        return Store(Path.Combine(file, "judge"));
    }

    public void Dispose() => _state.Dispose();

    private static FileJudgeConfigStore Store(string stateDirectory) =>
        new(stateDirectory, AwJson.CreateOptions(JudgeJsonContext.Default));
}
