// Shared scenario for the R8 spike, linked into each variant.
// Each variant implements IItemStore; the scenario checks behavior and exits non-zero on any mismatch.
namespace Spike.R8;

public sealed record Item(string Id, string Name, int Size);

public interface IItemStore : IDisposable
{
    string JournalMode();
    void CreateSchema();
    void Insert(Item item);
    void Update(Item item);
    void Delete(string id);
    Item? Get(string id);
    IReadOnlyList<Item> List();
    /// Inserts inside a transaction and rolls it back.
    void InsertThenRollback(Item item);
    /// Inserts two items in one committed transaction.
    void InsertBothCommitted(Item a, Item b);
}

public static class Scenario
{
    public static int Run(string variant, string dbPath, Func<string, IItemStore> open)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
        if (dir is null || !Directory.Exists(dir))
        {
            Console.Error.WriteLine($"{variant}: error: directory does not exist: {dir}");
            return 2;
        }
        var failures = new List<string>();
        void Check(bool ok, string what) { if (!ok) failures.Add(what); }

        using (var s = open(dbPath))
        {
            Check(s.JournalMode() == "wal", "journal_mode is wal");
            s.CreateSchema();
            s.Insert(new Item("a", "alpha", 1));
            s.Insert(new Item("b", "beta", 2));
            s.Insert(new Item("c", "gamma", 3));
            s.Update(new Item("b", "beta-2", 20));
            s.Delete("c");
            s.InsertThenRollback(new Item("x", "rolled-back", 99));
            s.InsertBothCommitted(new Item("d", "delta", 4), new Item("e", "epsilon", 5));
            Check(s.Get("b") == new Item("b", "beta-2", 20), "update visible");
            Check(s.Get("c") is null, "delete visible");
            Check(s.Get("x") is null, "rollback discarded insert");
            Check(s.Get("missing") is null, "missing id is null");
            var all = s.List();
            Check(all.Count == 4, $"4 rows after scenario (got {all.Count})");
            foreach (var i in all) Console.WriteLine($"{variant}: row {i.Id}|{i.Name}|{i.Size}");
        }
        foreach (var f in failures) Console.Error.WriteLine($"{variant}: FAIL {f}");
        Console.WriteLine(failures.Count == 0 ? $"{variant}: OK" : $"{variant}: FAILED {failures.Count}");
        return failures.Count == 0 ? 0 : 1;
    }
}
