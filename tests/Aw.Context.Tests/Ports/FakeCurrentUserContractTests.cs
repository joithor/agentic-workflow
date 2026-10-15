using Aw.Context.Core.Engine;
using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.Ports;

[Trait("Lane", "unit")]
public sealed class FakeCurrentUserContractTests : CurrentUserContract
{
    protected override ICurrentUser CreateUser() => new FakeCurrentUser();
}
