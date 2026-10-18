using Aw.Sindri.Host.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>The short form without a server: its usage line, and 'sindri help' listing it.</summary>
public sealed class HandbackShortFormUsageTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Handback_without_an_item_prints_the_short_usage_and_exits_2()
    {
        var (exit, stderr) = await Short.Run(new ServiceCollection().BuildServiceProvider(), signal: null, "handback");

        Assert.Equal(2, exit);
        Assert.Contains("usage: sindri handback <item-or-address>", stderr, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Help_lists_the_short_form() =>
        Assert.Contains(Usage.Text.Split('\n'), static l => l.Trim().StartsWith("sindri handback <item-or-address>", StringComparison.Ordinal));
}
