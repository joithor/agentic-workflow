using Aw.Sindri.Features.OnyxAccount;
using Aw.Sindri.Features.OnyxUp;

namespace Aw.Sindri.Host.Onyx;

/// <summary>`sindri onyx up --json`: the stack stage and, when it ran, the account stage. Holds no secret.</summary>
/// <param name="Up">Steps 1-5.</param>
/// <param name="Account">Steps 6-9; null when the stack stage ended the run.</param>
internal sealed record OnyxUpSummary(OnyxUpResponse Up, OnyxAccountResponse? Account);
