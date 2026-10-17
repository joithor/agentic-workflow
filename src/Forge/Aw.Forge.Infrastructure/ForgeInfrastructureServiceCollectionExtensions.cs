using Aw.Forge.Core.Credentials;
using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure.Credentials;
using Aw.Forge.Infrastructure.Git;
using Aw.Forge.Infrastructure.GitHub;
using Aw.Forge.Infrastructure.World;
using Aw.Redaction;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Infrastructure;

/// <summary>Registers Aw.Forge.Infrastructure's adapters. One method per adapter: Sindri calls the one its profile's workflow.forge.adapter names.</summary>
public static class ForgeInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the file-backed forge (workflow.forge.adapter: file) that tests and proofs use; its state lives under $AW_STATE_DIR/forge/file.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <param name="hosts">The allowed hosts (workflow.forge.hosts).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddForgeFileInfrastructure(this IServiceCollection services, Func<string, string?> environment, ForgeHosts hosts)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(hosts);
        var settings = SettingsFromEnvironment.Read(environment);
        return services
            .AddSingleton(settings)
            .AddSingleton(_ => new FileForge(hosts, Path.Combine(settings.StateDirectory, "file"), Redactor.Default))
            .AddSingleton<IForge>(provider => provider.GetRequiredService<FileForge>())
            .AddSingleton<IForgeSeed>(provider => provider.GetRequiredService<FileForge>());
    }

    /// <summary>Adds the GitHub adapter (workflow.forge.adapter: github).</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="hosts">The allowed hosts (workflow.forge.hosts), such as github.com.</param>
    /// <param name="credential">workflow.forge.credential; the default is env:AW_FORGE_TOKEN.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddForgeGitHubInfrastructure(this IServiceCollection services, Func<string, string?> environment, ForgeHosts hosts, ForgeCredentialSpec? credential = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(hosts);
        var spec = credential ?? ForgeCredentialSpec.Default;
        return services
            .AddSingleton(SettingsFromEnvironment.Read(environment))
            .AddSingleton<IForge>(provider =>
            {
                var clock = provider.GetService<TimeProvider>() ?? TimeProvider.System;
                ICredentialSource source = spec.Kind == ForgeCredentialKind.Env
                    ? new EnvCredentialSource(spec.Name!, environment)
                    : new CommandCredentialSource(spec.Argv, clock);
                var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                return new GitHubForge(new GitHubApi(http, source, hosts), source, new GitPusher(Redactor.Default), Redactor.Default);
            });
    }
}
