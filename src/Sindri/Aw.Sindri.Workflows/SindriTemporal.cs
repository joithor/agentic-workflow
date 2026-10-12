namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The fixed names of Sindri's Temporal deployment (engine spec rev 8 §0 "Operations").</summary>
internal static class SindriTemporal
{
    /// <summary>The namespace the dev server creates and the worker and clients use.</summary>
    public const string Namespace = "sindri";

    /// <summary>The task queue the worker inside 'sindri serve' polls.</summary>
    public const string TaskQueue = "sindri";

    /// <summary>The dev server's SQLite file, directly under the Sindri home.</summary>
    public const string DatabaseFile = "temporal.db";
}
