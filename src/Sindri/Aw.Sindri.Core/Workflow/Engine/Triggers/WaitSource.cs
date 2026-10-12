using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// What satisfied a wait (spec §3.4, §5.4, §5.8): an event, a resource grant, a child's end, or an answer. An answer
/// wait is satisfied by an <see cref="AnswerGiven"/> with text, which the decision reads as 'wait.satisfied' with
/// outcome 'answered' from an <see cref="AnswerWaitSource"/>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "source")]
[JsonDerivedType(typeof(EventWaitSource), "event")]
[JsonDerivedType(typeof(ResourceWaitSource), "resource")]
[JsonDerivedType(typeof(ChildWaitSource), "child")]
[JsonDerivedType(typeof(AnswerWaitSource), "answer")]
public abstract record WaitSource
{
    /// <summary>The source's id in the trigger key.</summary>
    [JsonIgnore]
    public abstract string KeyPart { get; }
}
