using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

// The same contract the fake is held to, with the adapter over a runner scripted from the facts: what a docker CLI on that box prints.
[Lane(TestLane.Unit)]
public sealed class DockerCliRuntimeContractTests : ContainerRuntimeContract
{
    protected override IContainerRuntime Create(ContainerRuntimeFacts facts, IReadOnlyDictionary<string, string[]> localImages)
    {
        var box = new Box(facts, localImages);
        var stderr = facts.DaemonAnswers ? [] : new[] { "Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?" };
        var runner = new FakeToolRunner((_, arguments) => box.Run(arguments), stderr)
        {
            // what `docker image inspect` prints for an image that isn't there
            StandardErrorFor = (_, arguments) => arguments[0] == "image" && facts.DaemonAnswers && !localImages.ContainsKey(arguments[^1]) ? [$"Error response from daemon: No such image: {arguments[^1]}"] : stderr,
        };
        return new DockerCliRuntime(runner, static _ => null);
    }

    // A box with the given facts: answers each docker command as its CLI would, and remembers whether the stack was started and stopped.
    private sealed class Box(ContainerRuntimeFacts facts, IReadOnlyDictionary<string, string[]> localImages)
    {
        private const string NginxRow = """{"Service":"nginx","State":"STATE","Health":"","Image":"nginx@sha256:aaa","Publishers":[{"URL":"127.0.0.1","TargetPort":80,"PublishedPort":3000,"Protocol":"tcp"},{"URL":"","TargetPort":8080,"PublishedPort":0,"Protocol":"tcp"}]}""";
        private const string ApiRow = """{"Service":"api_server","State":"STATE","Health":"healthy","Image":"onyxdotapp/onyx-backend@sha256:bbb","Publishers":[]}""";

        private bool _started;
        private bool _stopped;

        public ToolRun Run(IReadOnlyList<string> arguments)
        {
            if (!facts.CliFound)
            {
                return ToolRun.Missing;
            }

            return arguments[0] == "compose" ? Compose(arguments) : Other(arguments);
        }

        private static ToolRun Exited(string output, int code = 0) => new(ToolOutcome.Exited, code, output);

        private ToolRun Compose(IReadOnlyList<string> arguments)
        {
            if (arguments[1] == "version")
            {
                return facts.ComposeV2 ? Exited(facts.ComposeVersion + "\n") : Exited("", 1);
            }

            if (arguments.Contains("up", StringComparer.Ordinal))
            {
                (_started, _stopped) = (true, false);
            }
            else if (arguments.Contains("stop", StringComparer.Ordinal))
            {
                _stopped = _started;
            }

            if (!facts.DaemonAnswers)
            {
                return Exited("", 1);
            }

            if (arguments.Contains("config", StringComparer.Ordinal))
            {
                return Exited("{\"services\":{\"nginx\":{\"ports\":[{\"mode\":\"ingress\",\"host_ip\":\"127.0.0.1\",\"target\":80,\"published\":\"3000\",\"protocol\":\"tcp\"}]}}}");
            }

            return arguments.Contains("ps", StringComparer.Ordinal) ? Exited(Ps()) : Exited("");
        }

        private string Ps()
        {
            var state = _stopped ? "exited" : "running";
            return _started ? NginxRow.Replace("STATE", state, StringComparison.Ordinal) + "\n" + ApiRow.Replace("STATE", state, StringComparison.Ordinal) + "\n" : "\n";
        }

        private ToolRun Other(IReadOnlyList<string> arguments) => arguments[0] switch
        {
            "version" when arguments.Contains("{{.Server.Os}}/{{.Server.Arch}}", StringComparer.Ordinal) => facts.DaemonAnswers ? Exited("linux/aarch64\n") : Exited("", 1),
            "version" => facts.DaemonAnswers ? Exited(facts.ServerVersion + "\n") : Exited("", 1),
            "context" => facts.Endpoint is null ? Exited("", 1) : Exited(facts.Endpoint + "\n"),
            "image" => facts.DaemonAnswers ? Inspect(arguments[^1]) : Exited("", 1),
            _ => facts.MemoryBytes is null && facts.DataRoot is null
                ? Exited("", 1)
                : Exited(facts.MemoryBytes?.ToString(CultureInfo.InvariantCulture) + "|" + facts.DataRoot + "\n"),
        };

        private ToolRun Inspect(string image)
            => localImages.TryGetValue(image, out var digests) ? Exited("[" + string.Join(',', digests.Select(static d => "\"" + d + "\"")) + "]\n") : Exited("", 1);
    }
}
