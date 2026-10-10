using Xunit.v3;

namespace Aw.Kernel.Testing;

/// <summary>
/// Puts a test, or every test in a class, in a lane: emits the trait 'Lane=&lt;lane&gt;'. Put it on the class or
/// on the method, not both: xUnit applies both, so the test would carry two Lane traits and the rule is exactly one.
/// The planned reflection check (gap — R1b) fails a test with no Lane trait or with more than one.
/// </summary>
/// <param name="lane">The lane.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class LaneAttribute(TestLane lane) : Attribute, ITraitAttribute
{
    /// <summary>The lane.</summary>
    public TestLane Lane { get; } = lane;

    /// <inheritdoc />
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => [new(Lanes.Key, Lanes.NameOf(Lane))];
}
