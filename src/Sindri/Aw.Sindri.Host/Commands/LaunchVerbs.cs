using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri launch init [--viewer &lt;name&gt;] [--force] [--stdout]' and 'sindri launch show [&lt;id&gt;] [--json]' (API P10a, U7; launcher spec §4):
/// box-local, so they run without the control plane. Exit 0 ok, 1 refused (a file exists without --force), 2 usage or error.
/// </summary>
/// <param name="setup">The launcher's box-local side.</param>
/// <param name="output">Where the verb writes.</param>
[SuppressMessage("Major Code Smell", "MA0182:Avoid unused internal types", Justification = "Dispatched from the verb table in U7 (API P10a).")]
internal sealed class LaunchVerbs(ILaunchSetup setup, Output output)
{
    internal const string Usage = "launch init [--viewer <name>] [--force] [--stdout] | launch show [<id>] [--json]";

    internal Task<int> Run(string[] args, CancellationToken ct) =>
        throw new NotSupportedException(string.Join(' ', args) + setup.GetType().Name + output.Out.NewLine + ct.CanBeCanceled);
}
