using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The blocks and events this build ships (spec §12), read from the real composition.</summary>
public sealed class ShippedBlocksTests
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-blocks-test");

    [Fact, Trait("Lane", "unit")]
    public async Task The_catalog_holds_exactly_the_section_12_blocks_and_events()
    {
        await using var provider = Composed();
        var catalog = provider.GetRequiredService<BlockCatalog>();

        Assert.Equal(
            [
                "effect.claim.ref", "effect.notify.desk", "evidence.check", "gate.budget.available", "gate.route.startable", "input.context.code",
                "input.context.history", "input.context.lenses", "step.brief.questions", "step.brief.seed", "step.brief.write", "step.bugfix.judge",
                "step.bugfix.recheck", "step.bugfix.record-candidate", "step.bugfix.start-attempt", "step.checks", "step.desk.choose",
                "step.evidence.api-plan", "step.evidence.api-run", "step.evidence.combine", "step.evidence.db-plan", "step.evidence.db-run",
                "step.evidence.desk", "step.evidence.none", "step.evidence.ui-plan", "step.evidence.ui-run", "step.handtest.run",
                "step.intake.ticket", "step.interview.answer", "step.interview.apply", "step.interview.questions", "step.interview.record",
                "step.judge.choose", "step.owner.decide", "step.produce", "step.report.write", "step.reproduce.test-check", "step.reproduce.ui-check",
                "step.review.fix", "step.review.recheck", "step.review.run", "step.rootcause.investigate", "step.scope.anchor",
                "step.scope.challenge", "step.scope.challenge-log", "step.scope.check", "step.scope.draft", "step.scope.evidence",
                "step.scope.freeze", "step.scope.gather", "step.scope.intake", "step.scope.propose", "step.scope.read-brief",
                "step.scope.read-change", "step.scope.read-tracker", "step.scope.reanchor", "step.scope.review", "step.scope.withdraw", "step.verify",
                "step.verify.design", "step.verify.journey",
            ],
            catalog.Blocks.Select(b => b.Id.Value).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.Equal(["claim.created", "item.routed", "notify.delivered"], catalog.Events.Select(e => e.Kind.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_shipped_descriptor_says_human_only_false()
    {
        var catalog = FixtureCatalog.Shipped();

        Assert.DoesNotContain(catalog.Blocks, static b => b.HumanOnly == false);   // written only when true (DT spec 8.5): false would change the descriptor hash for nothing
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_budget_gate_and_the_two_effects_have_runtimes_and_the_desk_steps_run_on_the_desk()
    {
        await using var provider = Composed();
        await using var scope = provider.CreateAsyncScope();

        _ = Assert.IsType<BudgetAvailableGate>(Assert.Single(scope.ServiceProvider.GetServices<IGate>()));
        Assert.Empty(scope.ServiceProvider.GetServices<IStep>());
        Assert.Equal([typeof(ClaimRefEffect), typeof(NotifyDeskEffect)], scope.ServiceProvider.GetServices<IEffectKind>().Select(static e => e.GetType()));   // engine P6
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("step.produce", new[] { "done" }, "fact:output", new[] { "done" })]
    [InlineData("step.checks", new[] { "passed", "failed" }, "fact:findings", new[] { "failed" })]
    [InlineData("step.verify", new[] { "accepted", "rejected" }, "fact:findings", new[] { "rejected" })]
    public void Each_desk_step_declares_its_outcomes_and_required_output(string id, string[] outcomes, string output, string[] requiredOn)
    {
        var step = FixtureCatalog.Shipped().Find(BlockId.From(id))!;   // why: the catalog test pins every shipped id

        Assert.True(step.DeskCapable);
        Assert.Equal(outcomes, step.Outcomes.Select(o => o.Value), StringComparer.Ordinal);
        var spec = Assert.Single(step.Outputs);
        Assert.Equal(output, spec.Fact.Value);
        Assert.Equal(requiredOn, spec.RequiredOn.Select(o => o.Value), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("effect.claim.ref", Capability.Effects | Capability.WriteScope)]
    [InlineData("effect.notify.desk", Capability.Effects)]
    [InlineData("gate.budget.available", Capability.None)]
    [InlineData("gate.route.startable", Capability.None)]
    public void Each_block_declares_what_it_reaches(string id, Capability capabilities) =>
        Assert.Equal(capabilities, FixtureCatalog.Shipped().Find(BlockId.From(id))!.Capabilities);   // why: the catalog test pins every shipped id

    [Theory, Trait("Lane", "unit")]
    [InlineData("item.routed", "router.stub", EventRoute.Item, new[] { "startable", "notStartable" }, new[] { "fact:stepSpec" })]
    [InlineData("claim.created", "system:dispatcher", EventRoute.Causation, new[] { "created", "claimExists" }, new string[0])]
    [InlineData("notify.delivered", "system:dispatcher", EventRoute.Causation, new[] { "delivered" }, new string[0])]
    public void Each_event_declares_its_producer_route_outcomes_and_facts(string kind, string producer, EventRoute route, string[] outcomes, string[] facts)
    {
        var e = FixtureCatalog.Shipped().FindEvent(EventKind.From(kind))!;   // why: the catalog test pins every shipped kind

        Assert.Equal(producer, e.Producer);
        Assert.Equal(route, e.Route);
        Assert.Equal(outcomes, e.Outcomes.Select(o => o.Value), StringComparer.Ordinal);
        Assert.Equal(facts, e.Facts.Select(f => f.Value), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("effect.claim.ref", "sha256:2a44426d862b3721014969dfcc03a7851f56551f2b9bec9f340e500353a9abd3")]
    [InlineData("effect.notify.desk", "sha256:c3cb05f7d73c6c539b05acb87c15185c4a9b81d12fb27626b59f12b94e157b07")]
    [InlineData("gate.budget.available", "sha256:2e11428fb8765eb8e43f62db6dce8dd654cddd1b6685e6c61080037174408135")]
    [InlineData("gate.route.startable", "sha256:00a724a0231157eba73e8b90950bde6e40fb9e6f17f03f955d3896e6aa2fadf0")]
    [InlineData("step.checks", "sha256:8d6a4302bc9aa7ab780620c014cbc2954c3b9e4cef153be63bb1a2eeb72c2fe8")]
    [InlineData("step.produce", "sha256:b86ef5f788d73f55217a97d0158f1caea49b09d19f4878858acd040434505370")]
    [InlineData("step.verify", "sha256:076936b6ec84ebf12669ca18a2525adb4c3922e86ed4c6cfc16c975e55944d3d")]
    public void Every_shipped_descriptor_hashes_as_before_the_decision_tree_fields(string id, string hash) =>
        Assert.Equal(hash, PinRules.Hash(FixtureCatalog.Shipped().Find(BlockId.From(id))!, SliceHarness.Json).Value);   // why: no approved recipe may drift-park because a nullable field was added

    [Fact, Trait("Lane", "unit")]
    public void Every_shipped_descriptor_has_a_pinned_hash()
    {
        var actual = FixtureCatalog.Shipped().Blocks.OrderBy(static b => b.Id.Value, StringComparer.Ordinal)
            .Select(static b => b.Id.Value + " " + PinRules.Hash(b, SliceHarness.Json).Value);

        Assert.Equal(_pinnedHashes, actual, StringComparer.Ordinal);   // why: a descriptor's hash is what a recipe pins; it moves only by an owner-approved, listed change
    }

    private static readonly string[] _pinnedHashes = [
        "effect.claim.ref sha256:2a44426d862b3721014969dfcc03a7851f56551f2b9bec9f340e500353a9abd3",
        "effect.notify.desk sha256:c3cb05f7d73c6c539b05acb87c15185c4a9b81d12fb27626b59f12b94e157b07",
        "evidence.check sha256:5e8052c5aaf078f6a487a5822fa63d9821a011fa480205fe5bbc8e9f07092d62",
        "gate.budget.available sha256:2e11428fb8765eb8e43f62db6dce8dd654cddd1b6685e6c61080037174408135",
        "gate.route.startable sha256:00a724a0231157eba73e8b90950bde6e40fb9e6f17f03f955d3896e6aa2fadf0",
        "input.context.code sha256:5123ce153cfdb6fa6fbfc9b2389f86c50ced60c76c4ca856158159ad6c323094",
        "input.context.history sha256:f0530a3e621910fb26ceace373310f46b2b35b46173a72ada286bfe0268392e2",
        "input.context.lenses sha256:10e07eb604a8092412459861c964ccd631089a1c192fa246826c29b2b82d5f3e",
        "step.brief.questions sha256:d57f4e4fa07e9ac219e4012b0f4724dedece71778638d145d42a76ee1a7831d6",
        "step.brief.seed sha256:082179dd9bdf0756ffc2efb58c7ee0ccfbe9c6c259d060af83def5f348070740",
        "step.brief.write sha256:74ab2c64882b66558c7b6595286cf5adf4f4e8e81cac125eb35f97ad9bafdca2",
        "step.bugfix.judge sha256:b85ef223337f347fa5644a3c2739dbd330b13895ffe1b919a10f779ceb65e386",
        "step.bugfix.recheck sha256:59cec1093d9bb47e31d84ba62b50dbbf6942724097032aa39bc01e462bcdcdc5",
        "step.bugfix.record-candidate sha256:e4624e9608c06bae38a456f5dc271e4469f5e77b03168c5b0c8674c3067c5867",
        "step.bugfix.start-attempt sha256:98817771c1c63c9232fd20b42bf32ff44a910b03887702eb23a034b9b39d107d",
        "step.checks sha256:8d6a4302bc9aa7ab780620c014cbc2954c3b9e4cef153be63bb1a2eeb72c2fe8",
        "step.desk.choose sha256:f01cd85115d1e9d1e12afdb5cdae46a3bdbf7a033d0f95d9973c7847ca6e062c",
        "step.evidence.api-plan sha256:6569f1b231e81b1c2766f6d6a2c921d913902f8bb018397ef406eb561a925988",
        "step.evidence.api-run sha256:d4e136c9e5435a8485879e5910510eccd99b6cd7a200ba74248fb4a8c405991d",
        "step.evidence.combine sha256:4763332a755d9790c0f16f7a8bc90e0327e11ca25fddb9f95c46944d06afb5a4",
        "step.evidence.db-plan sha256:2a7a0b5fc3bf5d2d73dc01e234d5166ba0ffc29bdf6faaeb409bb3ddc198e1d5",
        "step.evidence.db-run sha256:2a11047b4e9b0ebba08c94e6f025b4ef5f1b8efe7f785443255db4e237e6b7d8",
        "step.evidence.desk sha256:6f4e09b7fad8b4a095386eaa2083406a84245702cfb6d887a3a3d7934dcce0fe",
        "step.evidence.none sha256:3a59ef573f878e44c196fac08806a30b47d78f633e594e2db16d04f4f7cefa8c",
        "step.evidence.ui-plan sha256:3e4799c7fda03eda86807f35f6366a153f49219339c9c1c3b1fc4e323143900a",
        "step.evidence.ui-run sha256:cc61bf599ffbc47677b7e719566da7511d3e9674489741c98e862529912e7ad9",
        "step.handtest.run sha256:3ec327e0820b5cb0b02b62c6ee95b93f74f626a800f095d751811aff55bd4a49",
        "step.intake.ticket sha256:ea24ee0f0a02b06b48469073d11591f0ed25a9c803c86c8e439cf096e0e2b04f",
        "step.interview.answer sha256:6fdbc0aa65ff51935b3daba0e56081fc082e175b65a65d915fe7bd71bf8f0fbe",
        "step.interview.apply sha256:82320c6da188ce0214f3a6750782845a4bd0f79118d51f68098000a489d6971d",
        "step.interview.questions sha256:1a3747c7caab648ec46ad80bd0f9463678e910331a7f3a99c9d5eb352e486911",
        "step.interview.record sha256:fd0e7dfb21b08317f8b98dd809927da7b37fd9029588e5cf3780dcb773ec7e6e",
        "step.judge.choose sha256:7e172d1a423e48ffc9c053c3a5d4ff4edfa5753518e339170a73c685f726b2a6",
        "step.owner.decide sha256:8df7dfe0c17e2b53dc326b5dc77352b48520495c3e64ec3d52e12e662992237b",
        "step.produce sha256:b86ef5f788d73f55217a97d0158f1caea49b09d19f4878858acd040434505370",
        "step.report.write sha256:d7b9f03e2b946166e8d958866b8d9543e16286bcf21d0f31e0815d29918c40c1",
        "step.reproduce.test-check sha256:53c108eccc335ecce0339b1dd8c390fd7b9acda30c799427cf539e96db7daae8",
        "step.reproduce.ui-check sha256:db810e660ef0b51326ea417be46017ea844537bde8b3b32b7968314aa6f0348d",
        "step.review.fix sha256:822ac918431cb0b69bbd86edc650ccf80e9c1a1865cf640f00e05217d8ff16e1",
        "step.review.recheck sha256:87fe20fa2000cb66b32f09bed01b8a150ba79aace78936743295b662fb5d6337",
        "step.review.run sha256:a1b6aeacbdf6793b41acecfcfc7f730a626434cde3ca6b3c692d98a019b2f892",
        "step.rootcause.investigate sha256:089db1c623c8ac46b368e17c20432e4384db243721ce09c7848cd940c23cd6d0",
        "step.scope.anchor sha256:386744bd505765e9e639bdbecd10bd90afed0d7d2204f80c448ddb2189b651ce",
        "step.scope.challenge sha256:d6929a35f012fe93402194238296b82c8ac6d6846ca5175c0e54db19110268d9",
        "step.scope.challenge-log sha256:5b31d3b3bcad028a6d982a27491205750bcab3ec4225b0a1265a13c9d77809e5",
        "step.scope.check sha256:d6e0476a8fc9ae6367ce832524582d73a686794f41deef6f5799829dca161a10",
        "step.scope.draft sha256:4537caca7bdb97f8101bd6c99e49ec8b59ac1c43527baab913d52db1671d9803",
        "step.scope.evidence sha256:61227542a0f3a8d36746dbfba7493292720deea94f0ec774be477702ea6d4d96",
        "step.scope.freeze sha256:b5bb435675715d8516db3442fc66f8946d6a918064c48fd49aad6d015f45ee65",
        "step.scope.gather sha256:bca40d30800053d619458116fd0efc806fdbf564f224fec3043806b9b635cfcd",
        "step.scope.intake sha256:506f001a0a159768445453e2d88fb74f859c7ae1cbec09439bc00988fd355ceb",
        "step.scope.propose sha256:f265276967764afcc97d667ef7c0a51330f9118483d16abbd144f11ec066487b",
        "step.scope.read-brief sha256:34ed392d91e7189a9219fd6f9f586f716dc76368ef52f3b0192440a7dcf3e200",
        "step.scope.read-change sha256:450d6ba90dccd1fc41143ea96d8db5f87aa2b294218a536ff368466a2ee3a94d",
        "step.scope.read-tracker sha256:1e767ceeda8672f4840d2314115a40d6f140481de25813cf487fc10bf82c1d4b",
        "step.scope.reanchor sha256:7449c792e00cf503319435ae0b737bd0efd493eb90a7f8c01880dcdb841acb5f",
        "step.scope.review sha256:bdce296798dc103c89c10eb86f0ddd8b3b620497fbc85b241ec6c97ca501a1f0",
        "step.scope.withdraw sha256:f4ee57b7ed70b510bc06e7a84594387159a8f69edb8e4da4a660aca30e991749",
        "step.verify sha256:076936b6ec84ebf12669ca18a2525adb4c3922e86ed4c6cfc16c975e55944d3d",
        "step.verify.design sha256:f64255db776db059a66d31bf1234d42fde46a2ebb7b2fb0568c21ac3ec44cdd5",
        "step.verify.journey sha256:f8b1dcef144b670930540e962818c004b78f02d84803253e5d96f18ff4c3e877",
    ];

    [Fact, Trait("Lane", "unit")]
    public async Task The_choice_steps_are_desk_capable_descriptors_without_runtimes()
    {
        await using var provider = Composed();
        await using var scope = provider.CreateAsyncScope();
        var catalog = provider.GetRequiredService<BlockCatalog>();

        foreach (var id in new[] { "step.judge.choose", "step.desk.choose" })
        {
            var step = catalog.Find(BlockId.From(id))!;   // why: the catalog test pins both ids
            Assert.True(step.DeskCapable);
            Assert.Equal(BlockKind.Step, step.Kind);
            var decision = Assert.Single(step.Outputs);
            Assert.Equal(("fact:decision", true, true), (decision.Fact.Value, decision.Every, decision.EngineFilled));
            Assert.Empty(decision.RequiredOn);
        }

        Assert.True(catalog.Find(BlockId.From("step.desk.choose"))!.HumanOnly);   // why: the catalog test pins both ids
        Assert.Equal(BlockRuntime.Block, catalog.Find(BlockId.From("step.judge.choose"))!.Runtime);

        Assert.Empty(scope.ServiceProvider.GetServices<IStep>());   // no runtime until the block runtime (DT-J)
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_outcomes_from_and_inputs_from_names_a_list_parameter()
    {
        var from = FixtureCatalog.WithFixtures().Blocks.Where(b => b.OutcomesFrom is not null || b.InputsFrom is not null).ToList();

        Assert.Equal(["step.desk.choose", "step.judge.choose"], from.Select(b => b.Id.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var block in from)
        {
            foreach (var named in new[] { block.OutcomesFrom, block.InputsFrom }.OfType<string>())
            {
                // "choices", or "<stateParam>.<listParam>" for a twin: the first names a text parameter holding a state, the second a list parameter of that state's Step
                var parameter = named.Contains('.', StringComparison.Ordinal) ? named[..named.IndexOf('.', StringComparison.Ordinal)] : named;
                var type = Assert.Single(block.Parameters, p => p.Name == parameter).Type;
                Assert.Equal(named.Contains('.', StringComparison.Ordinal) ? ParameterType.Text : ParameterType.List, type);
            }
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_descriptor_without_the_new_fields_serializes_without_them()
    {
        var json = System.Text.Encoding.UTF8.GetString(AwJson.Serialize(FixtureCatalog.Shipped().Find(BlockId.From("step.produce")), SliceHarness.Json));

        Assert.DoesNotContain("outcomesFrom", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inputsFrom", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("every", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("engineFilled", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("humanOnly", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtime", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_descriptor_kind_matches_its_id_prefix()
    {
        var blocks = FixtureCatalog.WithFixtures().Blocks;

        Assert.NotEmpty(blocks);
        Assert.All(blocks, b => Assert.Equal(BlockKinds.Of(b.Id), b.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_gate_has_a_rung_and_no_other_block_does()
    {
        var blocks = FixtureCatalog.WithFixtures().Blocks;

        Assert.All(blocks.Where(b => b.Kind == BlockKind.Gate), b => Assert.NotNull(b.Rung));
        Assert.All(blocks.Where(b => b.Kind != BlockKind.Gate), b => Assert.Null(b.Rung));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_effect_is_capability_bearing() =>
        Assert.All(FixtureCatalog.WithFixtures().Blocks.Where(b => b.Kind == BlockKind.Effect), b => Assert.True(b.Capabilities.HasFlag(Capability.Effects)));

    private static ServiceProvider Composed() =>
        Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? _stateRoot : null, new FakeTimeProvider())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
}
