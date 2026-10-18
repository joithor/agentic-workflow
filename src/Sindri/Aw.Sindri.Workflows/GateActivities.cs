using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'EvaluateGates' (engine P7 plan D7, workflow engine spec §5.2): runs the gates of the transition a trigger matches, one
/// call per gate in the transition's order, over the registered <see cref="IGate"/> runtimes, and answers what the pure
/// decision reads as its gate answers. The interpreter runs it as a local activity before it decides, so a replay reads the
/// recorded answers and runs no gate again. A gate with no runtime, one that threw, or one whose argument fact can't be read
/// has no answer, and the decision refuses it as unavailable (fail closed, W5).
/// </summary>
/// <param name="dispatch">Runs a gate by id.</param>
/// <param name="contents">Reads a stored fact a gate's argument names.</param>
/// <param name="logger">The log a gate that threw is noted in.</param>
internal sealed partial class GateActivities(GateDispatch dispatch, IFactContents contents, ILogger<GateActivities> logger)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>The activity: <see cref="Evaluate"/> under the activity's cancellation.</summary>
    /// <param name="run">The gates and what they evaluate.</param>
    /// <returns>The answers, in the transition's order.</returns>
    [Activity("EvaluateGates")]
    public Task<IReadOnlyList<GateAnswer>> EvaluateGatesAsync(GateRun run) => Evaluate(run, ActivityExecutionContext.Current.CancellationToken);

    /// <summary>Evaluates each gate of <paramref name="run"/> once.</summary>
    /// <param name="run">The gates and what they evaluate.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>An answer per gate that has a runtime and returned; none for a gate without a runtime or one that failed.</returns>
    public async Task<IReadOnlyList<GateAnswer>> Evaluate(GateRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        List<GateAnswer> answers = [];
        foreach (var use in run.Gates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await Answer(use, run, cancellationToken).ConfigureAwait(false) is { } answer)
            {
                answers.Add(answer);
            }
        }

        return answers;
    }

    private async Task<GateAnswer?> Answer(GateUse use, GateRun run, CancellationToken cancellationToken)
    {
        try
        {
            var context = await ContextOf(use, run, cancellationToken).ConfigureAwait(false);
            return context is not null && await dispatch(use.Use, context, cancellationToken).ConfigureAwait(false) is { } result ? new GateAnswer(use.Use, result) : null;
        }
        catch (Exception failed) when (failed is not OperationCanceledException) // boundary: a gate that throws has no answer and the decision refuses it as unavailable; the others still run
        {
            Threw(logger, use.Use.Value, run.Address, failed.Message);
            return null;
        }
    }

    /// <summary>What the gate evaluates: its literal and fact arguments resolved (a fact's stored text), the facts they name, the threshold and the tokens left; none when a named fact can't be read.</summary>
    private async Task<GateContext?> ContextOf(GateUse use, GateRun run, CancellationToken cancellationToken)
    {
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var facts = new Dictionary<FactKey, JsonElement>();
        foreach (var (name, value) in use.With)
        {
            if (value.Literal is { } literal)
            {
                arguments[name] = literal;
            }
            else if (value.Fact is { } key && run.Facts.TryGetValue(key, out var stored))
            {
                var read = await contents.Read(stored, cancellationToken).ConfigureAwait(false);
                if (read.IsError)
                {
                    Unreadable(logger, use.Use.Value, key.Value, read.Error.Message);
                    return null;
                }

                var text = JsonSerializer.SerializeToElement(Encoding.UTF8.GetString(read.Value.Span), _json);
                arguments[name] = text;
                facts[key] = text;
            }
        }

        return new GateContext(use.Use, arguments, facts, use.Threshold) { TokensLeft = run.TokensLeft };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "gate {Gate} of {Address} threw and has no answer: {Why}")]
    private static partial void Threw(ILogger logger, string gate, string address, string why);

    [LoggerMessage(Level = LogLevel.Warning, Message = "gate {Gate} has no answer: the fact {Fact} can't be read: {Why}")]
    private static partial void Unreadable(ILogger logger, string gate, string fact, string why);
}
