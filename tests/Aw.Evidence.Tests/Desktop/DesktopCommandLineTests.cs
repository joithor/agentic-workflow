using Aw.Evidence.Core.Desktop;

namespace Aw.Evidence.Tests.Desktop;

public sealed class DesktopCommandLineTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_size_fills_every_element_and_the_shots_element_becomes_one_triple_per_screen()
    {
        var command = DesktopCommandLine.Capture(["run", "--size={size}", "{shots}", "--quiet"], "1440x900", [("page:Home", "/r/home-1440x900/app.png"), ("item/ITM-1", "/r/item-1440x900/app.png")]);

        Assert.Equal(
            ["run", "--size=1440x900", "--shot", "page:Home", "/r/home-1440x900/app.png", "--shot", "item/ITM-1", "/r/item-1440x900/app.png", "--quiet"],
            command);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_command_without_screens_has_no_shot_flags()
    {
        Assert.Equal(["go", "900x900"], DesktopCommandLine.Capture(["go", "{size}", "{shots}"], "900x900", []));
    }
}
