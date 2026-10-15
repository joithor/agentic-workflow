using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;

namespace Aw.Sindri.Ui.Tests.Settings;

/// <summary>
/// UI spec §11.4, §12.3, §14.2: Settings' Reduce motion switch and text size, kept in the preferences; reduced motion is
/// the OS setting or the switch, re-read when the OS setting changes.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SettingsViewModelTests
{
    private readonly InMemoryPreferencesStore _store = new();
    private readonly FakeMotionPreference _os = new();
    private readonly List<AppearanceSettings> _applied = [];
    private readonly Preferences _preferences;

    public SettingsViewModelTests()
    {
        _preferences = new Preferences(_store);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SettingsViewModel Settings() => new(_preferences, _os, _applied.Add);

    private Task Store(UiPreferences preferences) => _preferences.Update(_ => preferences);

    private Task<UiPreferences> Stored() => new Preferences(_store).Load(Token).AsTask();

    [Fact]
    public async Task Load_shows_and_applies_the_stored_switch_and_text_size()
    {
        await Store(UiPreferences.Defaults with { TextScale = 1.2, ReduceMotion = true });
        using var settings = Settings();

        await settings.Load(Token);

        Assert.True(settings.ReduceMotion);
        Assert.True(settings.IsMotionReduced);
        Assert.Equal(1.2, settings.TextScale);
        Assert.Equal("120%", settings.TextScaleText);
        Assert.Equal(new AppearanceSettings(1.2, ReduceMotion: true), Assert.Single(_applied));
    }

    [Fact]
    public async Task With_nothing_stored_motion_follows_the_OS_setting()
    {
        _os.Set(reduce: true);
        using var settings = Settings();

        await settings.Load(Token);

        Assert.False(settings.ReduceMotion);
        Assert.True(settings.SystemReducesMotion);
        Assert.True(settings.IsMotionReduced);
        Assert.Equal(new AppearanceSettings(1.0, ReduceMotion: true), _applied[^1]);
    }

    [Fact]
    public async Task An_OS_change_is_reread_and_applied()
    {
        using var settings = Settings();
        await settings.Load(Token);

        _os.Set(reduce: true);
        Assert.True(settings.IsMotionReduced);
        Assert.Equal(new AppearanceSettings(1.0, ReduceMotion: true), _applied[^1]);

        _os.Set(reduce: false);
        Assert.False(settings.IsMotionReduced);
        Assert.Equal(new AppearanceSettings(1.0, ReduceMotion: false), _applied[^1]);
    }

    [Fact]
    public async Task The_switch_forces_reduced_motion_whatever_the_OS_says_and_is_saved()
    {
        await Store(UiPreferences.Defaults with { LastPage = "Work", SheetWidth = 600 });
        using var settings = Settings();
        await settings.Load(Token);

        settings.ReduceMotion = true;
        await _preferences.Flush();

        Assert.True(settings.IsMotionReduced);
        Assert.Equal(new AppearanceSettings(1.0, ReduceMotion: true), _applied[^1]);
        Assert.Equal(UiPreferences.Defaults with { LastPage = "Work", SheetWidth = 600, ReduceMotion = true }, await Stored());

        _os.Set(reduce: true);
        _os.Set(reduce: false);
        Assert.True(settings.IsMotionReduced);
        Assert.Equal(new AppearanceSettings(1.0, ReduceMotion: true), _applied[^1]);
    }

    [Fact]
    public async Task Turning_the_switch_off_follows_the_OS_again()
    {
        _os.Set(reduce: true);
        await Store(UiPreferences.Defaults with { ReduceMotion = true });
        using var settings = Settings();
        await settings.Load(Token);

        settings.ReduceMotion = false;
        await _preferences.Flush();

        Assert.True(settings.IsMotionReduced);
        Assert.False((await Stored()).ReduceMotion);
    }

    [Fact]
    public async Task A_new_text_size_is_applied_and_saved_without_touching_the_other_preferences()
    {
        var bounds = new WindowBounds(40, 60, 1280, 800);
        await Store(UiPreferences.Defaults with { Window = bounds });
        using var settings = Settings();
        await settings.Load(Token);

        settings.TextScale = 1.1;
        await _preferences.Flush();

        Assert.Equal("110%", settings.TextScaleText);
        Assert.Equal(new AppearanceSettings(1.1, ReduceMotion: false), _applied[^1]);
        Assert.Equal(UiPreferences.Defaults with { Window = bounds, TextScale = 1.1 }, await Stored());
    }

    [Theory]
    [InlineData(0.5, 0.9)]
    [InlineData(2.0, 1.3)]
    public async Task A_text_size_outside_the_range_is_clamped(double asked, double shown)
    {
        using var settings = Settings();
        await settings.Load(Token);

        settings.TextScale = asked;
        await _preferences.Flush();

        Assert.Equal(shown, settings.TextScale);
        Assert.Equal(shown, _applied[^1].TextScale);
        Assert.Equal(shown, (await Stored()).TextScale);
    }

    [Fact]
    public async Task Setting_the_same_value_writes_nothing()
    {
        using var settings = Settings();
        await settings.Load(Token);
        var writes = _store.Writes;

        settings.TextScale = 1.0;
        settings.ReduceMotion = false;
        await _preferences.Flush();

        Assert.Equal(writes, _store.Writes);
    }

    [Fact]
    public async Task An_OS_change_off_the_UI_thread_is_applied_on_it()
    {
        var ui = new QueuedContext();
        SettingsViewModel settings;
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(ui);
        try
        {
            settings = Settings();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        using (settings)
        {
            await settings.Load(Token);
            var before = _applied.Count;

            await Task.Run(() => _os.Set(reduce: true), Token);

            Assert.Equal(before, _applied.Count);
            ui.RunAll();
            Assert.True(settings.IsMotionReduced);
            Assert.Equal(new AppearanceSettings(1.0, ReduceMotion: true), _applied[^1]);
        }
    }

    [Fact]
    public async Task After_dispose_an_OS_change_is_ignored()
    {
        var settings = Settings();
        await settings.Load(Token);
        var before = _applied.Count;

        settings.Dispose();
        _os.Set(reduce: true);

        Assert.Equal(before, _applied.Count);
    }

    // A UI thread stand-in: posted work waits until the test runs it.
    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _posted = new();

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_posted)
            {
                _posted.Enqueue((d, state));
            }
        }

        public void RunAll()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State) next;
                lock (_posted)
                {
                    if (!_posted.TryDequeue(out next))
                    {
                        return;
                    }
                }

                next.Callback(next.State);
            }
        }
    }
}
