using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>
/// An instance's opaque id (spec §5.1, §6.3): 'WFI-' and 32 lower-case hex digits. The CLI's '/'-separated
/// addresses are <see cref="InstanceAddress"/>; only this id crosses the API.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<WorkflowInstanceId>))]
public readonly partial struct WorkflowInstanceId : ITypedId<WorkflowInstanceId, string>
{
    /// <summary>The id for <paramref name="value"/>, written as 'WFI-' and its 32 hex digits.</summary>
    /// <param name="value">A fresh id from the id generator, or one derived from a trigger key.</param>
    /// <returns>The id.</returns>
    public static WorkflowInstanceId For(Guid value) => From("WFI-" + value.ToString("N"));

    /// <summary>The id of the instance at <paramref name="address"/>.</summary>
    /// <param name="address">The instance's address.</param>
    /// <returns>Its id.</returns>
    public static WorkflowInstanceId Of(InstanceAddress address) => throw new NotSupportedException("red: minted from the address");

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("An instance id is 'WFI-' and 32 lower-case hex digits");

    [GeneratedRegex("^WFI-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
