using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Guard;
using Aw.Kernel;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>Runs the vetted requests in order against the host, follows same-origin redirects, and checks each expectation.</summary>
internal sealed class RequestRunner(IApiTransport transport, ITextScrubber scrubber)
{
    private const int MaxRedirects = 5;

    internal async ValueTask RunAsync(VettedRun run, Collected collected, CancellationToken cancellationToken)
    {
        foreach (var request in run.Requests)
        {
            var step = request.Step;
            var index = collected.Steps.Count + 1;
            var target = $"{step.Method.ToString().ToUpperInvariant()} {step.Path}";
            var names = (step.Headers ?? new Dictionary<string, HeaderSource>(StringComparer.Ordinal)).Keys.ToList();
            if (collected.Error is not null)
            {
                collected.Steps.Add(new ApiStep(index, ApiAction.Request, target, ApiStepStatus.Skipped, Code: null, Output: null, names));
                continue;
            }

            var sent = await SendAsync(request, run.Host!, cancellationToken).ConfigureAwait(false);   // why: a script with requests has a host (vetting)
            if (sent.IsError)
            {
                collected.Error = new CodedMessage(sent.Error.Code, scrubber.Scrub(sent.Error.Message));
                collected.Steps.Add(new ApiStep(index, ApiAction.Request, target, ApiStepStatus.Broken, Code: null, Output: null, names));
                continue;
            }

            collected.Record(new StepInfo(index, target, ApiAction.Request, names), step.Expect, sent.Value.Status, sent.Value.Body, request.GoldenText);
        }
    }

    private async ValueTask<Result<HttpReply>> SendAsync(ResolvedRequest request, ResolvedHost host, CancellationToken cancellationToken)
    {
        var step = request.Step;
        var (url, method) = (host.UrlFor(step.Path), step.Method);
        var body = step.Body?.GetRawText();
        var stopAtRedirect = step.Expect.Status is >= 300 and <= 399;
        for (var hops = 0; ; hops++)
        {
            var reply = await transport.SendAsync(new HttpCall(method, url, request.Headers, body), cancellationToken).ConfigureAwait(false);
            if (reply.IsError || stopAtRedirect || !IsRedirect(reply.Value))
            {
                return reply;
            }

            var next = new Uri(new Uri(url), reply.Value.Location);
            var origin = HostGuard.OriginOf(next.AbsoluteUri);
            if (!string.Equals(origin, host.Origin, StringComparison.OrdinalIgnoreCase))
            {
                return EvidenceErrors.RequestFailed.With($"{step.Method.ToString().ToUpperInvariant()} {step.Path}: redirect to {origin} refused (the run only talks to {host.Origin})");
            }

            if (hops == MaxRedirects)
            {
                return EvidenceErrors.RequestFailed.With(string.Create(CultureInfo.InvariantCulture, $"{step.Method.ToString().ToUpperInvariant()} {step.Path}: more than {MaxRedirects} redirects"));
            }

            url = next.AbsoluteUri;
            if (reply.Value.Status is not (307 or 308))
            {
                method = method is HttpVerb.Get or HttpVerb.Head ? method : HttpVerb.Get;
                body = null;
            }
        }
    }

    private static bool IsRedirect(HttpReply reply) => reply.Status is 301 or 302 or 303 or 307 or 308 && reply.Location is not null;
}
