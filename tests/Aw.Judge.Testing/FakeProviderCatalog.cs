using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using WireNames = Aw.Judge.Core.Providers.ProviderNames;

namespace Aw.Judge.Testing;

/// <summary>
/// The IProviderCatalog fake over given provider instances: the agent CLIs among them count as installed, in the order
/// given (config.json's list, when set, picks and orders them); passes ProviderCatalogContract.
/// </summary>
/// <param name="providers">The providers (ScriptedProviders, usually); a rules provider is added when missing.</param>
public sealed class FakeProviderCatalog(params IModelProvider[] providers) : IProviderCatalog
{
    private readonly IReadOnlyList<IModelProvider> _providers =
        providers.Any(p => p.Name == ProviderName.Rules) ? providers : [.. providers, new ScriptedProvider(ProviderName.Rules, [.. ContentClasses.All]).Returns(new ProviderUnavailable("no-rule"))];

    /// <inheritdoc />
    public ProviderLineup Lineup(ProvidersConfig? config, IReadOnlyCollection<ProviderName>? allowed)
    {
        var installed = _providers.Select(p => AgentCliNames.FromProvider(p.Name)).OfType<AgentCliName>().ToList();
        var clis = config?.AgentClis is { } names
            ? [.. names.Select(n => WireNames.TryParse(n, out var p) ? AgentCliNames.FromProvider(p) : null).OfType<AgentCliName>().Where(installed.Contains)]
            : installed;
        var jev = config?.Jev != false && _providers.Any(p => p.Name == ProviderName.Jev);
        var chain = ChainSpec.Build(clis, jev);
        chain = allowed is null ? chain : chain.Restrict(allowed);
        var named = chain.Classes.Values.SelectMany(n => n).ToHashSet();
        return new ProviderLineup([.. _providers.Where(p => named.Contains(p.Name))], chain);
    }
}
