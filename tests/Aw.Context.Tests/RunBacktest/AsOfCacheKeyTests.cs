using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;

namespace Aw.Context.Tests.RunBacktest;

public sealed class AsOfCacheKeyTests
{
    private const string Key = "repo-abc-x1-c1";

    [Fact, Trait("Lane", "unit")]
    public void The_shipped_limits_and_a_different_timeout_keep_the_plain_key()
    {
        Assert.Equal(Key, AsOfMapCache.WithLimits(Key, ClusterLimits.Default), StringComparer.Ordinal);
        Assert.Equal(Key, AsOfMapCache.WithLimits(Key, new ClusterLimits { TimeoutMinutes = 5 }), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_different_size_limit_changes_the_key()
    {
        Assert.Equal(Key + "-g2048.8.256", AsOfMapCache.WithLimits(Key, new ClusterLimits { MaxFileMB = 8 }), StringComparer.Ordinal);
        Assert.NotEqual(AsOfMapCache.WithLimits(Key, new ClusterLimits { MaxGraphMB = 0 }), AsOfMapCache.WithLimits(Key, new ClusterLimits { MaxSnapshotMB = 0 }), StringComparer.Ordinal);
    }
}
