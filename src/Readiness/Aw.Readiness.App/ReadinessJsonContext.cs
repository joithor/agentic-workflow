using System.Text.Json.Serialization;
using Aw.Kernel;
using Aw.Readiness.App.Features.CheckRepo;
using Aw.Readiness.Core;

namespace Aw.Readiness.App;

/// <summary>The package's one source-generated JSON context (spec §4b.3): the --json report, the run history and errors.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(CheckRepoResponse))]
[JsonSerializable(typeof(RunRecord))]
internal sealed partial class ReadinessJsonContext : JsonSerializerContext;
