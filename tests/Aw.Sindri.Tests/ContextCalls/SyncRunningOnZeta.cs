using System.Reflection;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// Wraps the host's real context engine and adds a second intent source, 'zeta', whose sync is always running already (SND-CONTEXT-056). A sync of
/// every source in one engine call then fails as a whole, as the real engine's does when one source is busy, while a sync of 'onyx' alone is the real one.
/// </summary>
public class SyncRunningOnZeta : DispatchProxy
{
    private IContextEngine _inner = null!;

    /// <summary>Replaces the registered engine with the wrapper.</summary>
    /// <param name="services">The host's services.</param>
    public static void Install(IServiceCollection services)
    {
        var original = services.Last(d => d.ServiceType == typeof(IContextEngine));
        _ = services.Remove(original);
        services.Add(new ServiceDescriptor(typeof(IContextEngine), sp =>
        {
            var proxy = Create<IContextEngine, SyncRunningOnZeta>();
            ((SyncRunningOnZeta)(object)proxy)._inner = original.ImplementationFactory is { } factory ? (IContextEngine)factory(sp)
                : (IContextEngine)ActivatorUtilities.CreateInstance(sp, original.ImplementationType!);
            return proxy;
        }, original.Lifetime));
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        ArgumentNullException.ThrowIfNull(args);
        if (string.Equals(targetMethod.Name, nameof(IContextEngine.Sources), StringComparison.Ordinal))
        {
            return new ValueTask<Result<SourcesView>>(Sources((ContextScope)args[0]!, (CancellationToken)args[1]!));
        }

        var busy = string.Equals(targetMethod.Name, nameof(IContextEngine.Sync), StringComparison.Ordinal) && (args[0] is null || string.Equals(args[0] as string, "zeta", StringComparison.Ordinal));
        if (busy)
        {
            return new ValueTask<Result<SyncReport>>(ContextErrors.SyncRunning.With("zeta is being synced already (fix: wait for it; sindri context sources status)"));
        }

        return Forward(targetMethod, args);
    }

    private object? Forward(MethodInfo method, object?[] args)
    {
        try
        {
            return method.Invoke(_inner, args);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            throw e.InnerException;
        }
    }

    private async Task<Result<SourcesView>> Sources(ContextScope scope, CancellationToken cancellationToken)
    {
        var view = await _inner.Sources(scope, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view;
        }

        var intent = view.Value.Sources.First(static r => r.Kind == SourceKind.Intent);
        return view.Value with { Sources = [.. view.Value.Sources, intent with { Source = SourceId.From("zeta") }] };
    }
}
