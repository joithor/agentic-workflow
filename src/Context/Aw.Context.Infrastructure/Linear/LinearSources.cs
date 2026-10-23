using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// Builds a linear source for the profile's linear settings (registered by AddContextInfrastructure). A source keeps only its resolved key.
/// </summary>
/// <param name="parts">What every linear source shares.</param>
internal sealed class LinearSources(LinearSourceParts parts)
{
    /// <summary>A linear source.</summary>
    /// <param name="settings">The checked 'context.sources.linear' settings.</param>
    /// <returns>The source.</returns>
    public IContextSource Create(LinearSettings settings) => new LinearSource(settings, parts);
}
