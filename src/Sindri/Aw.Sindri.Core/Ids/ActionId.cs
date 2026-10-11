using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>
/// A command catalog row's id (API spec §3.3): the command's name ('StopProcess'), or the command and a variant
/// ('CloseItem:cancelled', 'ApplyCommand:answer-grant'). The UI keys its controls by it.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ActionId>))]
public readonly partial struct ActionId : ITypedId<ActionId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("An action id is a PascalCase command name, optionally ':' and a kebab-case variant");

    [GeneratedRegex("^[A-Z][A-Za-z]{2,40}(:[a-z][a-z0-9-]{1,40})?\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
