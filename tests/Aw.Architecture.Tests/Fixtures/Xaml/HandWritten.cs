namespace Aw.Architecture.Tests.Fixtures.Xaml;

// ARCH-DI-001 red next to the exemption: hand-written code using IServiceProvider still fails.
internal sealed class HandWritten
{
    public object? Resolve(IServiceProvider provider) => provider.GetService(typeof(string));
}
