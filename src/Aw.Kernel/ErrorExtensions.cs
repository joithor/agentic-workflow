using System.Collections;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Aw.Kernel;

/// <summary>
/// The extension members an <see cref="Error"/> carries (RFC 9457 §3.2): an immutable, ordered list of camelCase member
/// names and their JSON values. A slice builds each value with its own source-generated type info
/// (JsonSerializer.SerializeToElement with the type info), so nothing here uses reflection and it is AOT-safe.
/// A host that answers HTTP writes them as top-level members of the problem body, after the standard ones; a CLI prints
/// the message only and ignores them.
/// </summary>
/// <remarks>
/// Two extensions are equal when they hold the same names in the same order with deeply equal values, so an <see cref="Error"/> carrying
/// them compares by content. A slice should bound its own lists: the Sindri host writes at most 100 items per array.
/// </remarks>
public sealed class ErrorExtensions : IReadOnlyList<KeyValuePair<string, JsonElement>>, IEquatable<ErrorExtensions>
{
    /// <summary>The longest member name.</summary>
    public const int MaxNameLength = 64;

    private static readonly ReadOnlyCollection<string> _reserved = new(["type", "title", "status", "detail", "instance", "traceId"]);

    private readonly KeyValuePair<string, JsonElement>[] _members;

    private ErrorExtensions(KeyValuePair<string, JsonElement>[] members)
    {
        _members = members;
    }

    /// <summary>No extensions.</summary>
    public static ErrorExtensions Empty { get; } = new([]);

    /// <summary>The names an extension may not use, in any letter case: RFC 9457's own members and the host's trace id.</summary>
    public static IReadOnlyList<string> ReservedNames => _reserved;

    /// <inheritdoc />
    public int Count => _members.Length;

    /// <inheritdoc />
    public KeyValuePair<string, JsonElement> this[int index] => _members[index];

    /// <summary>Whether <paramref name="name"/> is a valid extension name: camelCase (a lower-case letter, then letters and digits), at most <see cref="MaxNameLength"/> characters, and not a reserved name.</summary>
    /// <param name="name">The member name.</param>
    /// <returns>True when an extension may use it.</returns>
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength || name[0] is not (>= 'a' and <= 'z'))
        {
            return false;
        }

        foreach (var c in name)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')))
            {
                return false;
            }
        }

        return !_reserved.Any(r => string.Equals(r, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Returns these extensions with one more member at the end.</summary>
    /// <param name="name">The member name; see <see cref="IsValidName"/>.</param>
    /// <param name="value">The value; a clone is kept, so the caller's document can be disposed.</param>
    /// <returns>The new extensions.</returns>
    /// <exception cref="ArgumentException">The name isn't valid or is already present, or the value is undefined (a bug in the slice, not an expected failure).</exception>
    public ErrorExtensions With(string name, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            throw new ArgumentException($"The extension '{name}' has no value (an undefined JsonElement).", nameof(value));
        }

        if (!IsValidName(name))
        {
            throw new ArgumentException($"'{name}' isn't a valid extension name: camelCase, at most {MaxNameLength} characters, and not one of {string.Join(", ", _reserved)}.", nameof(name));
        }

        if (Array.Exists(_members, m => string.Equals(m.Key, name, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"The extension '{name}' is already set.", nameof(name));
        }

        return new([.. _members, new(name, value.Clone())]);
    }

    /// <inheritdoc />
    public bool Equals(ErrorExtensions? other)
    {
        if (other is null || other._members.Length != _members.Length)
        {
            return false;
        }

        for (var i = 0; i < _members.Length; i++)
        {
            if (!string.Equals(_members[i].Key, other._members[i].Key, StringComparison.Ordinal)
                || !JsonElement.DeepEquals(_members[i].Value, other._members[i].Value))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ErrorExtensions);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_members.Length);
        foreach (var member in _members)
        {
            hash.Add(member.Key, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, JsonElement>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, JsonElement>>)_members).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
