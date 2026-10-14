using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>One command attempt: the id the audit row 'command.handled:&lt;commandId&gt;' and the command's domain event carry.</summary>
[ValueObject<Guid>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(GuidIdJsonConverter<CommandId>))]
public readonly partial struct CommandId : ITypedId<CommandId, Guid>
{
    private static Validation Validate(Guid value) =>
        value == Guid.Empty ? Validation.Invalid("CommandId must not be empty") : Validation.Ok;
}
