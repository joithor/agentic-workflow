using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>The providers this process built and the chain that routes and orders them; the host builds it once.</summary>
/// <param name="Providers">Every available provider.</param>
/// <param name="Chain">The routing.</param>
public sealed record ProviderSet(IReadOnlyList<IModelProvider> Providers, ChainSpec Chain);
