using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands.Catalog;

[Lane(TestLane.Unit)]
public sealed class InMemoryCommandCatalogContractTests : CommandCatalogContract
{
    protected override ICommandCatalog Create() => new InMemoryCommandCatalog(
        new CommandRow(ActionId.From("StopProcess"), typeof(StopProcessRequest), Variant: null, ActionClass.Desk, ["host"],
            NeedsTerminal: false, SelfAudited: true, Destructive: true, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/stop", CliForm.Parse("sindri stop").Value, "ready"),
        new CommandRow(ActionId.From("RunDoctor"), CommandType: null, Variant: null, ActionClass.Local, [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            Method: null, Route: null, CliForm.Parse("sindri doctor").Value, "cli-only:local"));
}
