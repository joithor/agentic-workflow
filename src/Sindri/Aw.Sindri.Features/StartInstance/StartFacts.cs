using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.StartInstance;

/// <summary>Stores a start's input facts owner-only and renders the generated ones (spec section 3.11) from the start time.</summary>
/// <param name="facts">Where the input facts are stored.</param>
/// <param name="clock">The start time that an input's generate template reads.</param>
/// <param name="inputRuntimes">The Step inputs this build runs.</param>
/// <param name="callers">The caller, whose actor stamps the start as who started the instance; none stamps nobody.</param>
/// <param name="checks">The start-time checks of input facts; none checks nothing.</param>
internal sealed class StartFacts(IFactStore facts, TimeProvider clock, StepInputRuntimes inputRuntimes, ICallerContextAccessor? callers = null, IEnumerable<IStartFactCheck>? checks = null)
{
    /// <summary>SND-WF-016 when a Step of <paramref name="recipe"/> names an input this build has no runtime for.</summary>
    /// <param name="recipe">The approved recipe.</param>
    /// <returns>The refusal, or null.</returns>
    public Error? NoRuntime(Recipe recipe) => StepInputRefusals.Check(recipe, inputRuntimes);

    /// <summary>Runs every registered <see cref="IStartFactCheck"/> over the seeded facts.</summary>
    /// <param name="request">The start.</param>
    /// <param name="recipe">The recipe being started.</param>
    /// <returns>The refusal naming the first fact that fails and each of its problems, or null when every fact passes.</returns>
    public Error? Validate(StartInstanceRequest request, Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recipe);
        foreach (var (key, fact) in request.Facts)
        {
            foreach (var check in checks ?? [])
            {
                if (check.Check(recipe.Id, key, fact.Content) is { } problem)
                {
                    return SindriErrors.InvalidInput.With($"{key.Value} ({fact.Name}) is not valid for {recipe.Id.Value}: {string.Join("; ", problem.Errors)} (fix: {problem.Fix})");
                }
            }
        }

        return null;
    }

    /// <summary>Stores the supplied facts and, for each input with a generate template the start gave no value for, the rendered text (spec section 3.11): a supplied value always wins.</summary>
    public async ValueTask<Result<StartedFacts>> Store(StartInstanceRequest request, Recipe recipe, CancellationToken cancellationToken)
    {
        var stored = new Dictionary<FactKey, InputFact>();
        var now = clock.GetUtcNow();   // the one start time: generated inputs read it, the instance is stamped with it, the workflow renders the name from it
        foreach (var input in recipe.Inputs.Where(static i => i.Generate is not null))
        {
            if (request.Facts.ContainsKey(input.Key) || InstanceNames.Generate(input.Generate!, now) is not { } text) // why: the Where above keeps only inputs with a template
            {
                continue;
            }

            var made = await facts.Put(System.Text.Encoding.UTF8.GetBytes(text), cancellationToken).ConfigureAwait(false);
            if (made.IsError)
            {
                return made.Error;
            }

            stored[input.Key] = new InputFact("generated", made.Value, Generated: true);
        }

        foreach (var (key, fact) in request.Facts)
        {
            var put = await facts.Put(fact.Content, cancellationToken).ConfigureAwait(false);
            if (put.IsError)
            {
                return put.Error;
            }

            stored[key] = new InputFact(fact.Name, put.Value);
        }

        return new StartedFacts(stored, now, callers?.Current?.Actor);
    }
}
