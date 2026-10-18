using System.Reflection;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An engine that answers only 'sources': the one call the sync-gaps service makes.</summary>
public class EngineSourcesProxy : DispatchProxy
{
    public SourcesView? View { get; set; }

    public int Calls { get; private set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name != nameof(IContextEngine.Sources))
        {
            throw new NotSupportedException(targetMethod?.Name);
        }

        Calls++;
        Result<SourcesView> answer = View ?? throw new InvalidOperationException("no sources view set");
        return new ValueTask<Result<SourcesView>>(answer);
    }
}
