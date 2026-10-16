using Aw.Kernel.Services;
using Aw.Sindri.Features.StreamEvents;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The 200 answer of an open event stream: 'text/event-stream', never cached, headers sent at once and the body unbuffered,
/// then <see cref="EventPump"/> writes frames until the client leaves or a stop comes. The stream's slot is freed when it ends.
/// </summary>
/// <param name="pump">The loop.</param>
/// <param name="service">Reads each page.</param>
/// <param name="request">The filters and first cursor.</param>
/// <param name="slot">The stream's slot, disposed when the stream ends.</param>
/// <param name="stopping">Cancelled when the process is asked to stop.</param>
internal sealed class SseResult(EventPump pump, IService<StreamEventsRequest, StreamEventsResponse> service, StreamEventsRequest request, IDisposable slot, CancellationToken stopping) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        using (slot)
        {
            var response = httpContext.Response;
            response.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-store";
            httpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();
            using var either = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted, stopping);
            try
            {
                await response.StartAsync(either.Token).ConfigureAwait(false);
                await response.Body.FlushAsync(either.Token).ConfigureAwait(false);   // the headers reach the client now, not with the first frame
            }
            catch (OperationCanceledException) when (either.IsCancellationRequested)
            {
                return;   // gone before the first byte
            }

            await pump.Run(new SseWriter(response.Body), service, request, either.Token).ConfigureAwait(false);
        }
    }
}
