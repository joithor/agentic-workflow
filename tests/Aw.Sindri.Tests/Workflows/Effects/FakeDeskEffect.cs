using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflows.Effects;

/// <summary>
/// A stand-in for 'effect.notify.desk' (engine P6 unit (c) builds the real one): it performs each effect identity once,
/// as the real block's existence check does, records every call, and replies 'delivered' to a recipe effect.
/// </summary>
internal sealed class FakeDeskEffect : IEffectKind
{
    private readonly Lock _gate = new();
    private readonly List<EffectContext> _calls = [];
    private readonly HashSet<string> _performed = new(StringComparer.Ordinal);

    /// <summary>When set, every dispatch fails with it (a setup the effect can't run in) instead of performing the effect.</summary>
    public Error? Failure { get; set; }

    /// <summary>Every dispatch, in order.</summary>
    public IReadOnlyList<EffectContext> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>The recipe effect's dispatches only: the engine's own notices (unit (c)) go through this block too.</summary>
    public IReadOnlyList<EffectContext> RecipeCalls => [.. Calls.Where(static c => !c.FromEngine)];

    /// <summary>The recipe effect's identities performed, once each.</summary>
    public IReadOnlyList<string> RecipePerformed => [.. RecipeCalls.Select(static c => c.Identity).Distinct(StringComparer.Ordinal)];

    /// <summary>The identities performed, once each.</summary>
    public IReadOnlyList<string> Performed
    {
        get
        {
            lock (_gate)
            {
                return [.. _performed];
            }
        }
    }

    /// <summary>Registers this fake as the runtime of 'effect.notify.desk', replacing the shipped one.</summary>
    public void Replace(IServiceCollection services) =>
        services.AddKeyedScoped<IEffectKind>("effect.notify.desk", (_, _) => this);

    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        bool existed;
        lock (_gate)
        {
            _calls.Add(context);
            if (Failure is { } failure)
            {
                return ValueTask.FromResult<Result<EffectDone>>(failure);
            }

            existed = !_performed.Add(context.Identity);
        }

        return ValueTask.FromResult<Result<EffectDone>>(new EffectDone(context.FromEngine ? null : OutcomeName.From("delivered"), existed));
    }
}
