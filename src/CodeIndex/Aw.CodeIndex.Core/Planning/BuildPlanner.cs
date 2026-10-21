namespace Aw.CodeIndex.Core.Planning;

/// <summary>Decides what a build has to do, as a pure function of data in and a plan out (spec §5.2).</summary>
public static class BuildPlanner
{
    /// <summary>Plans a build.</summary>
    /// <param name="input">The snapshot, the live index's rows and the wanted stamps.</param>
    /// <returns>The files to parse and drop, and which layers are current.</returns>
    public static BuildPlan Plan(BuildPlanInput input) => throw new NotSupportedException();
}
