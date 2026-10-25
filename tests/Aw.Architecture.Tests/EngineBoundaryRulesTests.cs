using System.Xml.Linq;
using Aw.Architecture.Tests.Rules;

namespace Aw.Architecture.Tests;

/// <summary>The Temporal boundary (API spec P13, E-30): only the adapter project references the SDK, and nothing else uses its types.</summary>
[Trait("Lane", "integration")]
public sealed class EngineBoundaryRulesTests
{
    private static (string, IReadOnlyList<string>) Project(string name, params string[] packages) => (name, packages);

    [Fact]
    public void The_adapter_project_alone_may_reference_the_sdk() =>
        Verify.None(EngineBoundaryRules.CheckPackages([Project("Aw.Sindri.Workflows", "Temporalio", "Microsoft.Extensions.DependencyInjection.Abstractions"), Project("Aw.Sindri.Host", "Microsoft.AspNetCore.App")]));

    [Theory]
    [InlineData("Aw.Sindri.Host")]
    [InlineData("Aw.Sindri.Ui")]
    [InlineData("Aw.Sindri.Infrastructure")]
    [InlineData("Aw.Sindri.Features")]
    public void Any_other_project_referencing_the_sdk_is_reported(string project) =>
        Verify.Exactly(EngineBoundaryRules.CheckPackages([Project(project, "Temporalio"), Project("Aw.Sindri.Workflows", "Temporalio")]), EngineBoundaryRules.Id, project);

    [Fact]
    public void Types_of_the_adapter_namespace_may_use_the_sdk_and_nothing_else_may() =>
        Verify.Exactly(EngineBoundaryRules.CheckTypes(
        [
            ("Aw.Sindri.Infrastructure.Workflows.TemporalEngineReader", "Temporalio.Client.ITemporalClient"),
            ("Aw.Sindri.Host.Commands.StatusVerb", "Temporalio.Client.ITemporalClient"),
            ("Aw.Sindri.Ui.Api.EngineClient", "Temporalio.Client.TemporalClient"),
            ("Aw.Sindri.Host.Commands.StatusVerb", "System.String"),
        ]), EngineBoundaryRules.Id, "Aw.Sindri.Host.Commands.StatusVerb", "Aw.Sindri.Ui.Api.EngineClient");

    [Fact]
    public void A_namespace_that_only_starts_like_the_adapter_is_not_the_adapter() =>
        Verify.Exactly(EngineBoundaryRules.CheckTypes([("Aw.Sindri.Infrastructure.WorkflowsExtra.Thing", "Temporalio.Client.ITemporalClient")]), EngineBoundaryRules.Id, "WorkflowsExtra");

    [Fact]
    public void In_the_solution_only_the_adapter_project_references_the_sdk() =>
        Verify.None(EngineBoundaryRules.CheckPackages(Solution.ProductionProjects.Select(name =>
        {
            var path = Solution.ProjectPaths.First(p => Solution.NameOf(p) == name);
            var packages = XDocument.Load(Path.Combine(Solution.RepoRoot, path)).Descendants("PackageReference")
                .Select(static e => (string?)e.Attribute("Include") ?? string.Empty).ToList();
            return (name, (IReadOnlyList<string>)packages);
        })));
}
