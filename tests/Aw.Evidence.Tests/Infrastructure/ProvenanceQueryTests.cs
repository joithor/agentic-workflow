using Aw.Evidence.Core;
using Aw.Evidence.Infrastructure.Stack;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The psql adapter against a stub docker that records its arguments and standard input.</summary>
[Lane(TestLane.Integration)]
public sealed class ProvenanceQueryTests : IDisposable
{
    private readonly TempDirectory _root = new();

    [Fact]
    public async Task The_pattern_travels_as_a_psql_variable_and_never_inside_the_sql()
    {
        var pattern = "x'; DROP TABLE profile; --%";
        var query = new DockerPsqlProvenance(Recorder("printf 'seed-admin-1@example.com\\n\\nseed-admin-2@example.com\\n'"));

        var emails = await query.EmailsLikeAsync(pattern, TestContext.Current.CancellationToken);

        Assert.Equal(["seed-admin-1@example.com", "seed-admin-2@example.com"], emails.Value);
        Assert.Equal(
            ["exec", "-i", "postgres_container", "psql", "-U", "postgres", "-At", "-v", "ON_ERROR_STOP=1", "-v", "pattern=" + pattern],
            await File.ReadAllLinesAsync(_root.Combine("args"), TestContext.Current.CancellationToken));
        var sql = await File.ReadAllTextAsync(_root.Combine("stdin"), TestContext.Current.CancellationToken);
        Assert.Equal("SELECT email FROM public.profile WHERE email LIKE :'pattern';\n", sql);
    }

    [Fact]
    public async Task Every_email_is_read_without_variables()
    {
        var query = new DockerPsqlProvenance(Recorder("echo a@example.com"));

        Assert.Equal(["a@example.com"], (await query.AllEmailsAsync(TestContext.Current.CancellationToken)).Value);
        Assert.Equal("SELECT email FROM public.profile;\n", await File.ReadAllTextAsync(_root.Combine("stdin"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_psql_failure_is_a_probe_failure_with_its_message()
    {
        var query = new DockerPsqlProvenance(Recorder("echo 'relation \"public.profile\" does not exist' >&2; exit 3"));

        var emails = await query.AllEmailsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.ProbeFailed.Code, emails.Error.Code);
        Assert.Contains("does not exist", emails.Error.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _root.Dispose();

    private string Recorder(string then) =>
        StubCommand.Write(_root, "docker", $"printf '%s\\n' \"$@\" > '{_root.Combine("args")}'; cat > '{_root.Combine("stdin")}'; {then}");
}
