using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Host;

/// <summary>The headers the API reads and writes (API spec §6, §9): shared by the caller middleware, the CLI client and the tests.</summary>
[SuppressMessage("Design", "MA0182:Unused internal type", Justification = "The P1 contract unit adds the names; units B (middleware) and D (CLI client) use them (P1 plan §2.4).")]
internal static class SindriHeaders
{
    /// <summary>'human', or 'session' (also the default when missing).</summary>
    internal const string Caller = "X-Sindri-Caller";

    /// <summary>'1' from the CLI at an interactive terminal; honoured only with human.</summary>
    internal const string Terminal = "X-Sindri-Terminal";

    /// <summary>'cli', 'ui' or 'prover': names the actor.</summary>
    internal const string Client = "X-Sindri-Client";

    /// <summary>The repo a session caller is scoped to.</summary>
    internal const string Repo = "X-Sindri-Repo";

    /// <summary>The per-start human token.</summary>
    internal const string Token = "X-Sindri-Token";

    /// <summary>The API version every response carries.</summary>
    internal const string ApiVersion = "Sindri-Api-Version";
}
