using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Packets.App.Features.ValidateScopingPacket;
using Aw.Packets.App.Infrastructure;
using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Digest;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Line;

/// <summary>
/// The line fixture's gold scope, design and plan packets (tests/fixtures/line/gold) against the real tool: each validates,
/// and every parent hash in an index.json (and in freeze.json) is the computed hash of its parent packet, so a drift
/// between the fixture and aw-packets fails here. node selfcheck.mjs checks the fixture's own consistency without hashing.
/// </summary>
[Lane(TestLane.Integration)]   // reads tests/fixtures/line and the scoping fixture's repo from the source tree
public sealed class LineFixtureTests
{
    private static readonly string _root = Path.GetFullPath(Path.Combine(Golden.SourcePath(typeof(LineFixtureTests).Assembly, "Line"), "..", "..", ".."));
    private static readonly string _gold = Path.Combine(_root, "tests", "fixtures", "line", "gold");

    private static async Task<string> ScopeHash(string change) =>
        PacketDigest.Compute((await new FileSystemScopingPacketSource().Read(Path.Combine(_gold, "scope", change), CancellationToken.None)).Value).Value.Value;

    private static async Task<string> LineHash(PacketKind kind, string change) =>
        PacketDigest.Compute(kind, (await new FileSystemLinePacketSource().Read(kind, Path.Combine(_gold, kind.Name, change), CancellationToken.None)).Value).Value.Value;

    private static async Task<JsonElement> Json(params string[] path) =>
        JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine([_gold, .. path]), CancellationToken.None)).RootElement;

    private static async Task<string> FileHash(params string[] path) =>
        LinePacketBuilder.FileHash(await File.ReadAllBytesAsync(Path.Combine([_gold, .. path]), CancellationToken.None));

    private static IEnumerable<string> Parents(JsonElement index) => index.GetProperty("parents").EnumerateArray().Select(static p => p.GetString() ?? string.Empty);

    [Theory]
    [InlineData("expiry")]
    [InlineData("archive")]
    public async Task The_gold_scope_packet_validates_as_a_scoping_packet(string change)
    {
        var service = new ValidateScopingPacketService(new FileSystemScopingPacketSource(), PacketFiles.Json);

        var report = await service.Handle(new ValidateScopingPacketRequest(Path.Combine(_gold, "scope", change)), CancellationToken.None);

        Assert.True(report.Value.Passed, string.Join("; ", report.Value.Checks.Where(static c => c.State == CheckState.Fail).Select(static c => c.Message)));
    }

    [Fact]
    public async Task The_gold_design_and_plan_validate_against_the_fixtures_base_files_and_chain_to_their_parents_by_computed_hash()
    {
        var baseSha = (await Json("plan", "expiry", "packet.json")).GetProperty("baseSha").GetString()!;
        var files = Directory.EnumerateFiles(Path.Combine(_root, "tests", "fixtures", "scoping", "repo"), "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Path.Combine(_root, "tests", "fixtures", "scoping", "repo"), f).Replace(Path.DirectorySeparatorChar, '/'))
            .ToHashSet(StringComparer.Ordinal);
        var inspector = new LinePacketInspector(new FileSystemLinePacketSource(), new InMemoryRepoSnapshot().With(baseSha, files), PacketFiles.Json);

        foreach (var kind in new[] { PacketKinds.Design, PacketKinds.Plan })
        {
            var inspection = await inspector.Inspect(kind, Path.Combine(_gold, kind.Name, "expiry"), "repo", CancellationToken.None);

            Assert.True(inspection.Value.Passed, $"{kind.Name}: " + string.Join("; ", inspection.Value.Checks.Where(static c => c.State == CheckState.Fail).Select(static c => $"{c.Rule} {c.Message}")));
        }

        var scope = await ScopeHash("expiry");
        var design = await LineHash(PacketKinds.Design, "expiry");
        var plan = await LineHash(PacketKinds.Plan, "expiry");
        var designIndex = await Json("design", "expiry", "index.json");
        var planIndex = await Json("plan", "expiry", "index.json");
        Assert.Equal([scope], Parents(designIndex), StringComparer.Ordinal);
        Assert.Equal([design, scope], Parents(planIndex), StringComparer.Ordinal);
        Assert.Equal(await FileHash("design", "expiry", "acceptance.json"), designIndex.GetProperty("acceptance").GetProperty("self").GetString());
        Assert.Equal(await FileHash("scope", "expiry", "acceptance.json"), designIndex.GetProperty("acceptance").GetProperty("extends").GetString());
        Assert.Equal(await FileHash("design", "expiry", "acceptance.json"), planIndex.GetProperty("acceptance").GetProperty("extends").GetString());
        Assert.Equal(JsonValueKind.Null, planIndex.GetProperty("acceptance").GetProperty("self").ValueKind);

        var frozen = (await Json("freeze.json")).GetProperty("frozen").EnumerateArray()
            .ToDictionary(static f => f.GetProperty("packet").GetString() ?? string.Empty, static f => f.GetProperty("hash").GetString(), StringComparer.Ordinal);
        Assert.Equal(scope, frozen["scope/expiry"]);
        Assert.Equal(await ScopeHash("archive"), frozen["scope/archive"]);
        Assert.Equal(design, frozen["design/expiry"]);
        Assert.Equal(plan, frozen["plan/expiry"]);
    }
}
