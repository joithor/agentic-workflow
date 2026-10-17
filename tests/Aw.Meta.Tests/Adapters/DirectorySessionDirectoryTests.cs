using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Adapters;

public sealed class DirectorySessionDirectoryTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    [Fact, Trait("Lane", "integration")]
    public async Task A_missing_directory_has_no_sessions()
    {
        var sessions = await new DirectorySessionDirectory(Path.Combine(_dir.Path, "absent"), new InMemoryProcessLiveness()).Live(CancellationToken.None);

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

        var liveness = new InMemoryProcessLiveness();
        liveness.Run(1, DateTimeOffset.UnixEpoch);
        var sessions = await new DirectorySessionDirectory(_dir.Path, liveness).Live(CancellationToken.None);

        var only = Assert.Single(sessions.Value);
        Assert.Equal(("good", string.Empty, 1), (only.Session.Value, only.Name, only.Pid));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_whose_pid_was_reused_by_a_process_that_started_after_the_record_was_written_is_not_live()
    {
        var file = Path.Combine(_dir.Path, "42.json");
        await File.WriteAllTextAsync(file, """{"pid":42,"sessionId":"old"}""", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(file, new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc));
        var liveness = new InMemoryProcessLiveness();
        liveness.Run(42, new DateTimeOffset(2026, 10, 17, 11, 0, 0, TimeSpan.Zero));

        var sessions = await new DirectorySessionDirectory(_dir.Path, liveness).Live(CancellationToken.None);

        Assert.Empty(sessions.Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_whose_process_started_before_its_record_was_last_written_is_live()
    {
        var file = Path.Combine(_dir.Path, "42.json");
        await File.WriteAllTextAsync(file, """{"pid":42,"sessionId":"current"}""", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(file, new DateTime(2026, 10, 17, 11, 0, 0, DateTimeKind.Utc));
        var liveness = new InMemoryProcessLiveness();
        liveness.Run(42, new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero));

        var sessions = await new DirectorySessionDirectory(_dir.Path, liveness).Live(CancellationToken.None);

        Assert.Equal("current", Assert.Single(sessions.Value).Session.Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_host_that_cannot_be_asked_is_the_error_not_an_empty_list()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir.Path, "42.json"), """{"pid":42,"sessionId":"x"}""", TestContext.Current.CancellationToken);
        var liveness = new InMemoryProcessLiveness();
        liveness.FailWith(MetaErrors.HostUnreadable.With("unavailable on macOS: ps could not be run"));

        var sessions = await new DirectorySessionDirectory(_dir.Path, liveness).Live(CancellationToken.None);

        Assert.True(sessions.IsError);
        Assert.Equal(MetaErrors.HostUnreadable.Code, sessions.Error.Code);
    }

    public void Dispose() => _dir.Dispose();
}
