using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Infrastructure.Parsing;
using Aw.Kernel.Testing;

namespace Aw.CodeIndex.Tests.Parsing;

public sealed class FileParseQuarantineTests : IDisposable
{
    private static readonly RepoName _repo = RepoName.From("alpha");
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "aw-quarantine-" + RealTime.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void A_mark_is_on_disk_before_the_parse_and_gone_after_it()
    {
        var quarantine = new FileParseQuarantine(_directory);
        var blob = new string('a', 40);

        quarantine.Begin(_repo, blob);
        Assert.Equal(blob + "\n", File.ReadAllText(Path.Combine(_directory, "alpha.inflight")));

        quarantine.End(_repo);
        Assert.False(File.Exists(Path.Combine(_directory, "alpha.inflight")));
    }

    [Fact, Trait("Lane", "integration")]
    public void The_list_is_one_blob_id_per_line_and_lines_that_are_not_blob_ids_are_ignored()
    {
        Directory.CreateDirectory(_directory);
        var good = new string('b', 40);
        File.WriteAllText(Path.Combine(_directory, "alpha.quarantine"), $"{good}\nnot a blob\n\n../x\n");

        var quarantine = new FileParseQuarantine(_directory);

        Assert.True(quarantine.Contains(_repo, good));
        Assert.Equal(1, quarantine.Recover(_repo));
    }

    [Fact, Trait("Lane", "integration")]
    public void A_mark_that_is_not_a_blob_id_does_not_poison_the_list()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "alpha.inflight"), "garbage");

        var quarantine = new FileParseQuarantine(_directory);

        Assert.Equal(0, quarantine.Recover(_repo));
        Assert.False(File.Exists(Path.Combine(_directory, "alpha.inflight")));
    }
}
