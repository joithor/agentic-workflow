using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ApplyCommand;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>'workflow complete' on a choice Step (DT spec §8.1): the engine writes fact:decision from --outcome, and a hand-written one is refused.</summary>
[Lane(TestLane.Unit)]
public sealed class ChoiceReportTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-7/evidence/1");
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('f', 32));

    private readonly TemporalLikeRuntime _like = new();
    private readonly InMemoryFactStore _facts = new();
    private readonly ChoiceDesk _desk;

    public ChoiceReportTests()
    {
        _desk = new ChoiceDesk(_address);
    }

    [Fact(Timeout = 30_000)]
    public async Task CompleteStep_on_a_choice_step_stores_an_engine_filled_decision()
    {
        var service = new CompleteStepService(_desk, _desk, _like, _facts, new InMemoryCommandNonces(new FakeTimeProvider()), _like);

        var done = await service.Handle(Request(), TestContext.Current.CancellationToken);

        Assert.False(done.IsError, done.IsError ? done.Error.Message : null);
        var report = Assert.IsType<StepCompleted>(Assert.Single(_desk.Sent));
        var decision = Assert.Single(report.Outputs);
        Assert.Equal("fact:decision", decision.Key.Value);
        var content = await _facts.Read(decision.Value, TestContext.Current.CancellationToken);
        Assert.Equal($$"""{"choice":"api","provider":"desk","actor":"{{_like.Current!.Actor}}"}""", Encoding.UTF8.GetString(content.Value.Span));
    }

    [Fact(Timeout = 30_000)]
    public async Task CompleteStep_refuses_a_hand_written_engine_filled_output()
    {
        var nonces = new InMemoryCommandNonces(new FakeTimeProvider());
        var service = new CompleteStepService(_desk, _desk, _like, _facts, nonces, _like);
        var written = new Dictionary<FactKey, ReadOnlyMemory<byte>> { [FactKey.From("fact:decision")] = """{"choice":"db"}"""u8.ToArray() };

        var refused = await service.Handle(Request(written), TestContext.Current.CancellationToken);

        Assert.Equal("SND-CLI-003", refused.Error.Code);
        Assert.Equal("fact:decision is written by the engine on 'step.desk.choose' (fix: drop --output fact:decision)", refused.Error.Message);
        Assert.Empty(_desk.Sent);
        Assert.Equal(NonceClaim.Fresh, (await nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken)).Value);
    }

    private static CompleteStepRequest Request(Dictionary<FactKey, ReadOnlyMemory<byte>>? outputs = null) =>
        new(_address.Value, RunToken.For(1, 1), OutcomeName.From("api"), outputs ?? [], _nonce);

    /// <summary>One instance waiting at a step.desk.choose state, recording what is sent to it.</summary>
    private sealed class ChoiceDesk(InstanceAddress address) : IInstanceDirectory, IInstanceRuntime
    {
        private static readonly BlockDescriptor _descriptor = FixtureCatalog.Shipped().Find(BlockId.From("step.desk.choose"))!;
        private readonly List<Trigger> _sent = [];

        public IReadOnlyList<Trigger> Sent => _sent;

        public ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<AttemptState?>>((AttemptState?)null);

        public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) => ValueTask.FromResult<Result<InstanceAddress>>(address);

        public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) => throw new NotSupportedException("not started here");

        public ValueTask<Result<InstanceView>> Get(InstanceAddress at, CancellationToken cancellationToken) => ValueTask.FromResult<Result<InstanceView>>(View());

        public ValueTask<Result<CommandResult>> Send(InstanceAddress at, CommandNonce nonce, Trigger command, CancellationToken cancellationToken)
        {
            _sent.Add(command);
            return ValueTask.FromResult<Result<CommandResult>>(new CommandResult(View(), "-> evidence-api"));
        }

        public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress at, LaunchId launch, CancellationToken cancellationToken) => throw new NotSupportedException("not requested here");

        public ValueTask<Result<Unit>> Signal(InstanceAddress at, WaitSatisfied delivered, CancellationToken cancellationToken) => throw new NotSupportedException("not signalled here");

        private InstanceView View() => new(
            address, new RecipeVersion(RecipeId.From("workflow.dev-evidence-route"), 1, RecipeHash.From("sha256:" + new string('a', 64))), "route-evidence-desk", Detail: null,
            DateTimeOffset.FromUnixTimeMilliseconds(1_000), string.Empty, [], Question: null,
            new DeskTask(RunToken.For(1, 1), "route-evidence-desk", _descriptor.Id, _descriptor.Outcomes, _descriptor.Outputs, new Dictionary<FactKey, InputFact>(), TimeoutAt: null),
            RunToken.For(1, 1), Children: [], Outcome: null)
        { Id = WorkflowInstanceId.Of(address), Version = 3 };
    }
}
