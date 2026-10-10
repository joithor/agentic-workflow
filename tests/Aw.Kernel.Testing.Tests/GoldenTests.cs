namespace Aw.Kernel.Testing.Tests;

/// <summary>Golden checks read and write real files, so they run in the integration lane.</summary>
[Lane(TestLane.Integration)]
public sealed class GoldenTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-golden-").FullName;

    private static Normalization Declared => new(Normalization.IsoTimestamps, Normalization.Guids);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Output_matching_after_normalization_passes() =>
        Golden.Match(
            "run 0e5f38f5-1c2d-4e3f-8a9b-0123456789ab at 2026-10-10T06:00:00.123Z\nok\n",
            Path.Combine(AppContext.BaseDirectory, "Golden", "run.txt"),
            Declared);

    [Fact]
    public void A_difference_names_the_first_differing_line()
    {
        var path = Write("a.txt", "one\ntwo\nthree\n");

        var e = Assert.Throws<GoldenMismatchException>(() => Golden.Match("one\nTWO\nthree\n", path, Normalization.None, update: false));

        Assert.Contains("line 2", e.Message, StringComparison.Ordinal);
        Assert.Contains("expected: two", e.Message, StringComparison.Ordinal);
        Assert.Contains("actual:   TWO", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Extra_trailing_output_is_a_difference()
    {
        var path = Write("b.txt", "one\n");

        _ = Assert.Throws<GoldenMismatchException>(() => Golden.Match("one\ntwo\n", path, Normalization.None, update: false));
    }

    [Fact]
    public void A_missing_golden_file_says_how_to_create_it()
    {
        var e = Assert.Throws<GoldenMismatchException>(
            () => Golden.Match("x", Path.Combine(_dir, "none.txt"), Normalization.None, update: false));

        Assert.Contains(Golden.UpdateVariable + "=1", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Update_mode_writes_the_normalized_output_and_the_next_check_passes()
    {
        var path = Path.Combine(_dir, "new.txt");

        Golden.Match("at 2026-10-10T06:00:00Z\r\n", path, Declared, update: true);

        Assert.Equal("at <timestamp>\n", File.ReadAllText(path));
        Golden.Match("at 2026-12-31T23:59:59Z\n", path, Declared, update: false);
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }
}
