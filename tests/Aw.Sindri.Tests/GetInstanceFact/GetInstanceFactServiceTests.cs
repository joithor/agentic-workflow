using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.GetInstanceFact;

/// <summary>'GetInstanceFact' (API spec §3.8): the scrubbed body behind one fact row, capped at 256 KiB, an object stays an object when cut.</summary>
[Lane(TestLane.Unit)]
public sealed class GetInstanceFactServiceTests
{
    private const string Sha = "0000000000000000000000000000000000000000000000000000000000000001";

    private sealed class Contents(string text) : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(text)));
    }

    private sealed class Redacting : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("sk-live-123", "[redacted]", StringComparison.Ordinal);
    }

    private static InstanceView With(string key, FactType type, bool produced = true)
    {
        var view = StatusCases.View("desk-task");
        var row = view.Facts[0] with { Key = key, Type = type };
        return view with { Facts = [row], FactShas = produced ? new Dictionary<string, string>(StringComparer.Ordinal) { [key] = Sha } : null };
    }

    private static async Task<Result<FactView>> Read(InstanceView view, string key, string content)
    {
        var instances = new SomeInstances(view);
        var service = new GetInstanceFactService(instances, instances, new Contents(content), new Redacting(), new FixedRepos(), new ListInstances.FixedCaller(new CallerContextBuilder().Human().Build()));
        return await service.Handle(new GetInstanceFactRequest(view.Id, key), CancellationToken.None);
    }

    [Fact]
    public async Task Text_fact_serves_a_string_body()
    {
        var result = await Read(With("fact:spec", FactType.Text), "fact:spec", "the spec");

        Assert.Equal(("fact:spec", FactType.Text, "the spec", false), (result.Value.Key, result.Value.Type, result.Value.Body.GetString(), result.Value.Truncated));
    }

    [Fact]
    public async Task Packet_fact_serves_an_object_body()
    {
        var result = await Read(With("fact:plan", FactType.Packet), "fact:plan", """{"tasks":[{"id":"t1"}],"checks":{"PKT-1":"pass"}}""");

        Assert.Equal(JsonValueKind.Object, result.Value.Body.ValueKind);
        Assert.Equal("t1", result.Value.Body.GetProperty("tasks")[0].GetProperty("id").GetString());
        Assert.False(result.Value.Truncated);
    }

    [Fact]
    public async Task Body_over_256KiB_is_cut_and_stays_an_object()
    {
        var entries = string.Join(',', Enumerable.Range(0, 200).Select(static i => string.Create(CultureInfo.InvariantCulture, $"\"k{i:D3}\":\"{new string('x', 4000)}\"")));

        var result = await Read(With("fact:plan", FactType.Packet), "fact:plan", "{" + entries + "}");

        Assert.True(result.Value.Truncated);
        Assert.Equal(JsonValueKind.Object, result.Value.Body.ValueKind);
        var kept = result.Value.Body.EnumerateObject().Select(static p => p.Name).ToList();
        Assert.InRange(kept.Count, 1, 199);
        Assert.Equal([.. Enumerable.Range(0, kept.Count).Select(static i => string.Create(CultureInfo.InvariantCulture, $"k{i:D3}"))], kept, StringComparer.Ordinal);   // the tail is what is dropped
        Assert.True(Encoding.UTF8.GetByteCount(result.Value.Body.GetRawText()) <= 256 * 1024);
    }

    [Fact]
    public async Task A_text_body_over_the_cap_is_cut_on_a_character_boundary()
    {
        var result = await Read(With("fact:spec", FactType.Text), "fact:spec", new string('é', 200_000));

        Assert.True(result.Value.Truncated);
        Assert.True(Encoding.UTF8.GetByteCount(result.Value.Body.GetString()!) <= 256 * 1024);
    }

    [Theory]
    [InlineData("fact:unknown", true)]
    [InlineData("fact:spec", false)]
    public async Task Undeclared_key_or_one_not_produced_yet_is_SND_ITEM_404(string key, bool produced)
    {
        var result = await Read(With("fact:spec", FactType.Text, produced), key, "x");

        Assert.Equal(SindriErrors.ItemNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Secrets_in_a_fact_are_scrubbed()
    {
        var text = await Read(With("fact:spec", FactType.Text), "fact:spec", "token sk-live-123 here");
        var packet = await Read(With("fact:plan", FactType.Packet), "fact:plan", """{"env":{"KEY":"sk-live-123"}}""");

        Assert.Equal("token [redacted] here", text.Value.Body.GetString());
        Assert.Equal("[redacted]", packet.Value.Body.GetProperty("env").GetProperty("KEY").GetString());
    }
}
