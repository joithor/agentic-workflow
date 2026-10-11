using Aw.Judge.Core.Config;
using Aw.Judge.Core.Providers;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class ProviderCatalogTests
{
    private static readonly AgentCliName[] _all = [AgentCliName.ClaudeCli, AgentCliName.CodexCli, AgentCliName.CursorCli];

    [Fact]
    public void The_aw_provider_host_leads_the_agent_clis()
    {
        var chain = Catalog(_all, hostProvider: "codex").Lineup(config: null, allowed: null).Chain;

        Assert.Equal([ProviderName.Jev, ProviderName.CodexCli, ProviderName.ClaudeCli, ProviderName.CursorCli, ProviderName.Rules], chain.For(ContentClass.Transcript));
    }

    [Fact]
    public void A_config_list_wins_over_the_host()
    {
        var chain = Catalog(_all, hostProvider: "codex").Lineup(new ProvidersConfig(AgentClis: ["cursor-cli", "claude-cli"]), allowed: null).Chain;

        Assert.Equal([ProviderName.CursorCli, ProviderName.ClaudeCli, ProviderName.Rules], chain.For(ContentClass.Image));
    }

    [Fact]
    public void The_lineup_holds_the_real_adapters()
    {
        var lineup = Catalog(_all, hostProvider: null).Lineup(config: null, allowed: null);

        Assert.Equal(
            [typeof(RulesProvider), typeof(JevProvider), typeof(ClaudeCliProvider), typeof(CodexCliProvider), typeof(CursorCliProvider)],
            lineup.Providers.Select(p => p.GetType()));
    }

    internal static ProviderCatalog Catalog(IReadOnlyList<AgentCliName> installed, string? hostProvider)
    {
        var runner = new FakeProcessRunner();
        return new ProviderCatalog(
            name => name == "AW_PROVIDER" ? hostProvider : null,
            new FakeExecutableLocator([.. installed.Select(AgentCliNames.Binary)]),
            [
                new RulesProvider(),
                new JevProvider(new HttpClient(), new FakeSecretSource()),
                new ClaudeCliProvider(runner),
                new CodexCliProvider(runner),
                new CursorCliProvider(runner),
            ]);
    }
}
