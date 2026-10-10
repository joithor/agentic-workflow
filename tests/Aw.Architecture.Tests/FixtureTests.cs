using Aw.Architecture.Tests.Fixtures;
using Aw.Architecture.Tests.Rules;

namespace Aw.Architecture.Tests;

/// <summary>Each rule fails on a fixture package that breaks it, with the rule id, what it found and the fix.</summary>
[Trait("Lane", "unit")]
public sealed class FixtureTests
{
    private static readonly string[] _compliant = ["Good", "SmallGood"];

    [Fact]
    public void The_compliant_fixtures_pass_every_rule()
    {
        var edges = FixtureSet.Edges(_compliant);
        var types = FixtureSet.Types(_compliant);
        Assert.NotEmpty(edges);
        Verify.None([
            .. DirectionRules.Check(edges), .. SliceRules.Check(edges), .. ServiceProviderRules.Check(edges),
            .. PortRules.Check(edges), .. InfrastructureRules.Check(types, FixtureSet.Roots(_compliant)),
            .. CycleRules.Check(types), .. NamingRules.CheckNamespaces(types),
        ]);
    }

    [Fact]
    public void Core_depending_on_its_infrastructure_is_reported() =>
        Verify.Exactly(DirectionRules.Check(FixtureSet.Edges("Direction")).Where(v => v.RuleId == DirectionRules.CoreId),
            DirectionRules.CoreId, "Direction.Core.Order -> ");

    [Fact]
    public void Features_depending_on_host_is_reported() =>
        Verify.Exactly(DirectionRules.Check(FixtureSet.Edges("Direction")).Where(v => v.RuleId == DirectionRules.FeaturesId),
            DirectionRules.FeaturesId, "Ship.ShipService -> ");

    [Fact]
    public void Infrastructure_depending_on_features_is_reported() =>
        Verify.Exactly(DirectionRules.Check(FixtureSet.Edges("Direction")).Where(v => v.RuleId == DirectionRules.InfrastructureId),
            DirectionRules.InfrastructureId, "Infrastructure.OrderStore -> ");

    [Fact]
    public void Another_package_past_its_core_is_reported() =>
        Verify.Exactly(DirectionRules.Check(FixtureSet.Edges("CrossA", "CrossB")), DirectionRules.CrossPackageId, "CrossB.Infrastructure.LedgerStore");

    [Fact]
    public void A_slice_using_another_slice_is_reported_in_both_shapes() =>
        Verify.Exactly(SliceRules.Check(FixtureSet.Edges("Slice", "SliceSmall")), SliceRules.Id, "slice Start uses slice List", "slice A uses slice B");

    [Fact]
    public void A_public_infrastructure_type_is_reported_but_the_registration_class_is_not() =>
        Verify.Exactly(InfrastructureRules.Check(FixtureSet.Types("InfraBad"), FixtureSet.Roots("InfraBad")), InfrastructureRules.Id, "OrderDto is public");

    [Fact]
    public void A_namespace_cycle_is_reported() =>
        Verify.Exactly(CycleRules.Check(FixtureSet.Types("Cycle")), CycleRules.Id, "Cycle.Core.Left <-> Aw.Architecture.Tests.Fixtures.Cycle.Core.Right");

    [Fact]
    public void A_utils_namespace_is_reported() =>
        Verify.Exactly(NamingRules.CheckNamespaces(FixtureSet.Types("Naming")), NamingRules.Id, "Naming.Core.Utils");

    [Fact]
    public void A_dumping_ground_project_is_reported() =>
        Verify.Exactly(NamingRules.CheckProjects(["Aw.Judge.Core", "Aw.Shared", "Aw.Judge.Helpers.Tests"]), NamingRules.Id, "Aw.Shared", "Aw.Judge.Helpers.Tests");

    [Fact]
    public void A_service_locator_in_features_is_reported_but_a_registration_lambda_is_not() =>
        Verify.Exactly(ServiceProviderRules.Check(FixtureSet.Edges("Locator")), ServiceProviderRules.Id, "Run.RunService uses IServiceProvider");

    [Fact]
    public void Technology_types_in_core_and_features_are_reported() =>
        Verify.Exactly(PortRules.Check(FixtureSet.Edges("Port")).Where(v => v.RuleId == PortRules.TechnologyId),
            PortRules.TechnologyId, "Port.Core.StoreOptions -> Aw.Kernel.Data.SqliteOptions", "Fetch.FetchService -> System.Net.Http.HttpClient");

    [Fact]
    public void Infrastructure_used_outside_composition_is_reported_in_both_shapes() =>
        Verify.Exactly(PortRules.Check(FixtureSet.Edges("Port", "PortSmall")).Where(v => v.RuleId == PortRules.CompositionId),
            PortRules.CompositionId, "Commands.FetchCommand -> ", "Commands.DrainCommand -> ");

    [Theory]
    [InlineData("Npgsql.NpgsqlConnection", true)]
    [InlineData("SQLitePCL.raw", true)]
    [InlineData("StackExchange.Redis.IDatabase", true)]
    [InlineData("Microsoft.Extensions.Caching.Memory.IMemoryCache", true)]
    [InlineData("System.Diagnostics.Process", true)]
    [InlineData("System.IO.File", true)]
    [InlineData("System.Net.Http.HttpClient", true)]
    [InlineData("Aw.Kernel.Data.SqliteUnitOfWork", true)]
    [InlineData("System.IO.TextWriter", false)]
    [InlineData("System.Diagnostics.Stopwatch", false)]
    [InlineData("Aw.Kernel.Result`1", false)]
    [InlineData("System.Netstandard.Thing", false)]
    public void Technology_is_matched_by_namespace_or_type(string fullName, bool expected) =>
        Assert.Equal(expected, Technology.Is(fullName));
}
