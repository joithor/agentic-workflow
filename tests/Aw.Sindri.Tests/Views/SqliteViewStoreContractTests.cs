using Aw.Kernel.Testing;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Views;

[Lane(TestLane.Integration)]
public sealed class SqliteViewStoreContractTests : ViewStoreContract, IDisposable
{
    private readonly TempHome _home = new();
    private SqliteViews? _views;

    public void Dispose()
    {
        _views?.Dispose();
        _home.Dispose();
    }

    protected override (IViewStore Store, IViewReader Reader) Create()
    {
        _views = new SqliteViews(_home.Path);
        return (_views.Store, _views.Reader);
    }
}
