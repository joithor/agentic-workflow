using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Scoping;

/// <summary>Reads one scoping fact's stored text off an instance's view.</summary>
internal static class ScopingFactText
{
    /// <summary>The fact's text, or null when it is not produced yet.</summary>
    internal static async ValueTask<Result<string?>> Read(InstanceView view, string key, IFactContents contents, CancellationToken cancellationToken)
    {
        if (view.FactShas?.GetValueOrDefault("fact:" + key) is not { } sha)
        {
            return (string?)null;
        }

        var bytes = await contents.Read(new FactValue("fact:sha256:" + sha, sha, HeadSha: null), cancellationToken).ConfigureAwait(false);
        return bytes.IsError ? bytes.Error : Encoding.UTF8.GetString(bytes.Value.Span);
    }
}
