using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

[Trait("Lane", "unit")]
public sealed class FakeExportStoreContractTests : ExportStoreContract
{
    protected override string Folder => FakeExportStore.Folder;

    protected override string Worktree => FakeExportStore.WorktreeRoot;

    protected override IExportStore CreateStore() => new FakeExportStore(new FakeTimeProvider());
}
