using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// What satisfied a wait (spec §3.4, §5.4, §5.7): an event, a resource grant, or a child's end. An answer wait is
/// satisfied by an <see cref="AnswerGiven"/> with text, which the decision reads as 'wait.satisfied' with outcome
/// 'answered'.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "source")]
[JsonDerivedType(typeof(EventWaitSource), "event")]
[JsonDerivedType(typeof(ResourceWaitSource), "resource")]
[JsonDerivedType(typeof(ChildWaitSource), "child")]
public abstract record WaitSource
{
    /// <summary>The source's id in the trigger key.</summary>
    [JsonIgnore]
    public abstract string KeyPart { get; }
}
