using Aw.Context.Core.Engine;
using Aw.Context.Infrastructure.Review;
using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class OsCurrentUserContractTests : CurrentUserContract
{
    protected override ICurrentUser CreateUser() => new OsCurrentUser();
}
