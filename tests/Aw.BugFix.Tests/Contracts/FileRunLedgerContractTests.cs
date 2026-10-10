using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.BugFix.Host;
using Aw.BugFix.Infrastructure;
using Aw.BugFix.Testing;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Integration)]
public sealed class FileRunLedgerContractTests : RunLedgerContract, IDisposable
{
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(BugFixJsonContext.Default);
    private readonly TempDirectory _temp = new();

    protected override IRunLedger Ledger { get; } = new FileRunLedger(_json);

    protected override string Directory => _temp.Path;

    public void Dispose() => _temp.Dispose();

    protected override void Edit(string path, RunRecord record) => File.WriteAllBytes(path, AwJson.Serialize(record, _json));
}
