using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// What a recipe can do, named once (setup-once spec §5.3): a block descriptor id ('step.produce'), an effect kind ('effect.notify.desk') or the literal value of a
/// capability field. The recipe's capability set is a set of these; the engine decides the names, so the shape is only: not empty, at most 128 characters, no control characters.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<CapabilityId>))]
public readonly partial struct CapabilityId : ITypedId<CapabilityId, string>
{
    private static Vogen.Validation Validate(string value) =>
        value.Length is > 0 and <= 128 && !value.Any(char.IsControl)
            ? Vogen.Validation.Ok
            : Vogen.Validation.Invalid("A capability id is 1 to 128 characters with no control characters");

    private static string NormalizeInput(string input) => input.Length > 128 ? string.Empty : input;
}
