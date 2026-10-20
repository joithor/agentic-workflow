using Aw.Sindri.Core.Workflow.Blocks;
using Xunit;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The contract every <see cref="IRunArea"/> honours.</summary>
public abstract class RunAreaContract
{
    private const string Address = "T-1/scoping/1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>An empty run area.</summary>
    /// <returns>The run area.</returns>
    protected abstract IRunArea Create();

    /// <summary>A file never written reads as empty text.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_file_never_written_reads_empty()
    {
        var read = await Create().Read(Address, "rounds.jsonl", Token);

        Expect.True(read.IsSuccess);
        Expect.Equal(string.Empty, read.Value);
    }

    /// <summary>Appended lines come back in order, and a replace drops what was there.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Appends_accumulate_and_a_replace_starts_over()
    {
        var area = Create();
        _ = await area.Write(Address, "rounds.jsonl", "a\n", append: true, Token);
        _ = await area.Write(Address, "rounds.jsonl", "b\n", append: true, Token);
        Expect.Equal("a\nb\n", (await area.Read(Address, "rounds.jsonl", Token)).Value);

        _ = await area.Write(Address, "rounds.jsonl", "c\n", append: false, Token);
        Expect.Equal("c\n", (await area.Read(Address, "rounds.jsonl", Token)).Value);
    }

    /// <summary>A file in a sub-directory is its own file, and each instance has its own area.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Names_nest_one_level_and_instances_are_apart()
    {
        var area = Create();
        _ = await area.Write(Address, "scope-packet/brief.md", "x", append: false, Token);
        _ = await area.Write("T-2/scoping/1", "scope-packet/brief.md", "y", append: false, Token);

        Expect.Equal("x", (await area.Read(Address, "scope-packet/brief.md", Token)).Value);
        Expect.Equal("y", (await area.Read("T-2/scoping/1", "scope-packet/brief.md", Token)).Value);
        Expect.Equal(string.Empty, (await area.Read(Address, "brief.md", Token)).Value);
    }

    /// <summary>A name that could leave the instance's directory is refused on write and read.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData("../x")]
    [InlineData("a/../x")]
    [InlineData("/etc/passwd")]
    [InlineData(".hidden")]
    [InlineData("a//b")]
    [InlineData("a\\b")]
    [InlineData("a/b/c/d")]
    [InlineData("")]
    public async Task A_name_that_climbs_out_is_refused(string name)
    {
        var area = Create();

        Expect.True((await area.Write(Address, name, "x", append: false, Token)).IsError);
        Expect.True((await area.Read(Address, name, Token)).IsError);
    }

    /// <summary>A text that is no instance address is refused.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_address_that_is_not_one_is_refused() =>
        Expect.True((await Create().Write("../../etc", "rounds.jsonl", "x", append: true, Token)).IsError);

    /// <summary>The path of a name ends with the name, differs by instance, and a name that climbs out has none.</summary>
    [Fact]
    public void A_path_names_the_directory_of_a_name_per_instance_and_refuses_a_name_that_climbs_out()
    {
        var area = Create();

        var one = area.PathFor(Address, "proof-pack");
        var two = area.PathFor("T-2/scoping/1", "proof-pack");

        Expect.True(one.IsSuccess && two.IsSuccess);
        Expect.True(one.Value.EndsWith("proof-pack", StringComparison.Ordinal));
        Expect.True(one.Value != two.Value);
        Expect.True(area.PathFor(Address, "../x").IsError);
        Expect.True(area.PathFor("../../etc", "proof-pack").IsError);
    }
}
