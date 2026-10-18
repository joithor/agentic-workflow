using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// What every shipped block with a runtime does (engine spec §2 "Tests"; E7): the <see cref="DescriptorContract"/> facts,
/// its runtime resolves under its kind interface in a scope, and an already-cancelled token stops it. A binding supplies
/// the pinned hash, the real composition, and a call that gives the block its own token.
/// </summary>
public abstract class BlockContract : DescriptorContract
{
    /// <summary>The block class (it may be internal, which is why a binding names it rather than a type argument).</summary>
    protected abstract Type Block { get; }

    /// <inheritdoc/>
    protected sealed override bool HasRuntime => true;

    /// <summary>Calls the block's one entry (Evaluate, Dispatch, Fill, …) on a fresh instance, passing <paramref name="cancellationToken"/> through.</summary>
    /// <param name="cancellationToken">The token the call must honour.</param>
    /// <returns>The call.</returns>
    protected abstract Task InvokeAsync(CancellationToken cancellationToken);

    /// <summary>The block resolves in a scope as its kind interface and as itself.</summary>
    [Fact]
    public void Runtime_resolves_under_its_kind()
    {
        var provider = Compose();
        using var lifetime = provider as IDisposable;
        using var scope = provider.CreateScope();
        var kind = Descriptor.Kind switch
        {
            BlockKind.Gate => typeof(IGate),
            BlockKind.Step => typeof(IStep),
            BlockKind.Effect => typeof(IEffectKind),
            BlockKind.StepInput => typeof(IStepInput),
            BlockKind.EvidenceProvider => typeof(IEvidenceProvider),
            _ => throw new InvalidOperationException($"unknown block kind {Descriptor.Kind}"),
        };

        Expect.True(scope.ServiceProvider.GetServices(kind).Where(Block.IsInstanceOfType).Take(2).Count() == 1);
        _ = scope.ServiceProvider.GetRequiredService(Block);
    }

    /// <summary>An already-cancelled token throws <see cref="OperationCanceledException"/>.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Cancellation_is_honoured()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var thrown = await Record.ExceptionAsync(() => InvokeAsync(cancelled.Token));

        Expect.True(thrown is OperationCanceledException);
    }
}
