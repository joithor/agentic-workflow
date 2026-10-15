using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Messages;
using Aw.Meta.Features.Lanes;
using Aw.Meta.Features.OutboxRecord;
using Aw.Meta.Features.Roles;
using Aw.Meta.Features.StopCheck;
using Aw.Meta.Features.SyncInbox;

namespace Aw.Meta.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(TeamConfig))]
[JsonSerializable(typeof(MetaRole))]
[JsonSerializable(typeof(ShowRoleResponse))]
[JsonSerializable(typeof(LanesResponse))]
[JsonSerializable(typeof(OutboxRecordResponse))]
[JsonSerializable(typeof(StopCheckResponse))]
[JsonSerializable(typeof(SyncInboxRequest))]
[JsonSerializable(typeof(SyncInboxResponse))]
internal sealed partial class MetaJsonContext : JsonSerializerContext;
