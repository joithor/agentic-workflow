// Aw.Kernel.Data NativeAOT canary: drives the library through its public surface and prints only deterministic
// lines, so run.sh can compare them with expected.txt. Usage: Aw.Kernel.Data.AotSmoke <empty directory>.
using System.Globalization;
using Aw.Kernel.Data;
using Aw.Kernel.Data.AotSmoke;

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    await Console.Error.WriteLineAsync("usage: Aw.Kernel.Data.AotSmoke <empty directory>").ConfigureAwait(false);
    return 2;
}

var ct = CancellationToken.None;
var factory = new SqliteConnectionFactory(new SqliteOptions(Path.Combine(args[0], "canary.db")));
var migrations = EmbeddedMigrations.Load(typeof(Canary).Assembly, "Aw.Kernel.Data.AotSmoke.Migrations.").Value;
Canary.Line("loaded " + string.Join(',', migrations.Select(m => m.Version.ToString(CultureInfo.InvariantCulture) + ":" + m.Name)));

var runner = new MigrationRunner(factory, TimeProvider.System);
var first = (await runner.Migrate(migrations, ct).ConfigureAwait(false)).Value;
Canary.Line("migrate " + first.FromVersion.ToString(CultureInfo.InvariantCulture) + "->" + first.ToVersion.ToString(CultureInfo.InvariantCulture));

var id = Guid.ParseExact("0f8fad5b-d9cb-469f-a165-70867728950e", "D");
var at = new DateTimeOffset(2026, 10, 10, 9, 5, 7, 42, TimeSpan.FromHours(2));
var sessions = new SqliteUnitOfWorkFactory(factory);
var work = (await sessions.BeginSession(ct).ConfigureAwait(false)).Value;
await using (work.ConfigureAwait(false))
{
    var insert = new SqlStatement(
        "INSERT INTO runs (id, name, started_at) VALUES ($id, $name, $at)",
        c => c.BindId("$id", new Canary.RunId(id), r => r.Value).Bind("$name", "canary").Bind("$at", at));
    _ = await SqliteQueries.Execute(work, insert, ct).ConfigureAwait(false);
    _ = await SqliteQueries.Execute(work, new SqlStatement("INSERT INTO steps VALUES ($id, $n)", c => c.Bind("$id", id).Bind("$n", 1)), ct).ConfigureAwait(false);
    _ = await work.Commit(ct).ConfigureAwait(false);
}

var read = (await sessions.BeginRead(ct).ConfigureAwait(false)).Value;
await using (read.ConfigureAwait(false))
{
    var row = await SqliteQueries.One(read, new SqlStatement("SELECT id, name, started_at FROM runs"), new Canary.RunMapper(), ct).ConfigureAwait(false);
    Canary.Line("row " + row.Value);
    var missing = await SqliteQueries.One(read, new SqlStatement("SELECT id, name, started_at FROM runs WHERE name = 'none'", Subject: "run none"), new Canary.RunMapper(), ct).ConfigureAwait(false);
    Canary.Line("missing " + missing.Error.Code);
}

var again = (await runner.Migrate(migrations, ct).ConfigureAwait(false)).Value;
Canary.Line("rerun applied " + again.Applied.Count.ToString(CultureInfo.InvariantCulture));
return 0;
