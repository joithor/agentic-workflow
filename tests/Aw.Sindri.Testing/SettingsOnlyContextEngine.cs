using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Sindri.Testing;

/// <summary>
/// An <see cref="IContextEngine"/> for Sindri tests that don't query context: it checks settings with the engine's own
/// pure rules (SettingsReader), and answers every query or refresh with SND-CONTEXT-011. The real engine and its fakes
/// are tested in Aw.Context.Tests.
/// </summary>
public sealed class SettingsOnlyContextEngine : IContextEngine
{
    private SettingsOnlyContextEngine()
    {
    }

    /// <summary>The one instance.</summary>
    public static SettingsOnlyContextEngine Instance { get; } = new();

    /// <inheritdoc />
    public Result<ContextSettings> CheckSettings(JsonElement? section) => SettingsReader.Read(section);

    /// <inheritdoc />
    public ValueTask<Result<ContextResult>> Query(ContextQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ContextResult>>(Down());

    /// <inheritdoc />
    public ValueTask<Result<SourcesView>> Sources(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SourcesView>>(Down());

    /// <inheritdoc />
    public ValueTask<Result<RefreshReport>> Refresh(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<RefreshReport>>(Down());

    private static Error Down() => ContextErrors.AllSourcesUnavailable.With("this test engine answers settings checks only");
}
