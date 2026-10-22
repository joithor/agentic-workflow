using System.Text.Json;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>'input.context.retrieve' (context spec Phase 10): a question put to the context engine before a Step starts.</summary>
/// <param name="engine">The context engine.</param>
/// <param name="scopes">The service scope for the instance's repo.</param>
/// <param name="facts">Reads the content behind a fact-reference argument.</param>
/// <param name="json">The host's JSON options.</param>
internal sealed class ContextRetrieveInput(IContextEngine engine, ContextInputScopes scopes, ContextInputFacts facts, JsonSerializerOptions json) : IStepInput, IDescribedBlock
{
    /// <summary>The block id.</summary>
    public const string Id = "input.context.retrieve";

    /// <summary>The section the text is under.</summary>
    public const string Section = "context";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From(Id), BlockKind.StepInput, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.Tools,
        [
            new ParameterSpec("q", ParameterType.Text, Required: false, IsCapabilityField: false),
            new ParameterSpec("node", ParameterType.Text, Required: false, IsCapabilityField: false),
            new ParameterSpec("symbol", ParameterType.Text, Required: false, IsCapabilityField: false),
            new ParameterSpec("depth", ParameterType.Number, Required: false, IsCapabilityField: false),
            new ParameterSpec("budget", ParameterType.Number, Required: false, IsCapabilityField: false),
        ],
        Rung: null, DeskCapable: false);

    /// <inheritdoc/>
    public ValueTask<Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
