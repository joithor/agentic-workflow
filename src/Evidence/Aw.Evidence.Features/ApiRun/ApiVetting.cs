using System.Text;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Settings;
using Aw.Kernel;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>The guards that run before any lock: the host, the header variables and the golden files.</summary>
internal sealed class ApiVetting(IEvidenceSettings settings, ISecretsSource secrets, ApiRunFiles files, IEvidenceTargets targets, IExecutableLocator locator)
{
    internal (IEvidenceTargets, IExecutableLocator) CliCollaborators { get; } = (targets, locator);

    internal async ValueTask<Result<VettedRun>> VetAsync(ApiRunRequest request, ApiScript script, CancellationToken cancellationToken)
    {
        var host = ResolveHost(request, script);
        if (host.IsError)
        {
            return host.Error;
        }

        var resolved = new List<ResolvedRequest>();
        foreach (var step in script.RequestList)
        {
            var headers = ReadHeaders(step);
            if (headers.IsError)
            {
                return headers.Error;
            }

            var golden = await ReadGoldenAsync(request.ScriptPath, step.Expect, cancellationToken).ConfigureAwait(false);
            if (golden.IsError)
            {
                return golden.Error;
            }

            resolved.Add(new ResolvedRequest(step, headers.Value, golden.Value));
        }

        return new VettedRun(script, host.Value, resolved);
    }

    internal async ValueTask<Result<string?>> ReadGoldenAsync(string scriptPath, Expectation expect, CancellationToken cancellationToken)
    {
        if (expect.Golden is not { } golden)
        {
            return (string?)null;
        }

        var path = golden.StartsWith('/') ? golden : RunPath.Combine(scriptPath[..scriptPath.LastIndexOf('/')], golden);
        var bytes = await files.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        return bytes.Map(static b => (string?)Encoding.UTF8.GetString(b));
    }

    private Result<ResolvedHost?> ResolveHost(ApiRunRequest request, ApiScript script)
    {
        if (script.RequestList.Count == 0)
        {
            return (ResolvedHost?)null;
        }

        if (request.Host is null)
        {
            return EvidenceErrors.HostRefused.With("--host is required when the script has requests (there is no default host)");
        }

        return HostGuard.Resolve(request.Host, request.AllowPreviewHost, settings.PreviewHostPattern).Map(static h => (ResolvedHost?)h);
    }

    private Result<IReadOnlyDictionary<string, string>> ReadHeaders(RequestStep step)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, source) in step.Headers ?? new Dictionary<string, HeaderSource>(StringComparer.Ordinal))
        {
            values[name] = secrets.Get(source.Env) ?? string.Empty;
            if (values[name].Length == 0)
            {
                return EvidenceErrors.CredentialsMissing.With($"environment variable {source.Env} (header {name}) is not set");
            }
        }

        return values;
    }
}
