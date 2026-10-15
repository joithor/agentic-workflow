using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.OwnerAnswer;

/// <summary>
/// Records the owner's answer (the dated decision row) and adds it to the running log. The relay to the lane that asked is
/// owed from then on, until a message "OWNER ANSWER q&lt;n&gt;: ..." is sent to it.
/// </summary>
/// <param name="owner">Stores the answer.</param>
/// <param name="log">The running log.</param>
/// <param name="clock">Dates the answer.</param>
internal sealed class AnswerOwnerService(IOwnerStore owner, IRunningLog log, TimeProvider clock) : IService<AnswerOwnerRequest, AnswerOwnerResponse>
{
    public async ValueTask<Result<AnswerOwnerResponse>> Handle(AnswerOwnerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.GetUtcNow();
        var answer = request.Answer.Trim();
        var answered = await owner.Answer(request.Question, answer, now, cancellationToken).ConfigureAwait(false);
        if (answered.IsError)
        {
            return answered.Error;
        }

        var line = LogLine.Owner(now, answer, answered.Value.AskedBy);
        var logged = await log.Append(line, cancellationToken).ConfigureAwait(false);
        return logged.IsError
            ? logged.Error
            : new AnswerOwnerResponse(answered.Value, string.Create(CultureInfo.InvariantCulture, $"OWNER ANSWER q{answered.Value.Number}: {answer}"), line);
    }
}
