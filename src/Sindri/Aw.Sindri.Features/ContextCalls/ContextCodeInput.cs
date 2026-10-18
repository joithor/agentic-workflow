using System.Text.Json;
using Aw.Context.Core.Code;
using Aw.Context.Core.Facade;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// 'input.context.code' (scoping spec C3, W6): the files, tests, data things and schema files around the anchors, read in process from the engine's code
/// lens as the service, cut to the instance's repo. The section 'code' is the readable text; the typed result is the <see cref="CodeView"/> as the route
/// answers it. A refusal, or an instance with no mapped repo, is an honest section and no typed result; callers and callees stay 'unavailable' in the view
/// until the code index exists.
/// </summary>
/// <param name="code">The engine's code lens.</param>
/// <param name="scopes">The service scope for the instance's repo.</param>
/// <param name="json">The host's JSON options.</param>
internal sealed class ContextCodeInput(IContextCode code, ContextInputScopes scopes, JsonSerializerOptions json) : IStepInput, IDescribedBlock
{
    /// <summary>The block id.</summary>
    public const string Id = "input.context.code";

    /// <summary>The section the text is under.</summary>
    public const string Section = "code";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From(Id), BlockKind.StepInput, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.Tools,
        [new ParameterSpec("anchors", ParameterType.Text, Required: true, IsCapabilityField: false)],
        Rung: null, DeskCapable: false);

    /// <inheritdoc/>
    public async ValueTask<InjectionSections> Fill(StepInputContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var anchors = ContextInputArguments.Anchors(context);
        if (anchors.Count == 0)
        {
            return Unavailable("this Step named no anchors");
        }

        var scope = await scopes.For(context.Repo, cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return Unavailable(scope.Error.Code + ": " + scope.Error.Message);
        }

        var view = await code.Code(anchors, draft: false, mapVersion: null, scope.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return Unavailable(view.Error.Code + ": " + view.Error.Message);
        }

        return new InjectionSections(
            new Dictionary<string, string>(StringComparer.Ordinal) { [Section] = ContextCodeText.Code(view.Value, fence: true) },
            JsonSerializer.SerializeToElement(view.Value, json.GetTypeInfo(typeof(CodeView))));
    }

    private static InjectionSections Unavailable(string why) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal) { [Section] = "The context code is unavailable for this Step (" + why + "); it runs without it.\n" });
}
