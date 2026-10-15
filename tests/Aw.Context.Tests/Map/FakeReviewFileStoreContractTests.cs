using Aw.Context.Core.Review;
using Aw.Context.Testing.Review;

namespace Aw.Context.Tests.Map;

[Trait("Lane", "unit")]
public sealed class FakeReviewFileStoreContractTests : ReviewFileStoreContract
{
    protected override string Folder => FakeReviewFileStore.Folder;

    protected override string Worktree => FakeReviewFileStore.WorktreeRoot;

    protected override IReviewFileStore CreateStore() => new FakeReviewFileStore();
}
