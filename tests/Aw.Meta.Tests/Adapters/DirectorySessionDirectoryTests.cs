using Aw.Meta.Infrastructure.Files;

namespace Aw.Meta.Tests.Adapters;

public sealed class DirectorySessionDirectoryTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    [Fact, Trait("Lane", "integration")]
    public async Task A_missing_directory_has_no_sessions()
    {
        var sessions = await new DirectorySessionDirectory(Path.Combine(_dir.Path, "absent")).Live(CancellationToken.None);

        Assert.True(sessions.IsSuccess);
        Assert.Empty(sessions.Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Records_that_cannot_be_read_are_skipped_and_extra_fields_ignored()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "1.json"), """{"pid":1,"sessionId":"good","entrypoint":"cli","extra":{"a":[1,2]}}""", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "2.json"), "{broken", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "3.json"), """{"pid":"three","sessionId":"x"}""", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "4.json"), """{"pid":1}""", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "notes.txt"), "ignored", TestContext.Current.CancellationToken);

        var sessions = await new DirectorySessionDirectory(_dir.Path).Live(CancellationToken.None);

        var only = Assert.Single(sessions.Value);
        Assert.Equal(("good", string.Empty, 1), (only.Session.Value, only.Name, only.Pid));
    }

    public void Dispose() => _dir.Dispose();
}
