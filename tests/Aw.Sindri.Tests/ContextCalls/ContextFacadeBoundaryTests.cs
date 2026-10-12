using System.Reflection;
using Aw.Context.Core.Facade;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Sindri reaches the context engine through IContextEngine only (context spec §4): never its store or file ports.</summary>
public sealed class ContextFacadeBoundaryTests
{
    private static readonly Assembly[] _sindri =
    [
        typeof(Aw.Sindri.Core.SindriErrors).Assembly,
        typeof(Aw.Sindri.Features.ContextCalls.ImportContextMapRequest).Assembly,
        typeof(Aw.Sindri.Infrastructure.Registration.SindriInfrastructureServiceCollectionExtensions).Assembly,
        typeof(Aw.Sindri.Host.SindriHeaders).Assembly,
    ];

    [Fact, Trait("Lane", "unit")]
    public void No_Sindri_type_takes_a_context_port_other_than_IContextEngine()
    {
        var contextCore = typeof(IContextEngine).Assembly;
        var offenders = _sindri
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(c => c.GetParameters())
                .Where(p => p.ParameterType.IsInterface && p.ParameterType.Assembly == contextCore && p.ParameterType != typeof(IContextEngine))
                .Select(p => $"{t.FullName}({p.ParameterType.Name} {p.Name})"))
            .ToList();

        Assert.True(offenders.Count == 0, "Sindri must use IContextEngine, not these context ports: " + string.Join("; ", offenders));
    }
}
