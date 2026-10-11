using Aw.Evidence.Core.Settings;
using Aw.Evidence.Core.Stack;

namespace Aw.Evidence.Testing;

/// <summary>An IEvidenceSettings holding plain values: an unconfigured install by default (no user agent, the default model and seed policy, no app title).</summary>
public sealed class FakeEvidenceSettings : IEvidenceSettings
{
    /// <inheritdoc/>
    public string? UserAgent { get; set; }

    /// <inheritdoc/>
    public string VisualModel { get; set; } = "judge-default";

    /// <inheritdoc/>
    public SeedPolicy SeedPolicy { get; set; } = SeedPolicy.Default;

    /// <inheritdoc/>
    public string AppTitleSource { get; set; } = "FakeEvidenceSettings.AppTitle";

    /// <summary>The configured app title, or null.</summary>
    public string? AppTitle { get; set; }

    /// <inheritdoc/>
    public ValueTask<string?> AppTitleAsync(CancellationToken cancellationToken) => ValueTask.FromResult(AppTitle);
}
