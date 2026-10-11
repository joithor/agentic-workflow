using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Infrastructure.Control;

/// <summary>The endpoint record as &lt;home&gt;/control.json (0600), written atomically by the control plane once it listens.</summary>
internal sealed class FileEndpoint(SindriSettings settings, JsonSerializerOptions options) : IEndpointFile
{
    public async ValueTask<Result<ControlEndpoint?>> Read(CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(settings.EndpointPath, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return Results.Success<ControlEndpoint?>(value: null);
        }
        catch (DirectoryNotFoundException)
        {
            return Results.Success<ControlEndpoint?>(value: null);
        }

        var endpoint = AwJson.Deserialize<ControlEndpoint>(bytes, options);
        return endpoint.IsError ? Results.Success<ControlEndpoint?>(value: null) : endpoint.Value;   // a damaged record reads as none
    }

    public ValueTask<Result<Unit>> Write(ControlEndpoint endpoint, CancellationToken cancellationToken)
    {
        OwnerOnly.EnsureDirectory(settings.Home);
        OwnerOnly.WriteAtomically(settings.EndpointPath, AwJson.Serialize(endpoint, options));
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    public async ValueTask<Result<Unit>> Remove(int pid, CancellationToken cancellationToken)
    {
        var current = await Read(cancellationToken).ConfigureAwait(false);
        if (current.IsSuccess && current.Value?.Pid == pid)
        {
            File.Delete(settings.EndpointPath);
        }

        return Unit.Value;
    }
}
