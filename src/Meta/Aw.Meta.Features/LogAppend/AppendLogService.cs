using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.LogAppend;

/// <summary>Appends one line to the running log: "- KIND yyyy-MM-dd HH:mm: text".</summary>
/// <param name="log">The running log.</param>
/// <param name="clock">Dates the line.</param>
internal sealed class AppendLogService(IRunningLog log, TimeProvider clock) : IService<AppendLogRequest, AppendLogResponse>
{
    public async ValueTask<Result<AppendLogResponse>> Handle(AppendLogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = LogLine.TryParseKind(request.Kind, out var kind);   // the validator already checked it
        var line = LogLine.Event(kind, clock.GetUtcNow(), request.Text);
        var appended = await log.Append(line, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new AppendLogResponse(line);
    }
}
