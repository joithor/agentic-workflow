using Aw.Meta.Core.Messages;

namespace Aw.Meta.Features.Roles;

/// <summary>The ShowRole response.</summary>
/// <param name="Role">The registered meta session; null when none is registered.</param>
public sealed record ShowRoleResponse(MetaRole? Role);
