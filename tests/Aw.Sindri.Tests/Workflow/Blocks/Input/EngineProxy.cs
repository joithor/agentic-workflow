using System.Reflection;
using Aw.Context.Core.Facade;
using Aw.Context.Testing.Engine;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>
/// An <see cref="IContextEngine"/> for the context input tests: the calls of the facade a stub implements (IContextLenses, IContextHistory or IContextCode) go
/// to the stub, every other call to <see cref="FakeContextEngine"/>. Sindri takes the whole engine, so a test needs one without writing forty members.
/// </summary>
#pragma warning disable CA1852 // DispatchProxy generates a subclass at run time, so the type cannot be sealed
internal class EngineProxy : DispatchProxy
#pragma warning restore CA1852
{
    private object _stub = new();

    /// <summary>The engine whose <paramref name="stub"/> facade answers.</summary>
    /// <param name="stub">A stub implementing one of the facade interfaces.</param>
    /// <returns>The engine.</returns>
    internal static IContextEngine Of(object stub)
    {
        var engine = Create<IContextEngine, EngineProxy>();
        ((EngineProxy)(object)engine)._stub = stub;
        return engine;
    }

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var target = targetMethod!.DeclaringType!.IsInstanceOfType(_stub) ? _stub : FakeContextEngine.Instance;
        try
        {
            return targetMethod.Invoke(target, args);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            throw e.InnerException;
        }
    }
}
