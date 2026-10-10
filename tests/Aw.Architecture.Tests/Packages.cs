namespace Aw.Architecture.Tests;

/// <summary>
/// The registry of every Aw.* production assembly the architecture rules cover (spec §4b.1). One line per assembly,
/// plus one ProjectReference per project in Aw.Architecture.Tests.csproj. /new-package appends both for each project
/// it generates (Core, Features, Infrastructure, Host, or Core and App in the small shape). RegistryTests fails when a
/// production project in AgenticWorkflow.slnx is missing here (ARCH-REG-001) or a line here doesn't load (ARCH-REG-002).
/// </summary>
internal static class Packages
{
    /// <summary>Assembly names, one per production project.</summary>
    public static IReadOnlyList<string> Production { get; } =
    [
        "Aw.Kernel",
        "Aw.Kernel.Data",
    ];
}
