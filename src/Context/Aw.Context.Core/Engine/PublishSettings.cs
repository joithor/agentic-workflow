namespace Aw.Context.Core.Engine;

/// <summary>'context.publish' (retrieval spec §8.7a, code index spec §6.4): how long a refreshed map waits for the code index of its commit before it goes ahead.</summary>
public sealed record PublishSettings
{
    /// <summary>The widest wait the profile may ask for, in minutes.</summary>
    public const int MaxWaitMinutes = 240;

    /// <summary>The default wait, in minutes.</summary>
    public const int DefaultWaitMinutes = 15;

    /// <summary>The minutes a refreshed map waits ('context.publish.waitForIndexMinutes', 0 to 240, default 15); 0 turns the wait off.</summary>
    public int WaitForIndexMinutes { get; init; } = DefaultWaitMinutes;
}
