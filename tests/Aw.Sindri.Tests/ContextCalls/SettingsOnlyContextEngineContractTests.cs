using Aw.Context.Testing.Engine;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

[Trait("Lane", "unit")]
public sealed class SettingsOnlyContextEngineContractTests : ContextEngineContract<SettingsOnlyContextEngine>
{
    protected override SettingsOnlyContextEngine CreateDown() => SettingsOnlyContextEngine.Instance;
}
