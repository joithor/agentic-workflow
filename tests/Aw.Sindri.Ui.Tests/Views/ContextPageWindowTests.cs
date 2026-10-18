using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The Context page in the shell window (U9a0): the real page replaces the placeholder, with its anchor box named and its seam hosts.</summary>
[Lane(TestLane.Integration)]
public sealed class ContextPageWindowTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Context_page_replaces_the_placeholder_in_the_window()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ContextPageWindowTests).Assembly).Dispatch(() =>
        {
            var shell = new ShellViewModel(new InMemoryEndpointRecords(), new FakeSindriApi(), new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
            var window = new ShellWindow { DataContext = shell };
            try
            {
                window.Show();
                shell.GoTo(PageId.Context);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                var page = Assert.Single(window.GetVisualDescendants().OfType<Control>(), c => c.Name == "ContextPage");
                Assert.Equal("ContextPage", page.GetType().Name);
                Assert.Same(shell.Context, page.DataContext);
                var anchor = page.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "AnchorBox");
                Assert.Equal(Labels.ContextAnchor, AutomationProperties.GetName(anchor));
                string[] hosts = ["LensStackHost", "FileMapHost", "ReviewHost", "NodeHost", "SourcesHost"];
                Assert.All(hosts, h => Assert.Contains(page.GetVisualDescendants().OfType<Control>(), c => c.Name == h));
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, Token);
    }
}
