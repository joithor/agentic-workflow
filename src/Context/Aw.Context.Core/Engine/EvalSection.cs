using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>'context.eval': whether the backtest may ask the model to label surfaces no PR touched. Off by default.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record EvalSection(bool? Adjudicator = null);
