using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>
/// One entry of a transition's 'produces' (spec §3.5): the fact it records, and the trigger's fact it copies it from.
/// The bare key 'fact:x' records the trigger's 'fact:x' under its own name; the long form '{ fact, from }' renames a
/// child's output on the way (DT spec §8.4). A bare key is written as a plain string, so its hash doesn't move. The
/// canonical form lives in the type: a source equal to the fact is the bare key, however the entry is built or read.
/// </summary>
/// <param name="Fact">The fact the transition records.</param>
/// <param name="From">The trigger's fact it takes the value from; a source equal to <paramref name="Fact"/> is the same key and reads as null, whether it is set here or by a <see langword="with"/>.</param>
[JsonConverter(typeof(ProducedFactJsonConverter))]
public sealed record ProducedFact(FactKey Fact, FactKey? From)
{
    /// <summary>The trigger's fact it takes the value from; null when it is the same key.</summary>
    public FactKey? From { get => field == Fact ? null : field; init; } = From == Fact ? null : From;

    /// <summary>The trigger's fact this entry reads: <see cref="From"/>, or <see cref="Fact"/> when it isn't renamed. A method, not a property: the record is written by its converter.</summary>
    public FactKey SourceKey() => From ?? Fact;

    /// <summary>Equal on the fact and the normalized source, so a <see langword="with"/> that sets the source to the fact equals the bare key (the generated one compares the backing field).</summary>
    public bool Equals(ProducedFact? other) => other is not null && Fact == other.Fact && From == other.From;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Fact, From);
}
