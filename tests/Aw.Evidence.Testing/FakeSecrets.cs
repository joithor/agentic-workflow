using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Testing;

/// <summary>An ISecretsSource over a dictionary; blank values read as unset.</summary>
/// <param name="values">The secrets by name.</param>
public sealed class FakeSecrets(IReadOnlyDictionary<string, string> values) : ISecretsSource
{
    /// <inheritdoc/>
    public string? Get(string name) => values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}
