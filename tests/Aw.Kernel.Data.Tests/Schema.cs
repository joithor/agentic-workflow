namespace Aw.Kernel.Data.Tests;

public static class Schema
{
    public static readonly Migration Items = new(1, "items", "CREATE TABLE items (id TEXT PRIMARY KEY, name TEXT NOT NULL UNIQUE, size INTEGER NOT NULL);");

    public static readonly Migration Tags = new(2, "tags", "CREATE TABLE tags (item_id TEXT NOT NULL REFERENCES items (id), tag TEXT NOT NULL);");

    public static readonly Migration Notes = new(3, "notes", "CREATE TABLE notes (id INTEGER PRIMARY KEY, body TEXT NOT NULL);");

    public static async Task CreateItems(TempDatabase db)
    {
        var report = await new MigrationRunner(db.Factory(), new FixedClock(DateTimeOffset.UnixEpoch))
            .Migrate([Items, Tags], TestContext.Current.CancellationToken);
        Assert.True(report.IsSuccess);
    }
}
