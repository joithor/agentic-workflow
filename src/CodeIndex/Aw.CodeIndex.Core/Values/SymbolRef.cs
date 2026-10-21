using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.CodeIndex.Core.Values;

/// <summary>A symbol's stable reference (spec §3): '&lt;repo&gt;:&lt;path&gt;#&lt;qualified name&gt;[/&lt;arity&gt;][@&lt;h&gt;]'. Consumers must not store one across commits. It is parsed into parts, never concatenated into SQL or a path.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<SymbolRef>))]
public readonly partial struct SymbolRef : ITypedId<SymbolRef, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("SymbolRef must be '<repo>:<path>#<qualified name>', without control characters");

    [GeneratedRegex("^[A-Za-z0-9_-][A-Za-z0-9._-]{0,63}:[^\\p{C}]{1,1900}#[^\\p{C}]{1,512}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
