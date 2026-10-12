using Aw.Context.Core.Map;

namespace Aw.Context.Features.ShowMapStatus;

/// <summary>The current version and its counts.</summary>
/// <param name="Reader">The caller and settings.</param>
internal sealed record ShowMapStatusRequest(MapReader Reader);
