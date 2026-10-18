namespace Aw.Context.Core.Engine;

/// <summary>The kinds of step the cascade may take (spec §6.1; the component hop is SC App. C, C4). The default is the first three.</summary>
[Flags]
public enum CascadeHops
{
    /// <summary>No hop: own scope only.</summary>
    None = 0,

    /// <summary>A write of a thing another node reads.</summary>
    State = 1,

    /// <summary>The next step of a shared flow.</summary>
    Flow = 2,

    /// <summary>A reverse depends-on.</summary>
    Dependency = 4,

    /// <summary>Opt-in: a confirmed code unit that implements the node also implements another.</summary>
    Component = 8,

    /// <summary>State, flow and dependency: what a call without "hops" walks.</summary>
    Default = State | Flow | Dependency,
}
