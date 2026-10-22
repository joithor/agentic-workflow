using System.Reflection;
using Aw.Context.Core.Facade;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Sindri reaches the context engine through IContextEngine only (context spec §4): never its store or file ports.</summary>
public sealed class ContextFacadeBoundaryTests
{
    private static readonly Assembly[] _sindri =
    [
        typeof(Core.SindriErrors).Assembly,
        typeof(Features.ContextCalls.ImportContextMapRequest).Assembly,
        typeof(Infrastructure.Registration.SindriInfrastructureServiceCollectionExtensions).Assembly,
        typeof(Host.SindriHeaders).Assembly,
    ];

    [Fact, Trait("Lane", "unit")]
    public void No_Sindri_type_takes_a_context_port_other_than_IContextEngine()
    {
        var contextCore = typeof(IContextEngine).Assembly;
        var offenders = _sindri
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(c => c.GetParameters())
                .Where(p => Types(p.ParameterType).Any(t => t.IsInterface && t.Assembly == contextCore && t != typeof(IContextEngine) && t != typeof(IScopeProposals)))
                .Select(p => $"{t.FullName}({p.ParameterType.Name} {p.Name})"))
            .ToList();

        Assert.True(offenders.Count == 0, "Sindri must use IContextEngine, not these context ports: " + string.Join("; ", offenders));
    }

    /// <summary>A type and every generic argument inside it (IEnumerable&lt;IGraphStore&gt;, Lazy&lt;…&gt;, Func&lt;…&gt;).</summary>
    private static IEnumerable<Type> Types(Type type) =>
        type.IsGenericType ? [type, .. type.GetGenericArguments().SelectMany(Types)] : [type];
}
