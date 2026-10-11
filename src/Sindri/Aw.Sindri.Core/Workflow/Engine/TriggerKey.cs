using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>
/// The key a trigger is decided under (spec W12, §5.3, §5.4): 'wf:{instance}:{trigger}'. Only decisions (applied, or
/// refused by a gate or evidence) are recorded under it and replayed; a validation refusal records nothing. The
/// trigger part is the trigger's own id: 'cmd:{nonce}:{verb}:{sha256 of its arguments}' for a command,
/// 'run:{token}:{sha256 of the report}' for a Step report, '{entry}:{kind}:{arm}' for a deadline, 'shift:{tick}',
/// 'event:{sequence}', 'child:{child event key}', 'recipe.revoked:{sequence}', 'open:{launch}' and so on.
/// At most 512 characters.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<TriggerKey>))]
public readonly partial struct TriggerKey : ITypedId<TriggerKey, string>
{
    private const int PartStart = 3 + 36 + 1;

    /// <summary>The instance the trigger is for.</summary>
    /// <returns>The instance in 'wf:{instance}:…'.</returns>
    public WorkflowInstanceId Instance() => WorkflowInstanceId.From(Value[3..(PartStart - 1)]);

    /// <summary>The trigger's own id, after 'wf:{instance}:'.</summary>
    public string Part => Value[PartStart..];

    /// <summary>The key of <paramref name="part"/> on <paramref name="instance"/>.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="part">The trigger's own id.</param>
    /// <returns>'wf:{instance}:{part}'.</returns>
    public static TriggerKey For(WorkflowInstanceId instance, string part) => From("wf:" + instance.Value + ":" + part);

    private static Vogen.Validation Validate(string value) =>
        Shape.IsMatch(value) ? Vogen.Validation.Ok : Vogen.Validation.Invalid("A trigger key is 'wf:{instance}:{trigger}', at most 512 characters");

    [GeneratedRegex("^wf:WFI-[0-9a-f]{32}:[!-~]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 512 ? string.Empty : input;
}
