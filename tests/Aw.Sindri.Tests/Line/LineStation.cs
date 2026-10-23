using System.Text.Json.Nodes;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Prove;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Line;

/// <summary>
/// The design and plan stations' packet Steps over the fakes (BS-2b): a frozen scope packet at '/scope', the run area and the packet files that read it, the repo's checkout, and a sound
/// design, lens round log and plan whose checks pass, so a test breaks one thing at a time. The stations' addresses differ, as the line's children do.
/// </summary>
internal sealed class LineStation
{
    internal const string DesignAddress = "T-1/design/1";
    internal const string PlanAddress = "T-1/plan/1";
    internal const string ScopeDirectory = "/scope";
    internal const string Checkout = "/work/web";
    internal const string BaseSha = "3288642cd47359fff2642b8bf429976915ca4894";
    internal const string SetHash = "sha256:8c3be257926be79bd2e77561072290d5fb150ac9f4c6a18d2af20fb3463d4233";

    internal const string Design = """{"spec":"# Design\n\n## Summary\nB holds.\n\n## Detail\nMore.\n","sections":[{"id":"sec-1","heading":"Summary","covers":["S1"],"cites":["S1","AC-1"],"assumes":[]},{"id":"sec-2","heading":"Detail","covers":[],"cites":["S1"],"assumes":[]}],"coverage":{"excluded":[]},"decisions":[],"deferred":[{"question":"Q2","owner":"owner","buildMayAssume":"yes"}],"signals":[{"item":"AC-1","kind":"api-transcript","statement":"b holds for 3 requests","runs":"head"}],"contracts":[],"acceptance":{"items":[{"id":"AC-1","text":"WHEN a THEN b.","status":"failing"}]},"escapes":[],"resolutions":[]}""";

    internal const string LensRounds = """[{"round":1,"selected":["product"],"writerModel":"w","lenses":[{"lens":"product","state":"ran","model":"l","findings":[]}]}]""";

    internal const string Plan = """{"phases":[{"id":"P1","title":"Ship b","workstream":"w","deps":[],"size":"S","kind":"feature","writeScope":["src/a.ts"],"readSeeds":["code:src/a.ts"],"rules":["test-change:tests/a.test.ts:the call changes"],"acceptance":[{"id":"AC-1","text":"WHEN a THEN b.","evidence":["api-transcript"],"holdout":false},{"id":"P1-H1","evidence":["api-transcript"],"holdout":true},{"id":"P1-C","evidence":["api-transcript"],"holdout":true}]}],"execReview":[{"phase":"P1","verdict":"executable"}]}""";

    internal const string HoldoutSets = $$"""[{"phase":"P1","count":3,"hash":"{{SetHash}}","canary":true}]""";

    internal LineStation()
    {
        Area = new InMemoryRunArea();
        Files = new InMemoryPacketFiles(Area)
            .Put(ScopeDirectory, "scope-map.json", $$"""{"schemaVersion":1,"change":"expiry","baseSha":"{{BaseSha}}","surfaces":[{"id":"S1","key":"code:src/a.ts"}]}""")
            .Put(ScopeDirectory, "acceptance.json", """{"schemaVersion":1,"parent":null,"items":[{"id":"AC-1","text":"WHEN a THEN b.","status":"failing"}]}""")
            .Put(ScopeDirectory, "questions.json", """{"schemaVersion":1,"questions":[{"id":"Q1"},{"id":"Q2"}]}""")
            .Put(ScopeDirectory, "answers.jsonl", """{"id":"A1","questionIds":["Q1"]}""" + "\n");
        Checkouts = new InMemoryRepoCheckouts().With("web", Checkout);
    }

    internal InMemoryRunArea Area { get; }

    /// <summary>The style packs the station's Steps resolve: 'plain', which asks the gold design's two sections and nothing else.</summary>
    internal FakeStylePacks Styles { get; } = new FakeStylePacks().With(Style("plain", maxWords: null));

    /// <summary>The lens packs the station's intake resolves: 'design-five', with one lens.</summary>
    internal FakeLensPacks Lenses { get; } = new FakeLensPacks().With(new LensPack(LensPackId.From("design-five"), "sha256:" + new string('b', 64), LensPackKind.Design, [new PackLens(LensId.From("product"), "Product", "prompt", LensWhen.Always)]));

    /// <summary>A style pack with the gold design's sections: Summary required and Detail optional, and no wording rules.</summary>
    internal static StylePack Style(string id, int? maxWords) => new(LensPackId.From(id), "sha256:" + new string('a', 64), [new StyleSection("Summary", Required: true, maxWords), new StyleSection("Detail", Required: false, MaxWords: null)], new StyleRules([], MaxLines: null));

    internal InMemoryPacketFiles Files { get; }

    internal InMemoryPackets Packets { get; } = new();

    internal InMemoryRepoCheckouts Checkouts { get; }

    /// <summary>The scope packet fact: its hash as the fake aw-packets computes it, and its directory.</summary>
    internal static string ScopePacket => $$"""{"hash":"{{InMemoryPackets.HashOf(new PacketRef(PacketKind.Scoping, ScopeDirectory))}}","dir":"{{ScopeDirectory}}"}""";

    /// <summary>The context of a Step of the station at <paramref name="address"/>, with the web repo.</summary>
    internal static StepContext Context(string step, string address, ScopingFacts facts) => ScopingStepContexts.Of(step, facts) with { Repo = RepoName.From("web"), Address = address };

    internal DesignIntakeStep DesignIntake() => new(Packets, Files, Styles, Lenses);

    internal DesignEvidenceStep DesignEvidence() => new(Packets, Area, Checkouts, Styles);

    internal PlanIntakeStep PlanIntake() => new(Packets, Files);

    internal PlanEvidenceStep PlanEvidence() => new(Packets, Files, Area, Checkouts);

    internal PlanFreezeStep PlanFreeze() => new(Packets, Checkouts, Holdouts);

    internal InMemoryHoldoutStore Holdouts { get; } = new();

    internal PlanHoldoutStoreStep PlanHoldoutStore(InMemoryBriefFiles drafts) => new(Packets, Files, drafts, Holdouts);

    /// <summary>The design station's evidence facts: the scope and the packs the intake pinned, the sound design, the round log and no accepted risks.</summary>
    internal async Task<ScopingFacts> DesignFacts(Func<JsonObject, JsonObject>? edit = null)
    {
        var intake = await DesignIntake().Run(Context("step.design.intake", DesignAddress, new ScopingFacts { ["scopePacket"] = ScopePacket, ["designRevision"] = "First pass." }), TestContext.Current.CancellationToken);
        var design = (JsonObject)JsonNode.Parse(Design)!;
        return new ScopingFacts
        {
            ["scope"] = intake.Result!.Outputs[FactKey.From("fact:scope")].GetRawText(),
            ["packs"] = intake.Result!.Outputs[FactKey.From("fact:packs")].GetRawText(),
            ["design"] = (edit?.Invoke(design) ?? design).ToJsonString(),
            ["findings"] = "[]",
            ["lensRounds"] = LensRounds,
            ["accepted"] = "[]",
        };
    }

    /// <summary>Runs the design evidence over the sound design; the result carries the candidate packet.</summary>
    internal async Task<BlockStepResult> DesignEvidenceRun(Func<JsonObject, JsonObject>? edit = null) =>
        await DesignEvidence().Run(Context("step.design.evidence", DesignAddress, await DesignFacts(edit)), TestContext.Current.CancellationToken);

    /// <summary>The text of a fact the result wrote.</summary>
    internal static string Fact(BlockStepResult result, string key) => result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText();

    /// <summary>The plan station's intake over the candidate design packet the design evidence made.</summary>
    internal async Task<BlockStepResult> PlanIntakeRun()
    {
        var evidence = await DesignEvidenceRun();
        return await PlanIntake().Run(Context("step.plan.intake", PlanAddress, new ScopingFacts { ["scopePacket"] = ScopePacket, ["designPacket"] = Fact(evidence, "designPacket") }), TestContext.Current.CancellationToken);
    }

    /// <summary>The plan station's evidence facts: the design the intake loaded, the sound plan, the holdout sets.</summary>
    internal async Task<ScopingFacts> PlanFacts(Func<JsonObject, JsonObject>? editPlan = null, string? holdout = null)
    {
        var intake = await PlanIntakeRun();
        var plan = (JsonObject)JsonNode.Parse(Plan)!;
        return new ScopingFacts
        {
            ["scopePacket"] = ScopePacket,
            ["design"] = Fact(intake, "design"),
            ["plan"] = (editPlan?.Invoke(plan) ?? plan).ToJsonString(),
            ["holdoutSets"] = holdout ?? HoldoutSets,
            ["findings"] = "[]",
            ["answerLog"] = "[]",
        };
    }

    /// <summary>Runs the plan evidence over the sound plan; the result carries the candidate plan packet.</summary>
    internal async Task<BlockStepResult> PlanEvidenceRun(Func<JsonObject, JsonObject>? editPlan = null, string? holdout = null) =>
        await PlanEvidence().Run(Context("step.plan.evidence", PlanAddress, await PlanFacts(editPlan, holdout)), TestContext.Current.CancellationToken);

    /// <summary>The text of a run-area file of the design station's instance.</summary>
    internal async Task<string> DesignFile(string name) => (await Area.Read(DesignAddress, name, TestContext.Current.CancellationToken)).Value;

    /// <summary>The text of a run-area file of the plan station's instance.</summary>
    internal async Task<string> PlanFile(string name) => (await Area.Read(PlanAddress, name, TestContext.Current.CancellationToken)).Value;
}
