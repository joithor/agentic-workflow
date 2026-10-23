using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Host.Commands.Scoping;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The fact files land in the run directory, none can land outside it, and the readers of '--facts' find them.</summary>
[Lane(TestLane.Unit)]
public sealed class StepFactWriterTests : IDisposable
{
    private readonly string _run = Directory.CreateTempSubdirectory("aw-facts-").FullName;

    public void Dispose() => Directory.Delete(_run, recursive: true);

    [Fact]
    public void The_files_are_written_into_the_run_directory()
    {
        var problem = StepFactWriter.Write(_run, new Dictionary<string, string>(StringComparer.Ordinal) { ["workspace.json"] = "\"/work/docs\"", ["docsScope.json"] = """{"base":"abc"}""" });

        Assert.Null(problem);
        Assert.Equal("\"/work/docs\"", File.ReadAllText(Path.Combine(_run, "workspace.json")));
        Assert.Equal("""{"base":"abc"}""", File.ReadAllText(Path.Combine(_run, "docsScope.json")));
    }

    [Fact]
    public void A_retry_rewrites_a_file_a_first_launch_left()
    {
        _ = StepFactWriter.Write(_run, new Dictionary<string, string>(StringComparer.Ordinal) { ["brief.json"] = "\"old\"" });

        _ = StepFactWriter.Write(_run, new Dictionary<string, string>(StringComparer.Ordinal) { ["brief.json"] = "\"new\"" });

        Assert.Equal("\"new\"", File.ReadAllText(Path.Combine(_run, "brief.json")));
    }

    [Theory]
    [InlineData("../escape.json")]
    [InlineData("a/b.json")]
    [InlineData("/etc/escape.json")]
    public void A_name_that_is_not_one_file_in_the_directory_is_refused_and_nothing_is_written(string name)
    {
        var problem = StepFactWriter.Write(_run, new Dictionary<string, string>(StringComparer.Ordinal) { ["ok.json"] = "1", [name] = "2" });

        Assert.NotNull(problem);
        Assert.Contains(name, problem.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_run));
    }

    [Fact]
    public async Task The_scoping_fact_reader_finds_what_the_launch_wrote_with_the_shape_docs_check_expects()
    {
        _ = StepFactWriter.Write(_run, new Dictionary<string, string>(StringComparer.Ordinal) { ["workspace.json"] = "\"/work/docs\"", ["docsScope.json"] = """{"base":"abc"}""" });

        var read = await ScopingFactDirectory.Read(_run, CancellationToken.None);

        Assert.Equal("/work/docs", JsonSerializer.Deserialize<string>(read.Value["workspace"]));
        Assert.Equal("abc", JsonDocument.Parse(read.Value["docsScope"]).RootElement.GetProperty("base").GetString());
    }
}
