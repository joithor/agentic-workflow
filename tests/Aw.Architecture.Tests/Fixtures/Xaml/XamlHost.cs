namespace Aw.Architecture.Tests.Fixtures.Xaml;

// ARCH-DI-001's XAML exemption: a nested XamlClosure_N type (as Avalonia's XAML compiler emits) may use IServiceProvider.
internal sealed class XamlHost
{
    internal sealed class XamlClosure_1
    {
        public object? Resolve(IServiceProvider provider) => provider.GetService(typeof(string));
    }
}
