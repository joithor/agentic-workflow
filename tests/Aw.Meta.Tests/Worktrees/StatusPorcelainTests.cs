using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Tests.Worktrees;

public sealed class StatusPorcelainTests
{
    private static readonly string[] _outputs = ["bin", "obj", "artifacts", "node_modules", "dist"];

    [Fact, Trait("Lane", "unit")]
    public void Untracked_files_under_build_output_directories_do_not_count()
    {
        const string output = "?? src/App/bin/Debug/app.dll\n?? src/App/obj/project.assets.json\n?? node_modules/x/index.js\n?? artifacts/log.txt\n?? dist/a.js\n";

        Assert.Empty(StatusPorcelain.Significant(output, _outputs));
    }

    [Fact, Trait("Lane", "unit")]
    public void Any_other_untracked_file_counts()
    {
        const string output = "?? NOTES.md\n?? src/App/obj/x.dll\n?? scratch/todo.txt\n";

        Assert.Equal(["NOTES.md", "scratch/todo.txt"], StatusPorcelain.Significant(output, _outputs));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tracked_change_counts_even_inside_a_build_output_directory()
    {
        const string output = " M bin/tracked.dll\nM  src/a.cs\nA  src/b.cs\n D src/c.cs\n";

        Assert.Equal(["bin/tracked.dll", "src/a.cs", "src/b.cs", "src/c.cs"], StatusPorcelain.Significant(output, _outputs));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rename_counts_by_its_new_path_and_a_quoted_path_is_unquoted()
    {
        const string output = "R  old.cs -> new.cs\n?? \"with space.txt\"\n";

        Assert.Equal(["new.cs", "with space.txt"], StatusPorcelain.Significant(output, _outputs));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_directory_name_only_matches_as_a_whole_path_segment()
    {
        const string output = "?? src/binary/data.txt\n?? objective.md\n?? bin\n";

        Assert.Equal(["src/binary/data.txt", "objective.md", "bin"], StatusPorcelain.Significant(output, _outputs));
    }

    [Fact, Trait("Lane", "unit")]
    public void Nothing_to_report_is_empty_and_the_build_output_list_is_the_caller_s()
    {
        Assert.Empty(StatusPorcelain.Significant(string.Empty, _outputs));
        Assert.Equal(["out/a.dll"], StatusPorcelain.Significant("?? out/a.dll\n?? bin/b.dll\n", ["bin"]));
    }
}
