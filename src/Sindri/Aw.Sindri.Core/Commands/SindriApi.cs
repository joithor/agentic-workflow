namespace Aw.Sindri.Core.Commands;

/// <summary>The API's version (API spec §9): GET /status reports it as 'apiVersion' and every response carries it as 'Sindri-Api-Version'.</summary>
public static class SindriApi
{
    /// <summary>The version this build serves.</summary>
    public const int Version = 1;
}
