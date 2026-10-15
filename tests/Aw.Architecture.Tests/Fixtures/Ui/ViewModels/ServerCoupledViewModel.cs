using Aw.Architecture.Tests.Fixtures.UiServer.Core;

namespace Aw.Architecture.Tests.Fixtures.Ui.ViewModels;

// ARCH-UI-001 red: a view model using the server package's type instead of its HTTP contract.
internal sealed class ServerCoupledViewModel
{
    public ServerStatus? Status { get; set; }
}
