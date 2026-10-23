using Aw.Architecture.Tests.ProcessFixtures;
using Aw.Architecture.Tests.Rules;

namespace Aw.Architecture.Tests;

/// <summary>ARCH-PROC-001 (deployable-context spec A2d): every ProcessStartInfo in Sindri's server assemblies is made by ChildEnvironment.</summary>
[Trait("Lane", "integration")]
public sealed class ChildProcessTests
{
    // Aw.Sindri.Ui.Desktop is the desktop app head (NativeAOT, never in the pod image); it keeps its one gsettings call.
    private static readonly string[] _serverAssemblies = ["Aw.Sindri.Core", "Aw.Sindri.Features", "Aw.Sindri.Infrastructure", "Aw.Sindri.Host", "Aw.Sindri.Workflows"];

    private static IEnumerable<Type> ServerTypes() =>
        Production.Assemblies.Where(static a => _serverAssemblies.Contains(a.GetName().Name, StringComparer.Ordinal) || a.GetName().Name == "sindri")
            .SelectMany(static a => a.GetTypes());

    [Fact]
    public void Every_ProcessStartInfo_in_Sindri_is_made_by_ChildEnvironment() =>
        Verify.None(ChildProcessRules.Check(ServerTypes(), "ChildEnvironment"));

    [Fact]
    public void A_type_that_constructs_one_outside_the_factory_is_reported_even_in_a_lambda() =>
        Verify.Exactly(ChildProcessRules.Check([typeof(FactoryFixture), typeof(StrayFixture), typeof(CleanFixture)], nameof(FactoryFixture)), ChildProcessRules.Id,
            "StrayFixture constructs a ProcessStartInfo");
}
