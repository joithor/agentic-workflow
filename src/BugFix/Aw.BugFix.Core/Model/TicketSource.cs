using System.Text.Json.Serialization;

namespace Aw.BugFix.Core.Model;

/// <summary>Where the ticket came from.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TicketSource>))]
public enum TicketSource
{
    /// <summary>linear.</summary>
    [JsonStringEnumMemberName("linear")]
    Linear = 1,

    /// <summary>text.</summary>
    [JsonStringEnumMemberName("text")]
    Text = 2,
}
