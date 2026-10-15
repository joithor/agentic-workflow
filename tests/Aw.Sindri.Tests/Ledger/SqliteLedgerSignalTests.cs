using System.Globalization;
using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>The SQLite ledger pulses only after its commit (API spec §5.5): a second connection already reads the row at the pulse. The pulse rules themselves (once per new append, none on a replay or a refusal) are in <see cref="LedgerStoreContract"/>.</summary>
public sealed class SqliteLedgerSignalTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly SequentialIdGenerator _ids = new();
    private readonly FakeProcessProbe _probe = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task Append_pulses_after_commit()
    {
        var signal = new CommittedHeadSignal(Path.Combine(_home.Path, "ledger.db"));
        var stores = SqliteStores.Create(_home.Path, _clock, _probe, signal);
        var grant = (await stores.Lease.Acquire(_probe.Self(), Token)).Value;

        _ = await stores.Ledger.Append(Entry("k1"), grant, Token);
        _ = await stores.Ledger.Append(Entry("k2"), grant, Token);

        // at each pulse the row was already readable by another connection: the pulse came after the commit
        Assert.Equal([(1L, 1L), (2L, 2L)], signal.Pulses);
    }

    private LedgerEntry Entry(string key)
    {
        var context = EventContext.Create("host:sindri", "local", 0, _ids.NewId()).Value;
        return new LedgerEntry(context.Envelope("service.heartbeat", """{"n":1}""", _clock, _ids), key, Item: null);
    }

    /// <summary>Records each pulse with the head a second connection reads at that moment.</summary>
    private sealed class CommittedHeadSignal(string path) : ILedgerSignal
    {
        public List<(long Pulsed, long Committed)> Pulses { get; } = [];

        public void Pulse(long sequence)
        {
            using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COALESCE(MAX(seq), 0) FROM events";
            Pulses.Add((sequence, Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture)));
        }

        public ValueTask<long> WaitAfter(long sequence, TimeSpan limit, CancellationToken cancellationToken) => ValueTask.FromResult(sequence);
    }
}
