namespace Aw.Meta.Core.Config;

/// <summary>How large a lane's context window may grow before the meta recycles the lane; every field is optional.</summary>
/// <param name="RecycleAt">A lane is due for a recycle at or above this many context tokens (input plus cache read plus cache creation of its last API call).</param>
public sealed record ContextConfig(int RecycleAt = 300000);
