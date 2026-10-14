using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CloseItem;
using Aw.Sindri.Features.CreateItem;

namespace Aw.Sindri.Host.Catalog;

/// <summary>API P3's rows (plan G16): creating an item, and closing one as done or cancelled (API spec §3.2, UI spec E-8).</summary>
internal static class ItemRows
{
    internal static IReadOnlyList<CommandRow> All { get; } =
    [
        new(ActionId.From("CreateItem"), typeof(CreateItemRequest), Variant: null, ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null,
            "POST", "/sindri/items", ShellRows.Form("sindri item add <title> [--repo <repo>]"), "ready"),
        new(ActionId.From("CloseItem:done"), typeof(CloseItemRequest), "done", ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: false, ReadsViews: true, Shortcut: null,
            "POST", "/sindri/items/{itemId}/close", ShellRows.Form("sindri item close <itemId> [--version <n>]"), "ready"),
        new(ActionId.From("CloseItem:cancelled"), typeof(CloseItemRequest), "cancelled", ActionClass.Desk, ServiceGrants: [],
            NeedsTerminal: false, SelfAudited: false, Destructive: true, ReadsViews: true, Shortcut: null,
            "POST", "/sindri/items/{itemId}/close", ShellRows.Form("sindri item close <itemId> --cancelled [--version <n>]"), "ready"),
    ];
}
