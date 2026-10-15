using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Messages;
using Aw.Meta.Features.Inbox;
using Aw.Meta.Features.InboxDefer;
using Aw.Meta.Features.InboxDismiss;
using Aw.Meta.Features.Lanes;
using Aw.Meta.Features.LogAppend;
using Aw.Meta.Features.Owner;
using Aw.Meta.Features.OwnerAnswer;
using Aw.Meta.Features.OwnerAsk;
using Aw.Meta.Features.OutboxRecord;
using Aw.Meta.Features.Roles;
using Aw.Meta.Features.Status;
using Aw.Meta.Features.StopCheck;
using Aw.Meta.Features.SyncInbox;

namespace Aw.Meta.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(TeamConfig))]
[JsonSerializable(typeof(MetaRole))]
[JsonSerializable(typeof(ShowRoleResponse))]
[JsonSerializable(typeof(ListInboxResponse))]
[JsonSerializable(typeof(DismissInboxResponse))]
[JsonSerializable(typeof(DeferInboxResponse))]
[JsonSerializable(typeof(LanesResponse))]
[JsonSerializable(typeof(OutboxRecordResponse))]
[JsonSerializable(typeof(ListOwnerResponse))]
[JsonSerializable(typeof(AnswerOwnerResponse))]
[JsonSerializable(typeof(AskOwnerResponse))]
[JsonSerializable(typeof(AppendLogResponse))]
[JsonSerializable(typeof(StatusResponse))]
[JsonSerializable(typeof(StopCheckResponse))]
[JsonSerializable(typeof(SyncInboxRequest))]
[JsonSerializable(typeof(SyncInboxResponse))]
internal sealed partial class MetaJsonContext : JsonSerializerContext;
