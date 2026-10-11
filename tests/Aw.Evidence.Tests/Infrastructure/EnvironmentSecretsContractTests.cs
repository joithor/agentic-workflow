using Aw.Evidence.Core.Guard;
using Aw.Evidence.Infrastructure.Guard;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Unit)]
public sealed class EnvironmentSecretsContractTests : SecretsSourceContract
{
    protected override ISecretsSource Create(IReadOnlyDictionary<string, string> values) =>
        new EnvironmentSecrets(name => values.TryGetValue(name, out var value) ? value : null);
}
