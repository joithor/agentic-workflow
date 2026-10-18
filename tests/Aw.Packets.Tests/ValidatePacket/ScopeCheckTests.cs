using System.Globalization;
using Aw.Packets.App.Features.ValidatePacket;
using Aw.Packets.Core;
using Aw.Packets.Core.Checks;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.ValidatePacket;

/// <summary>PKT-SCOPE-001/002 and the glob semantics.</summary>
public sealed class ScopeCheckTests
{
    private static async Task<ValidatePacketResponse> Validate(PacketBuilder packet, InMemoryRepoSnapshot? repo = null)
    {
        var service = new ValidatePacketService(new PacketInspector(
            new InMemoryPacketSource().With("pkt", PacketFiles.Of(packet.Build())),
            repo ?? new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles)), new InMemorySizesSource());
        var result = await service.Handle(new ValidatePacketRequest("pkt"), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        return result.Value;
    }

    private static IReadOnlyList<CheckResult> Failures(ValidatePacketResponse report, string rule) =>
        [.. report.Checks.Where(c => c.Rule == rule && c.State == CheckState.Fail)];

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/**", true)]
    [InlineData("src/*.cs", true)]
    [InlineData("**/guide.md", true)]
    [InlineData("README.md", true)]
    [InlineData("new: src/c.cs", true)]
    [InlineData("lib/**", false)]
    [InlineData("src/*.ts", false)]
    [InlineData("new: src/a.cs", false)]
    [InlineData("new: ../outside.cs", false)]
    public async Task A_write_scope_must_exist_at_the_base_sha_or_be_declared_new(string scope, bool passes)
    {
        var report = await Validate(A.Packet().With(A.Task("t1").Writing(scope).Example("h", holdout: true, "test")));

        Assert.Equal(passes, Failures(report, "PKT-SCOPE-001").Count == 0);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/a.cs", "src/**", false, false)]
    [InlineData("src/a.cs", "src/b.cs", false, true)]
    [InlineData("src/a.cs", "src/**", true, true)]
    [InlineData("new: src/c.cs", "src/*.cs", false, false)]
    [InlineData("new: src/c.cs", "new: src/c.cs", false, false)]
    [InlineData("docs/**", "src/**", false, true)]
    public async Task Unordered_tasks_must_not_share_a_write_scope(string first, string second, bool ordered, bool passes)
    {
        var t2 = A.Task("t2").Writing(second);
        var report = await Validate(A.Packet()
            .With(A.Task("t1").Writing(first).Example("h", holdout: true, "test"))
            .With(ordered ? t2.After("t1") : t2));

        var failures = Failures(report, "PKT-SCOPE-002");
        Assert.Equal(passes, failures.Count == 0);
        if (!passes)
        {
            Assert.Contains("t1", Assert.Single(failures).Message, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_deep_double_star_glob_is_checked_in_linear_time_not_a_crash()
    {
        var deep = string.Join('/', Enumerable.Repeat("a", 41));
        var repo = new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, new HashSet<string>(PacketBuilder.BaseFiles, StringComparer.Ordinal) { deep });

        var report = await Validate(A.Packet().With(A.Task("t1").Writing("src/**", string.Concat(Enumerable.Repeat("**/", 12)) + "zz").Example("h", holdout: true, "test")), repo);

        Assert.Contains("matches no file", Assert.Single(Failures(report, "PKT-SCOPE-001")).Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_very_long_glob_is_a_scope_failure_not_a_crash()
    {
        var report = await Validate(A.Packet().With(A.Task("t1").Writing("src/**", "src/" + new string('b', 3000)).Example("h", holdout: true, "test")));

        _ = Assert.Single(Failures(report, "PKT-SCOPE-001"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_oversized_scope_entry_is_shortened_in_the_message()
    {
        var report = await Validate(A.Packet().With(A.Task("t1").Writing("src/**", "src/" + new string('b', 3000))));

        var failure = Assert.Single(Failures(report, "PKT-SCOPE-001"));
        Assert.True(failure.Message.Length < 300, failure.Message.Length.ToString(CultureInfo.InvariantCulture));
        Assert.Contains("3004 characters", failure.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/**", "src/a.cs", true)]
    [InlineData("src/**", "src/deep/x/y.cs", true)]
    [InlineData("**", "README.md", true)]
    [InlineData("**/guide.md", "docs/guide.md", true)]
    [InlineData("**/guide.md", "guide.md", true)]
    [InlineData("src/*.cs", "src/a.cs", true)]
    [InlineData("src/*.cs", "src/deep/a.cs", false)]
    [InlineData("src/?.cs", "src/a.cs", true)]
    [InlineData("src/?.cs", "src/ab.cs", false)]
    [InlineData("a/**/b/**/c", "a/x/b/y/z/c", true)]
    [InlineData("a/**/b/**/c", "a/x/c", false)]
    [InlineData("*a*b*", "xaybz", true)]
    [InlineData("*a*b*", "xbya", false)]
    [InlineData("docs/*", "docs", false)]
    public void Glob_semantics(string glob, string path, bool matches) =>
        Assert.Equal(matches, ScopeGlob.Parse(glob)?.Matches(path));

    [Theory, Trait("Lane", "unit")]
    [InlineData("new: src", "is a directory")]
    [InlineData("new: README.md/x", "is a file")]
    [InlineData("/src/a.cs", "relative")]
    public async Task A_new_path_can_be_created_and_a_glob_is_relative(string scope, string message)
    {
        var report = await Validate(A.Packet().With(A.Task("t1").Writing("src/**", scope)));

        Assert.Contains(message, Assert.Single(Failures(report, "PKT-SCOPE-001")).Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("docs/**", "docs", true)]
    [InlineData("a**b", "axyb", true)]
    [InlineData("a**b", "ax/yb", false)]
    public void Glob_edge_cases(string glob, string path, bool matches) =>
        Assert.Equal(matches, ScopeGlob.Parse(glob)?.Matches(path));

    [Fact, Trait("Lane", "unit")]
    public async Task Two_declared_new_paths_can_conflict_as_file_and_directory()
    {
        var report = await Validate(A.Packet()
            .With(A.Task("t1").Writing("new: n"))
            .With(A.Task("t2").Writing("new: n/m")));

        var failures = Failures(report, "PKT-SCOPE-001");
        Assert.Equal(["task t1", "task t2"], failures.Select(static f => f.Subject), StringComparer.Ordinal);
        Assert.Contains("is a directory", failures[0].Message, StringComparison.Ordinal);
        Assert.Contains("'n' is a file", failures[1].Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dot_segment_glob_is_named_as_not_relative() =>
        Assert.Contains("dot segments", Assert.Single(Failures(await Validate(A.Packet().With(A.Task("t1").Writing("src/**", "./src/a.cs"))), "PKT-SCOPE-001")).Message, StringComparison.Ordinal);
}
