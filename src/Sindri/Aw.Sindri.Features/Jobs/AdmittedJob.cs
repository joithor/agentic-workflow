using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.Jobs;

/// <summary>A start that passed: the definition that runs it and the background service it runs, if any.</summary>
/// <param name="Definition">The registered definition.</param>
/// <param name="Service">The service the job runs, or null.</param>
internal sealed record AdmittedJob(JobDefinition Definition, ServiceName? Service);
