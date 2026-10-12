using Aw.Kernel.Testing;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Ledger;

[Lane(TestLane.Integration)]
public sealed class SqliteLedgerReaderContractTests : LedgerReaderContract, IDisposable
{
    private readonly TempHome _home = new();
    private LedgerDatabase? _readerDatabase;

    public void Dispose()
    {
        _readerDatabase?.Dispose();
        _home.Dispose();
    }

    protected override ValueTask<SindriStores> Create(TimeProvider clock, IProcessProbe probe) =>
        ValueTask.FromResult(SqliteStores.Create(_home.Path, clock, probe));

    protected override ILedgerReader Reader()
    {
        // its own database object over the same file: the reader shares nothing with the writer but the path
        _readerDatabase = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), Clock);
        return new SqliteLedgerReader(_readerDatabase);
    }

    protected override bool LedgerExists() => File.Exists(Path.Combine(_home.Path, "ledger.db"));
}
