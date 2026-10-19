using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Facade;
using Aw.Context.Core.History;
using Aw.Context.Core.Map;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// 'input.context.history' (scoping spec C5, W6): the records, decisions and landed changes behind the anchors inside a window, read in process from the
/// engine's history lens as the service, cut to the instance's repo. The section 'history' is the readable text; the typed result is the
/// <see cref="HistoryView"/> as the route answers it, (no live sync marker: a fill is a function of the stored state). A refusal, or an instance with no mapped repo,
/// is an honest section and no typed result. The window is 'since'/'before' (RFC 3339 or a date) as an object, or a bare string meaning 'since'.
/// </summary>
/// <param name="history">The context engine (its facade for this lens).</param>
/// <param name="scopes">The service scope for the instance's repo.</param>
/// <param name="json">The host's JSON options.</param>
internal sealed class ContextHistoryInput(IContextEngine history, ContextInputScopes scopes, JsonSerializerOptions json) : IStepInput, IDescribedBlock
{
    /// <summary>The block id.</summary>
    public const string Id = "input.context.history";

    /// <summary>The section the text is under.</summary>
    public const string Section = "history";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From(Id), BlockKind.StepInput, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.Tools,
        [
            new ParameterSpec("anchors", ParameterType.Text, Required: true, IsCapabilityField: false),
            new ParameterSpec("window", ParameterType.Text, Required: false, IsCapabilityField: false),
        ],
        Rung: null, DeskCapable: false);

    /// <inheritdoc/>
    public async ValueTask<InjectionSections> Fill(StepInputContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var notes = new List<string>();
        var anchors = ContextInputArguments.Anchors(context, notes);
        if (anchors.Count == 0)
        {
            return Unavailable("this Step named no anchors", notes);
        }

        var window = Window(context);
        if (window.IsError)
        {
            return Unavailable(window.Error.Code + ": " + window.Error.Message, notes);
        }

        var scope = await scopes.For(context.Repo, cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return Unavailable(scope.Error.Code + ": " + scope.Error.Message, notes);
        }

        var view = await history.History(anchors, window.Value, MapRead.Current, scope.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return Unavailable(view.Error.Code + ": " + view.Error.Message, notes);
        }

        var stable = ContextInputSections.Stable(view.Value);
        return ContextInputSections.Filled(Section, ContextHistoryText.History(stable, fence: true), notes, stable, json);
    }

    private static InjectionSections Unavailable(string why, List<string> notes) => ContextInputSections.Unavailable(Section, "context history", why, notes);

    private static Aw.Kernel.Result<HistoryWindow> Window(StepInputContext context)
    {
        if (!context.Arguments.TryGetValue("window", out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return HistoryWindow.Open;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => HistoryWindow.Of(value.GetString(), before: null),
            JsonValueKind.Object => HistoryWindow.Of(Text(value, "since"), Text(value, "before")),
            JsonValueKind.Array or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => ContextErrors.QueryInvalid.With("the window argument must be {since?, before?} or a date text, not " + value.ValueKind.ToString().ToLowerInvariant()),
            JsonValueKind.Null or JsonValueKind.Undefined => HistoryWindow.Open,
            _ => ContextErrors.QueryInvalid.With("the window argument must be {since?, before?} or a date text"),
        };
    }

    private static string? Text(JsonElement window, string name) =>
        window.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
