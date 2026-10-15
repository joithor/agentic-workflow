using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Aw.Kernel;

/// <summary>
/// An expected failure: a stable catalogue code ('AW-…', and 'SND-…' once sindri is ported), its
/// kind, and a message for people. Callers branch on <see cref="Kind"/> or <see cref="Code"/>, never on the message.
/// </summary>
/// <remarks>
/// Make errors with <see cref="ErrorDefinition.With"/> from a package's catalogue, so every code is one
/// <see cref="ErrorCatalogue.Validate"/> has seen. The constructor stays public because JSON reading and the
/// catalogue tests construct errors directly; the in-repo analyzer (spec §4b.6, Aw.Analyzers) is where a ban on
/// 'new Error(...)' outside catalogues belongs.
/// </remarks>
/// <param name="Code">The catalogue code, for example 'AW-KERNEL-002'.</param>
/// <param name="Kind">What kind of failure this is.</param>
/// <param name="Message">What happened, for people.</param>
public sealed record Error(string Code, ErrorKind Kind, string Message)
{
    /// <summary>
    /// Extension members (RFC 9457 §3.2) for a host that answers HTTP, or null for none. Never serialized with the record
    /// (so every existing JSON shape stays as it is); attach them with <see cref="WithExtension(string, JsonElement)"/>.
    /// </summary>
    [JsonIgnore]
    public ErrorExtensions? Extensions { get; init; }

    /// <summary>Returns a copy of this error with one more extension member.</summary>
    /// <param name="name">The camelCase member name; see <see cref="ErrorExtensions.IsValidName"/>.</param>
    /// <param name="value">The value, built with the slice's own source-generated type info.</param>
    /// <returns>The error, extended.</returns>
    /// <exception cref="ArgumentException">The name isn't valid or is already set.</exception>
    public Error WithExtension(string name, JsonElement value) => this with { Extensions = (Extensions ?? ErrorExtensions.Empty).With(name, value) };

    /// <summary>Returns a copy of this error with one more extension member, serialized through <paramref name="typeInfo"/> (no reflection).</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="name">The camelCase member name; see <see cref="ErrorExtensions.IsValidName"/>.</param>
    /// <param name="value">The value.</param>
    /// <param name="typeInfo">The slice's source-generated type info for <typeparamref name="T"/>.</param>
    /// <returns>The error, extended.</returns>
    /// <exception cref="ArgumentException">The name isn't valid or is already set.</exception>
    public Error WithExtension<T>(string name, T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return WithExtension(name, JsonSerializer.SerializeToElement(value, typeInfo));
    }
}
