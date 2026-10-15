using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Meta.Features.SyncInbox;

namespace Aw.Meta.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(SyncInboxRequest))]
[JsonSerializable(typeof(SyncInboxResponse))]
internal sealed partial class MetaJsonContext : JsonSerializerContext;
