using System.Text.Json.Serialization;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Contract;

/// <summary>The contract tests' source-generated JSON context.</summary>
[JsonSerializable(typeof(IdsHolder))]
[JsonSerializable(typeof(ContextResult))]
[JsonSerializable(typeof(SourcesView))]
[JsonSerializable(typeof(RefreshReport))]
internal sealed partial class ContractJsonContext : JsonSerializerContext;
