using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Infrastructure.Guard;

/// <summary>Named values from the process environment Composition passes; blank counts as unset.</summary>
internal sealed class EnvironmentSecrets(Func<string, string?> environment) : ISecretsSource
{
    /// <inheritdoc/>
    public string? Get(string name) => environment(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;
}
