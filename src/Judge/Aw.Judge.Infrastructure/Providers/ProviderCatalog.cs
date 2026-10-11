using Aw.Judge.Core.Config;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using WireNames = Aw.Judge.Core.Providers.ProviderNames;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>IProviderCatalog over a fixed set of provider instances, the PATH locator and AW_PROVIDER.</summary>
/// <param name="environment">Reads AW_PROVIDER.</param>
/// <param name="locator">Finds the installed agent CLIs.</param>
/// <param name="providers">One instance of every provider (rules, jev, and each agent CLI).</param>
internal sealed class ProviderCatalog(Func<string, string?> environment, IExecutableLocator locator, IReadOnlyList<IModelProvider> providers) : IProviderCatalog
{
    /// <inheritdoc />
    public ProviderLineup Lineup(ProvidersConfig? config, IReadOnlyCollection<ProviderName>? allowed)
        => throw new NotSupportedException($"red: {environment("AW_PROVIDER")}{locator}{providers.Count}{nameof(Configured)}{nameof(AgentCliResolution)}");

    // config.json's names are checked on read (JudgeConfigParser), so an unknown one can't reach here; skip it if it does.
    private static List<AgentCliName>? Configured(IReadOnlyList<string>? names) =>
        names?.Select(name => WireNames.TryParse(name, out var provider) ? AgentCliNames.FromProvider(provider) : null)
            .OfType<AgentCliName>()
            .ToList();
}
