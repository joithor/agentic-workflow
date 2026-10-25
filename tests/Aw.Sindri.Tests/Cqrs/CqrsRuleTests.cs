using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Catalog;

namespace Aw.Sindri.Tests.Cqrs;

/// <summary>ARCH-CQRS-002 and ARCH-CQRS-003 over the Features assembly, each proven red by a fixture.</summary>
public sealed class CqrsRuleTests
{
    private static Type[] Features => typeof(StopProcessRequest).Assembly.GetTypes();

    [Fact, Trait("Lane", "unit")]
    public void Query_handlers_take_no_write_ports()
    {
        var violations = QueryPortRules.Check(Features);

        Assert.True(violations.Count == 0, CatalogRules.Lines(violations));
    }

    [Fact, Trait("Lane", "unit")]
    public void Query_port_rule_flags_a_query_handler_that_takes_the_ledger()
    {
        var violation = Assert.Single(QueryPortRules.Check([typeof(LedgerReadingQueryService), typeof(LedgerWritingCommandService)]));

        Assert.StartsWith("ARCH-CQRS-002: LedgerReadingQueryService", violation, StringComparison.Ordinal);
        Assert.Contains("ILedgerStore", violation, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Query_port_rule_follows_a_handler_s_own_helper()
    {
        var violation = Assert.Single(QueryPortRules.Check([typeof(HelperQueryService)]));

        Assert.Contains("HelperQueryService handles a query but takes ILedgerStore", violation, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_query_port_allowlist_is_empty_and_gone()
    {
        Assert.Null(typeof(QueryPortRules).GetField("Allowlist"));   // P1's G1 allowlist went with C9's read ports
        Assert.Empty(QueryPortRules.Offenders(Features));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_command_and_variant_has_a_catalog_row()
    {
        var violations = CatalogCoverageRules.Check(Features, new CommandCatalog());

        Assert.True(violations.Count == 0, CatalogRules.Lines(violations));
    }

    [Fact, Trait("Lane", "unit")]
    public void Catalog_coverage_rule_flags_a_command_without_a_row_and_a_classified_command_without_variants()
    {
        var plainOnly = new InMemoryCommandCatalog(Row("FixtureApply", typeof(FixtureApply), variant: null));
        var variants = new InMemoryCommandCatalog(Row("FixtureApply:answer-text", typeof(FixtureApply), "answer-text"));

        Assert.Equal(
            ["ARCH-CQRS-003: FixtureApply has no command catalog row; add one in Host/Catalog", "ARCH-CQRS-003: UnlistedCommand has no command catalog row; add one in Host/Catalog"],
            CatalogCoverageRules.Check([typeof(FixtureApply), typeof(UnlistedCommand)], new InMemoryCommandCatalog()));
        Assert.Contains("needs variant rows", Assert.Single(CatalogCoverageRules.Check([typeof(FixtureApply)], plainOnly)), StringComparison.Ordinal);
        Assert.Empty(CatalogCoverageRules.Check([typeof(FixtureApply)], variants));
    }

    [Fact, Trait("Lane", "unit")]
    public void Catalog_coverage_rule_flags_an_IJob_whose_row_is_not_class_job()
    {
        var desk = new InMemoryCommandCatalog(Row("FixtureJob", typeof(FixtureJob), variant: null));
        var job = new InMemoryCommandCatalog(Row("FixtureJob", typeof(FixtureJob), variant: null) with { Class = ActionClass.Job });

        Assert.Equal(["ARCH-CQRS-003: FixtureJob is an IJob, so its row's class is 'job'"], CatalogCoverageRules.Check([typeof(FixtureJob)], desk));
        Assert.Empty(CatalogCoverageRules.Check([typeof(FixtureJob)], job));
    }

    private static CommandRow Row(string action, Type type, string? variant) =>
        new(ActionId.From(action), type, variant, ActionClass.Desk, ServiceGrants: [], NeedsTerminal: false, SelfAudited: false, Destructive: false,
            ReadsViews: false, Shortcut: null, "POST", "/sindri/fixture", CliForm.Parse("sindri fixture").Value, "ready");

    private sealed record UnlistedCommand : ICommand<CommandAccepted>;

    private sealed record FixtureJob : IJob<CommandAccepted>;

    private sealed record LedgerQuery : IQuery<long>;

    private sealed class LedgerReadingQueryService(ILedgerStore ledger) : IService<LedgerQuery, long>
    {
        public ValueTask<Result<long>> Handle(LedgerQuery request, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<long>>(ledger.GetHashCode());
    }

    private sealed class LedgerHelper(ILedgerStore ledger)
    {
        public override string ToString() => ledger.ToString() ?? string.Empty;
    }

    private sealed class HelperQueryService(LedgerHelper helper) : IService<LedgerQuery, long>
    {
        public ValueTask<Result<long>> Handle(LedgerQuery request, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<long>>(helper.GetHashCode());
    }

    private sealed class LedgerWritingCommandService(ILedgerStore ledger) : IService<UnlistedCommand, CommandAccepted>
    {
        public ValueTask<Result<CommandAccepted>> Handle(UnlistedCommand request, CancellationToken cancellationToken) =>
            throw new NotSupportedException(ledger.ToString());
    }
}
