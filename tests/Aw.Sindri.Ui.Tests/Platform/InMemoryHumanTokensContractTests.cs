using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Platform;

[Lane(TestLane.Unit)]
public sealed class InMemoryHumanTokensContractTests : HumanTokenSourceContract
{
    protected override IHumanTokenSource With(string? text) => new InMemoryHumanTokens { Text = text };
}
