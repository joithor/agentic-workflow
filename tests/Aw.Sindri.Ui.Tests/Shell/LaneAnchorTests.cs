using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Theme;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The lane anchors (docs/csharp/sindri-ui.md "Lane anchors"): where each marker sits, so two lanes' merges touch different lines.</summary>
[Lane(TestLane.Unit)]
public sealed class LaneAnchorTests
{
    private static string[] Lines(string path) => File.ReadAllLines(Path.Combine(UiSource.Root, path));

    [Fact]
    public void The_client_anchors_follow_the_public_reads_and_point_at_no_other_file()
    {
        var lines = Lines("src/Sindri/Aw.Sindri.Ui/Api/SindriApiClient.cs");
        var u3 = Array.FindIndex(lines, l => l.Contains("LANE ANCHOR U3'", StringComparison.Ordinal));
        var u8 = Array.FindIndex(lines, l => l.Contains("LANE ANCHOR U8", StringComparison.Ordinal));
        var recipe = Array.FindIndex(lines, l => l.Contains("Task<Result<RecipeView>> Recipe(", StringComparison.Ordinal));

        Assert.True(recipe >= 0 && u3 > recipe && u3 - recipe <= 4, "the U3' marker belongs right after Recipe(...), found at index " + u3.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(u3 + 1, u8);
        Assert.DoesNotContain(lines, l => l.Contains("Calls.cs", StringComparison.Ordinal));
    }
}
