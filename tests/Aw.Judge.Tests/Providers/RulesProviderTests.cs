using Aw.Judge.Core.Providers;
using Aw.Judge.Infrastructure.Providers;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class RulesProviderTests
{
    [Fact]
    public async Task It_never_decides_and_is_never_a_failure()
    {
        var provider = new RulesProvider();

        var result = await provider.Decide(Questions.Text, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);

        Assert.Equal(new ProviderUnavailable("no-rule"), result);
        Assert.Equal(ContentClasses.All.Order(), provider.Classes.Order());
    }
}
