using System.Reflection;
using Aw.Context.Core.Facade;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An <see cref="IContextEngine"/> that answers only the stored-state gap check, counts the asks and refuses every other call (so a tick that probed a live source would throw).</summary>
public class GapEngineProxy : DispatchProxy
{
    /// <summary>What the check answers.</summary>
    public bool Gap { get; set; } = true;

    /// <summary>How many times it was asked.</summary>
    public int Calls { get; private set; }

    /// <summary>The engine view of this proxy.</summary>
    internal IContextEngine Proxy => (IContextEngine)(object)this;

    internal static GapEngineProxy Create() => (GapEngineProxy)(object)Create<IContextEngine, GapEngineProxy>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name != nameof(IContextEngine.HasSyncGap))
        {
            throw new NotSupportedException(targetMethod?.Name);
        }

        Calls++;
        Result<bool> answer = Gap;
        return new ValueTask<Result<bool>>(answer);
    }
}
