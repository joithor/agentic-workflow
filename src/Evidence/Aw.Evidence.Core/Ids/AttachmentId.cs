using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Evidence.Core.Ids;

/// <summary>AttachmentId: the id of a Linear attachment.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<AttachmentId>))]
public readonly partial struct AttachmentId : ITypedId<AttachmentId, string>
{
    private static Validation Validate(string value) =>
        string.IsNullOrWhiteSpace(value) ? Validation.Invalid("AttachmentId must not be blank") : Validation.Ok;
}
