using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Locks;
using Aw.Evidence.Core.Settings;
using Aw.Evidence.Core.Stack;
using Aw.Evidence.Host;
using Aw.Evidence.Host.Commands;
using Aw.Evidence.Testing;
using Aw.Evidence.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Evidence.Tests.Cutover;

/// <summary>
/// The real composition and command table in process, with the browser, locks, stack probe and settings swapped for fakes and
/// Console's writers captured (as CommandExitTests does); a test swaps a service with <see cref="Configure"/>.
/// </summary>
internal sealed class CutoverCli : IDisposable
{
    internal TempDirectory Root { get; } = new();

    internal FakeBrowser Browser { get; } = new();

    internal FakeStackProbe Probe { get; } = new() { Title = "App" };

    internal FakeEvidenceSettings Settings { get; } = new() { AppTitle = "App" };

    internal Action<IServiceCollection> Configure { get; set; } = static _ => { };

    internal async Task<(int Exit, string Out, string Err)> Run(params string[] args)
    {
        var services = Composition.Compose(
            new ServiceCollection(), name => name switch { "AW_STATE_DIR" => Root.Combine("state"), "AW_LOG_LEVEL" => "none", _ => null }, new FakeTimeProvider());
        _ = services.Replace(ServiceDescriptor.Singleton<IBrowser>(Browser));
        _ = services.Replace(ServiceDescriptor.Singleton<IMachineLocks>(new InMemoryMachineLocks()));
        _ = services.Replace(ServiceDescriptor.Singleton<IStackProbe>(Probe));
        _ = services.Replace(ServiceDescriptor.Singleton<IEvidenceSettings>(Settings));
        Configure(services);
        await using var provider = services.BuildServiceProvider();
        var (output, error) = (new StringWriter(), new StringWriter());
        var (stdout, stderr) = (Console.Out, Console.Error);
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var exit = await CommandTable.Run(args, provider);
            return (exit, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
    }

    internal string Write(string relative, string content)
    {
        var path = Root.Combine(relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: a path under the root always has a directory
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose() => Root.Dispose();
}
