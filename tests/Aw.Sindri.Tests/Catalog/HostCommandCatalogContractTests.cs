using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Catalog;

/// <summary>The Host's command catalog keeps the same contract as the in-memory one.</summary>
[Lane(TestLane.Unit)]
public sealed class HostCommandCatalogContractTests : CommandCatalogContract
{
    protected override ICommandCatalog Create() => new CommandCatalog();
}
