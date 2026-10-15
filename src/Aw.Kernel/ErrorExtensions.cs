using System.Collections;
using System.Text.Json;

namespace Aw.Kernel;

/// <summary>
/// The extension members an <see cref="Error"/> carries (RFC 9457 §3.2): an immutable, ordered list of camelCase member
/// names and their JSON values. A slice builds each value with its own source-generated type info
/// (JsonSerializer.SerializeToElement with the type info), so nothing here uses reflection and it is AOT-safe.
/// A host that answers HTTP writes them as top-level members of the problem body, after the standard ones; a CLI prints
/// the message only and ignores them.
/// </summary>
public sealed class ErrorExtensions : IReadOnlyList<KeyValuePair<string, JsonElement>>
{
    /// <summary>The longest member name.</summary>
    public const int MaxNameLength = 64;

    private static readonly string[] _reserved = ["type", "title", "status", "detail", "instance", "traceId"];

    private readonly KeyValuePair<string, JsonElement>[] _members;

    private ErrorExtensions(KeyValuePair<string, JsonElement>[] members)
    {
        _members = members;
    }

    /// <summary>No extensions.</summary>
    public static ErrorExtensions Empty { get; } = new([]);

    /// <summary>The names an extension may not use: RFC 9457's own members and the host's trace id.</summary>
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

        return !Array.Exists(_reserved, r => string.Equals(r, name, StringComparison.Ordinal));
    }

    /// <summary>Returns these extensions with one more member at the end.</summary>
    /// <param name="name">The member name; see <see cref="IsValidName"/>.</param>
    /// <param name="value">The value; a clone is kept, so the caller's document can be disposed.</param>
    /// <returns>The new extensions.</returns>
    /// <exception cref="ArgumentException">The name isn't valid or is already present (a bug in the slice, not an expected failure).</exception>
    public ErrorExtensions With(string name, JsonElement value)
    => throw new NotSupportedException();

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, JsonElement>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, JsonElement>>)_members).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
