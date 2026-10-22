using Aw.Sindri.Infrastructure.LensPacks;

namespace Aw.Sindri.Tests.LensPacks;

/// <summary>What every <see cref="IOutsidePackFiles"/> does: a pack's files by their path inside it, nothing for a pack that is not there, and no other pack's files.</summary>
public abstract class PackFilesContract
{
    protected abstract string Root { get; }

    private protected abstract IOutsidePackFiles Create();

    protected abstract void Seed(string id, string path, string text);

    [Fact]
    public void A_pack_gives_every_file_under_its_directory_by_its_path_inside_the_pack()
    {
        Seed("mine", "pack.json", "{}");
        Seed("mine", "lenses/a.md", "look for a");

        var files = Create().Files(Root, "mine");

        Assert.Equal(["lenses/a.md", "pack.json"], files!.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);   // why: the pack was seeded
        Assert.Equal("look for a", files["lenses/a.md"]);
    }

    [Fact]
    public void A_pack_that_is_not_there_is_null()
    {
        Seed("mine", "pack.json", "{}");

        Assert.Null(Create().Files(Root, "other"));
    }

    [Fact]
    public void Another_packs_files_are_not_included()
    {
        Seed("mine", "pack.json", "{}");
        Seed("mine-too", "extra.md", "x");

        Assert.Equal(["pack.json"], Create().Files(Root, "mine")!.Keys, StringComparer.Ordinal);   // why: the pack was seeded
    }
}
