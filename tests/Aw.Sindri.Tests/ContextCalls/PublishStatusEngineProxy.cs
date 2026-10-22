using System.Reflection;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An engine that answers only the publish state.</summary>
public class PublishStatusEngineProxy : DispatchProxy
{
    private MapPublishView _view = null!;

    internal static IContextEngine Create(MapPublishView view)
    {
        var proxy = Create<IContextEngine, PublishStatusEngineProxy>();
        ((PublishStatusEngineProxy)(object)proxy)._view = view;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod?.Name == nameof(IContextEngine.PublishStatus) ? new ValueTask<Result<MapPublishView>>(_view) : throw new NotSupportedException(targetMethod?.Name);
}
