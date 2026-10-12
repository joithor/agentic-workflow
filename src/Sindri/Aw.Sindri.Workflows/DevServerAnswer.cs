namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>How a dev server's frontend answered a probe.</summary>
internal enum DevServerAnswer
{
    /// <summary>Nothing answered (refused, timed out, or the RPC failed).</summary>
    Unreachable,

    /// <summary>It answered, but has no 'sindri' namespace.</summary>
    NoNamespace,

    /// <summary>It answered and the 'sindri' namespace exists.</summary>
    Ready,
}
