using Avalonia.Headless;
using Aw.Sindri.Ui.Rendering.Tests.Views;

[assembly: AssemblyFixture(typeof(HeadlessSessionFixture))]

namespace Aw.Sindri.Ui.Rendering.Tests.Views;

/// <summary>
/// Starts the assembly's one headless session before any test runs. Avalonia binds its dispatcher and compositor to the first
/// thread that touches it, so a session started lazily by whichever test class gets there first can lose the race to another
/// class touching Avalonia on its own thread ("The calling thread cannot access this object because a different thread owns
/// it", in DefaultRenderLoop.Add or the Compositor constructor). With the session up first, every later Avalonia touch goes
/// through its Dispatch, and one made anywhere else fails in that test alone.
/// </summary>
public sealed class HeadlessSessionFixture
{
    public HeadlessSessionFixture()
    {
        _ = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessSessionFixture).Assembly);
    }
}
