using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Builds the <see cref="StepContext"/> of an in-process Step run inside its own activity (plan "How the text reaches the Step"; W6):
/// the injection sections and the typed <see cref="StepContext.InputResults"/> come from the in-memory texts the 'FillInput'
/// activity kept. On a miss (a restart, an eviction) the input runs again and its digests must equal the recorded ones: a Step never
/// runs on data its plan does not describe (SND-WF-018).
/// </summary>
/// <param name="texts">The in-memory texts.</param>
/// <param name="runner">Runs an input again.</param>
internal sealed class StepContexts(IInjectionTexts texts, IStepInputRunner runner)
{
    /// <summary>The context <paramref name="run"/> starts with.</summary>
    /// <param name="run">The run.</param>
    /// <param name="seed">The context without the inputs' contribution.</param>
    /// <param name="inputs">The Step's inputs in declaration order.</param>
    /// <param name="cancellationToken">Cancels the refill.</param>
    /// <returns>The context; SND-WF-018 when a refilled input differs from what was recorded.</returns>
    public async ValueTask<Result<StepContext>> For(StepRun run, StepContext seed, IReadOnlyList<InjectionInputSource> inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(inputs);
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        var results = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var input in inputs.OrderBy(static i => i.Recorded.Index))
        {
            var kept = await InputRefills.Texts(texts, runner, new InputRefill(run.Address, run.State, run.Token, input, WorkflowErrors.InputChanged), cancellationToken).ConfigureAwait(false);
            if (kept.IsError)
            {
                return kept.Error;
            }

            foreach (var (name, text) in kept.Value.Sections)
            {
                sections[name] = text;
            }

            if (kept.Value.Result is { } result)
            {
                results[ResultKey(input.Recorded, inputs)] = result;
            }
        }

        return seed with { Injection = new InjectionSections(sections), InputResults = results };
    }

    /// <summary>The key of an entry's typed result: the input id, or 'id#n' (n = the entry's place) when the Step lists that input more than once, so the second use never overwrites the first.</summary>
    private static string ResultKey(FilledInput entry, IReadOnlyList<InjectionInputSource> all) =>
        all.Where(i => i.Recorded.Input == entry.Input).Skip(1).Any() ? $"{entry.Input.Value}#{entry.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)}" : entry.Input.Value;
}
