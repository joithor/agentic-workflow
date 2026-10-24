using Aw.Kernel.Testing;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The files a Step's run directory gets: each declared read fact that is set, shaped as an API fact read shapes it, scrubbed.</summary>
[Lane(TestLane.Unit)]
public sealed class StepFactFilesTests
{
    private static readonly BlockDescriptor _draft = FixtureCatalog.Shipped().Find(BlockId.From("step.scope.draft"))!;   // why: the shipped block is the test's subject; it reads fact:brief and fact:scopeMap among others

    private readonly ContextInputFixtures.MemoryFacts _stored = new();

    private sealed class Redacting : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("hunter2", "[redacted]", StringComparison.Ordinal);
    }

    private ValueTask<Kernel.Result<IReadOnlyDictionary<string, string>>> Build(Dictionary<FactKey, FactValue> facts, BlockDescriptor? descriptor = null) =>
        StepFactFiles.Build(descriptor ?? _draft, facts, _stored, new Redacting(), CancellationToken.None);

    [Fact]
    public async Task A_declared_payload_and_a_declared_text_are_written_named_after_the_fact_without_its_prefix()
    {
        var facts = new Dictionary<FactKey, FactValue>
        {
            [FactKey.From("fact:scopeMap")] = _stored.Put("""{"nodes":[1,2]}"""),
            [FactKey.From("fact:brief")] = _stored.Put("Add a retry to the sync."),
        };

        var files = (await Build(facts)).Value;

        Assert.Equal("brief.json,scopeMap.json", string.Join(',', files.Keys.Order(StringComparer.Ordinal)));
        Assert.Equal("""{"nodes":[1,2]}""", files["scopeMap.json"]);
        Assert.Equal("\"Add a retry to the sync.\"", files["brief.json"]);
    }

    [Fact]
    public async Task A_fact_the_Step_does_not_declare_is_not_written()
    {
        var facts = new Dictionary<FactKey, FactValue>
        {
            [FactKey.From("fact:brief")] = _stored.Put("text"),
            [FactKey.From("fact:unrelated")] = _stored.Put("not for this Step"),
        };

        var files = (await Build(facts)).Value;

        Assert.Equal("brief.json", Assert.Single(files.Keys));
    }

    [Fact]
    public async Task A_declared_fact_that_is_not_set_yet_is_not_written()
    {
        var files = (await Build(new Dictionary<FactKey, FactValue> { [FactKey.From("fact:brief")] = _stored.Put("text") })).Value;

        Assert.False(files.ContainsKey("scopeMap.json"));
    }

    [Fact]
    public async Task A_secret_in_a_fact_is_scrubbed_in_text_and_in_a_payload()
    {
        var facts = new Dictionary<FactKey, FactValue>
        {
            [FactKey.From("fact:brief")] = _stored.Put("the password is hunter2"),
            [FactKey.From("fact:scopeMap")] = _stored.Put("""{"note":"hunter2"}"""),
        };

        var files = (await Build(facts)).Value;

        Assert.All(files.Values, content => Assert.DoesNotContain("hunter2", content, StringComparison.Ordinal));
        Assert.Contains("[redacted]", files["brief.json"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_step_the_catalog_does_not_know_and_an_instance_with_no_facts_get_no_files()
    {
        Assert.Empty((await StepFactFiles.Build(descriptor: null, new Dictionary<FactKey, FactValue>(), _stored, new Redacting(), CancellationToken.None)).Value);
        Assert.Empty((await StepFactFiles.Build(_draft, facts: null, _stored, new Redacting(), CancellationToken.None)).Value);
    }

    [Fact]
    public async Task A_set_fact_whose_content_is_gone_is_an_error_not_a_missing_file()
    {
        var gone = new FactValue("fact:sha256:" + new string('c', 64), new string('c', 64), HeadSha: null);

        var result = await Build(new Dictionary<FactKey, FactValue> { [FactKey.From("fact:brief")] = gone });

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task A_design_lens_gets_the_design_the_scope_and_the_changed_sections_though_its_descriptor_does_not_declare_them()
    {
        var lens = FixtureCatalog.Shipped().Find(BlockId.From("step.lens.run"))!;   // why: the shipped lens frame reads only the queue and the rounds; its subject's facts are not descriptor inputs
        var facts = new Dictionary<FactKey, FactValue>
        {
            [FactKey.From("fact:lensQueue")] = _stored.Put("""["product"]"""),
            [FactKey.From("fact:design")] = _stored.Put("""{"sections":[]}"""),
            [FactKey.From("fact:scope")] = _stored.Put("""{"items":[]}"""),
            [FactKey.From("fact:changedSections")] = _stored.Put("""["s1"]"""),
            [FactKey.From("fact:unrelated")] = _stored.Put("not for this Step"),
        };

        var files = (await StepFactFiles.Build(lens, facts, _stored, new Redacting(), CancellationToken.None, subject: "design")).Value;

        Assert.Equal("changedSections.json,design.json,lensQueue.json,scope.json", string.Join(',', files.Keys.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public async Task A_change_lens_gets_only_its_declared_facts()
    {
        var lens = FixtureCatalog.Shipped().Find(BlockId.From("step.lens.run"))!;
        var facts = new Dictionary<FactKey, FactValue>
        {
            [FactKey.From("fact:lensQueue")] = _stored.Put("""["product"]"""),
            [FactKey.From("fact:design")] = _stored.Put("""{"sections":[]}"""),
        };

        var files = (await StepFactFiles.Build(lens, facts, _stored, new Redacting(), CancellationToken.None, subject: "change")).Value;

        Assert.Equal("lensQueue.json", Assert.Single(files.Keys));
    }
}
