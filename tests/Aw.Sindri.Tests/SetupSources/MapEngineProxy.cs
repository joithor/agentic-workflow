using System.Reflection;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Sindri.Tests.SetupSources;

/// <summary>An <see cref="IContextEngine"/> that answers only the map status (what the setup list asks), counts the asks and refuses every other call.</summary>
public class MapEngineProxy : DispatchProxy
{
    /// <summary>What the status answers.</summary>
    public Result<MapStatusView> Answer { get; set; } = Context.Core.ContextErrors.NoMap.With("no map in this test");

    /// <summary>How many times it was asked.</summary>
    public int Calls { get; private set; }

    /// <summary>The engine view of this proxy.</summary>
    internal IContextEngine Proxy => (IContextEngine)(object)this;

    internal static MapEngineProxy Create() => (MapEngineProxy)(object)Create<IContextEngine, MapEngineProxy>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name != nameof(IContextEngine.MapStatus))
        {
            throw new NotSupportedException(targetMethod?.Name);
        }

        Calls++;
        return new ValueTask<Result<MapStatusView>>(Answer);
    }
}
