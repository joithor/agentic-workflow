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
    public async ValueTask<Result<StepContext>> For(StepRun run, StepContext seed, IReadOnlyList<InputSource> inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(inputs);
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        var results = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var input in inputs.OrderBy(static i => i.Recorded.Index))
        {
            var key = new InjectionKey(run.Address, run.State, run.Token, input.Recorded.Input);
            var kept = texts.Get(key);
            if (kept is null)
            {
                var again = await runner.Fill(input.Context, input.Recorded.Index, cancellationToken).ConfigureAwait(false);
                if (again.IsError)
                {
                    return again.Error;
                }

                if (!Same(input.Recorded, again.Value.Metadata))
                {
                    return WorkflowErrors.InputChanged.With($"input {input.Recorded.Input.Value} changed since run {run.Token} recorded it (fix: re-run the Step)");
                }

                kept = again.Value.Texts;
                texts.Put(key, kept);
            }

            foreach (var (name, text) in kept.Sections)
            {
                sections[name] = text;
            }

            if (kept.Result is { } result)
            {
                results[input.Recorded.Input.Value] = result;
            }
        }

        return seed with { Injection = new InjectionSections(sections), InputResults = results };
    }

    private static bool Same(FilledInput recorded, FilledInput now) =>
        recorded.Sections.SequenceEqual(now.Sections.Select(s => s with { Replaced = null }), EntryComparer.Instance) && Equals(recorded.Result, now.Result);

    private sealed class EntryComparer : IEqualityComparer<InjectionEntry>
    {
        public static EntryComparer Instance { get; } = new();

        public bool Equals(InjectionEntry? x, InjectionEntry? y) =>
            x is not null && y is not null && string.Equals(x.Section, y.Section, StringComparison.Ordinal) && x.Bytes == y.Bytes && string.Equals(x.Sha256, y.Sha256, StringComparison.Ordinal);

        public int GetHashCode(InjectionEntry obj) => StringComparer.Ordinal.GetHashCode(obj.Section);
    }
}
