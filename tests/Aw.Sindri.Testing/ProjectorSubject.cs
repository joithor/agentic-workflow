using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Testing;

/// <summary>What <see cref="ProjectorStatusContract"/> drives: the status queries read, the control RebuildViews uses, and the projector's publish.</summary>
/// <param name="Status">The status.</param>
/// <param name="Control">The control.</param>
/// <param name="Publish">Publishes the projector's status, as the projector does after each page.</param>
public sealed record ProjectorSubject(IProjectorStatus Status, IProjectorControl Control, Action<ViewsStatus> Publish);
