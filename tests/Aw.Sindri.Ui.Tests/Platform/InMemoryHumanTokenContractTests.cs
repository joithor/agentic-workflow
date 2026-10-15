using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Platform;

[Lane(TestLane.Unit)]
public sealed class InMemoryHumanTokenContractTests : HumanTokenSourceContract
{
    protected override IHumanTokenSource With(string? contents) => new InMemoryHumanToken(contents);

    protected override void Rewrite(IHumanTokenSource source, string? contents) => ((InMemoryHumanToken)source).Write(contents);
}
