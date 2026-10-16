using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>What assistive tech hears, read from a control's automation peer (a TextBlock's peer ignores a Name, so the names sit on wrappers).</summary>
internal static class Heard
{
    /// <summary>A control's accessible name, as its peer answers it.</summary>
    internal static string Name(Control control) => ControlAutomationPeer.CreatePeerForElement(control).GetName();

    /// <summary>
    /// A wrapper's accessible name, after checking it is what the screen reader reads: a Text control element, with its inner TextBlock left
    /// raw (out of the control view) so the words are not read twice.
    /// </summary>
    internal static string Wrapper(Border wrapper)
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(wrapper);
        Assert.True(peer.IsControlElement(), $"{wrapper.Name} is not a control element");
        Assert.Equal(AutomationControlType.Text, peer.GetAutomationControlType());
        foreach (var inner in wrapper.GetVisualDescendants().OfType<TextBlock>())
        {
            Assert.False(ControlAutomationPeer.CreatePeerForElement(inner).IsControlElement(), $"{wrapper.Name}'s inner text is read twice");
        }

        return peer.GetName();
    }
}
