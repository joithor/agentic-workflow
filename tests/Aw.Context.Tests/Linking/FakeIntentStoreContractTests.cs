using Aw.Context.Core.Linking;
using Aw.Context.Testing.Linking;

namespace Aw.Context.Tests.Linking;

[Trait("Lane", "unit")]
public sealed class FakeIntentStoreContractTests : IntentStoreContract
{
    protected override IIntentStore CreateStore() => new FakeIntentStore();
}
