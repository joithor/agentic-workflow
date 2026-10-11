using Aw.Evidence.Core.Guard;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeSecretsContractTests : SecretsSourceContract
{
    protected override ISecretsSource Create(IReadOnlyDictionary<string, string> values) => new FakeSecrets(values);
}
