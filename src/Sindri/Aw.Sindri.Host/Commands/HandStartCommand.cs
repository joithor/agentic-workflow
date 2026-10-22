namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri pr babysit &lt;url&gt; [--item &lt;id&gt;] [--json]' and 'sindri docs sync &lt;url&gt; [--item &lt;id&gt;] [--json]' (PR babysitter spec API16, §4.2 'The hand start').
/// </summary>
internal static class HandStartCommand
{
    internal const string BabysitUsage = "usage: sindri pr babysit <url> [--item <id>] [--json]";
    internal const string SyncUsage = "usage: sindri docs sync <url> [--item <id>] [--json]";

    internal static Task<int> Babysit(string[] args, RecipeDesk desk, IServiceProvider services, Output output, CancellationToken ct) => throw new NotSupportedException();

    internal static Task<int> DocsSync(string[] args, RecipeDesk desk, IServiceProvider services, Output output, CancellationToken ct) => throw new NotSupportedException();
}
