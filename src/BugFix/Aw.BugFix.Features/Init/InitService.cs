using System.Text;
using System.Text.Json;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.Init;

internal sealed class InitService(IStateStore store, IRunLedger ledger, IFiles files, TimeProvider clock, JsonSerializerOptions json)
    : IService<InitRequest, InitResponse>
{
    public ValueTask<Result<InitResponse>> Handle(InitRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(files.ReadText(request.TicketFile).Bind(ReadTicket).Bind(ticket => store.Locked(request.StateDirectory, () => Start(request, ticket), create: true)));

    private Result<Ticket> ReadTicket(string text)
    {
        var ticket = AwJson.Deserialize<Ticket>(Encoding.UTF8.GetBytes(text), json);
        if (ticket.IsError)
        {
            return BugFixErrors.Invalid.With($"invalid ticket: {ticket.Error.Message}");
        }

        return string.IsNullOrWhiteSpace(ticket.Value.Brief) || string.IsNullOrWhiteSpace(ticket.Value.Expected)
            ? BugFixErrors.Invalid.With("invalid ticket: brief and expected must not be empty")
            : ticket;
    }

    private Result<InitResponse> Start(InitRequest request, Ticket ticket)
    {
        var existing = store.Load(request.StateDirectory);
        if (existing.IsError && existing.Error.Code != BugFixErrors.StateMissing.Code)
        {
            return existing.Error;
        }

        if (existing.IsSuccess && !existing.Value.Finished)
        {
            var status = existing.Value.Status == BugFixStatus.NeedsHuman ? "needs-human" : "active";
            return BugFixErrors.WrongPhase.With(
                $"an unfinished bug fix already exists at {store.StateFile(request.StateDirectory)} (status {status}); resume it instead");
        }

        ledger.CreateDigestKey(request.StateDirectory);   // first: a bug fix never exists without its key
        store.Save(request.StateDirectory, BugFixState.Start(ticket, clock.GetUtcNow(), request.TicketFile));
        return new InitResponse(store.StateFile(request.StateDirectory));
    }
}
