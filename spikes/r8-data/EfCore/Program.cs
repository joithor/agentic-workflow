// R8 variant (b): EF Core 10 with the Sqlite provider, compiled model and precompiled queries.
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Spike.R8;

return Scenario.Run("efcore", args.Length > 0 ? args[0] : "efcore.db", path => new EfItemStore(path));

public sealed class ItemRow
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Size { get; set; }
}

public sealed class SpikeContext(DbContextOptions<SpikeContext> options) : DbContext(options)
{
    public DbSet<ItemRow> Items => Set<ItemRow>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ItemRow>(e => { e.ToTable("items"); e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id"); e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Size).HasColumnName("size"); });
    }
}

// Design-time factory: the Tasks package builds the compiled model and precompiled queries with it.
public sealed class SpikeContextFactory : IDesignTimeDbContextFactory<SpikeContext>
{
    public SpikeContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SpikeContext>().UseSqlite("Data Source=design.db").Options);
}

sealed class EfItemStore : IItemStore
{
    readonly SqliteConnection _conn;
    readonly SpikeContext _db;

    public EfItemStore(string path)
    {
        _conn = new SqliteConnection($"Data Source={path}");
        _conn.Open();
        using (var c = _conn.CreateCommand()) { c.CommandText = "PRAGMA journal_mode = wal"; c.ExecuteNonQuery(); }
        _db = new SpikeContext(new DbContextOptionsBuilder<SpikeContext>().UseSqlite(_conn).Options);
    }

    public string JournalMode() { using var c = _conn.CreateCommand(); c.CommandText = "PRAGMA journal_mode"; return (string)c.ExecuteScalar()!; }
    // EnsureCreated needs the design-time model (RequiresDynamicCode); schema comes from SQL, as with migrations files.
    public void CreateSchema() { using var c = _conn.CreateCommand(); c.CommandText = "CREATE TABLE IF NOT EXISTS items (id TEXT PRIMARY KEY, name TEXT NOT NULL, size INTEGER NOT NULL)"; c.ExecuteNonQuery(); }
    public void Insert(Item i) { _db.Items.Add(ToRow(i)); _db.SaveChanges(); _db.ChangeTracker.Clear(); }
    public void Update(Item i) { _db.Items.Update(ToRow(i)); _db.SaveChanges(); _db.ChangeTracker.Clear(); }
    // Workaround: the EF 10 query precompiler fails on a lambda that captures a method parameter
    // ("UnreachableException: IdentifierName of type ParameterSymbol"); copying it to a local avoids it.
    public void Delete(string id) { var key = id; _db.Items.Where(x => x.Id == key).ExecuteDelete(); }
    public Item? Get(string id) { var key = id; return _db.Items.AsNoTracking().Where(x => x.Id == key).Select(x => new Item(x.Id, x.Name, x.Size)).FirstOrDefault(); }
    public IReadOnlyList<Item> List() => _db.Items.AsNoTracking().OrderBy(x => x.Id).Select(x => new Item(x.Id, x.Name, x.Size)).ToList();

    public void InsertThenRollback(Item i)
    {
        using var tx = _db.Database.BeginTransaction();
        Insert(i);
        tx.Rollback();
    }

    public void InsertBothCommitted(Item a, Item b)
    {
        using var tx = _db.Database.BeginTransaction();
        _db.Items.Add(ToRow(a)); _db.Items.Add(ToRow(b)); _db.SaveChanges(); _db.ChangeTracker.Clear();
        tx.Commit();
    }

    static ItemRow ToRow(Item i) => new() { Id = i.Id, Name = i.Name, Size = i.Size };
    public void Dispose() { _db.Dispose(); _conn.Dispose(); }
}
