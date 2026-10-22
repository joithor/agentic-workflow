using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Features.Blocks;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The plain sentence on every block descriptor (setup-once spec §5.3.1, SO-E1): every shipped block has one, in plain words, and the sentence is not part of the descriptor hash an approval pins.</summary>
public sealed class BlockSentencesTests
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-sentences-test");

    private static BlockCatalog ShippedCatalog()
    {
        using var provider = Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? _stateRoot : null, new FakeTimeProvider())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        return provider.GetRequiredService<BlockCatalog>();
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_shipped_block_carries_a_plain_sentence_of_at_most_120_characters()
    {
        var catalog = ShippedCatalog();

        var missing = catalog.Blocks.Where(b => string.IsNullOrWhiteSpace(b.Sentence)).Select(b => b.Id.Value).Order(StringComparer.Ordinal).ToList();
        var notPlain = catalog.Blocks.Where(b => b.Sentence is not null && PlainWords.Problem(b.Sentence, 120, sentenceCase: true) is not null)
            .Select(b => $"{b.Id.Value}: {PlainWords.Problem(b.Sentence!, 120, sentenceCase: true)}").Order(StringComparer.Ordinal).ToList();

        Assert.Empty(missing);
        Assert.Empty(notPlain);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_sentences_the_spec_names_are_the_ones_shipped()
    {
        var catalog = ShippedCatalog();

        Assert.Equal("Reserves a work item so no other run starts on it", catalog.Find(BlockId.From("effect.claim.ref"))!.Sentence);
        Assert.Equal("Puts a question or notice in your Needs you list", catalog.Find(BlockId.From("effect.notify.desk"))!.Sentence);
        Assert.Equal("Writes progress lines into the workflow log", catalog.Find(BlockId.From("effect.line.report"))!.Sentence);
        Assert.Equal("Opens or updates a pull request and reads its checks", catalog.Find(BlockId.From("step.ship.open"))!.Sentence);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_sentence_is_kept_for_a_block_the_build_does_not_ship()
    {
        var shipped = ShippedCatalog().Blocks.Select(b => b.Id.Value).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(ShippedBlockSentences.Ids.Where(id => !shipped.Contains(id)).Order(StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_sentence_is_not_part_of_the_descriptor_hash()
    {
        var bare = TestBlocks.Step("step.produce");

        Assert.Equal(PinRules.Hash(bare, SliceHarness.Json), PinRules.Hash(bare with { Sentence = "Starts an AI agent session." }, SliceHarness.Json));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_catalog_fills_a_sentence_from_the_sentences_port_and_keeps_one_a_descriptor_has()
    {
        var own = TestBlocks.Step("step.own") with { Sentence = "Its own words." };
        var catalog = new BlockCatalog([TestBlocks.Step("step.produce"), own, TestBlocks.Step("step.none")], [], new Sentences());

        Assert.Equal("Words for produce.", catalog.Find(BlockId.From("step.produce"))!.Sentence);
        Assert.Equal("Its own words.", catalog.Find(BlockId.From("step.own"))!.Sentence);
        Assert.Null(catalog.Find(BlockId.From("step.none"))!.Sentence);
    }

    private sealed class Sentences : IBlockSentences
    {
        public string? For(BlockId id) => string.Equals(id.Value, "step.produce", StringComparison.Ordinal) ? "Words for produce." : null;
    }
}
