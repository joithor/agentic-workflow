# Sindri UI (C#): the app shell

The Sindri app is an [Avalonia UI](https://avaloniaui.net/) desktop app (readiness spec §4a "UI", `.agents/rules/csharp.md`
"The UI package"). Stage S1b ships its shell: one window that shows the control plane's status, the same status
`sindri status` prints, kept live from the event stream, and that says "Not running" with the fix when the control plane
is down. It is its own package, `SindriUi` (tier 4 in `deps.yaml`), and it holds no logic: every verdict comes from
Sindri's handlers (factory addendum §17.27.1). Nothing installs it before the switch.

## Shape

| Project | Holds |
|---|---|
| `src/Sindri/Aw.Sindri.Ui` | The views (`SindriApp`, the shell under `Views/Shell`), the view models, the typed client of Sindri's HTTP API, and the ports for platform services |
| `src/Sindri/Aw.Sindri.Ui.Desktop` | The thin desktop head: `Main`, the composition and one adapter per port. NativeAOT binary `sindri-ui`. No views or view models |

- **No Sindri project reference.** The app reaches Sindri only through its HTTP contract (`docs/csharp/sindri.md`
  "Command layer and API": `GET /sindri/ping`, `GET /sindri/status` and `GET /sindri/events`) and two files under
  Sindri's home ("Files"): the endpoint record it finds the API by (`control.json`) and the per-start human token
  (`auth/human.token`, API spec §6) it sends as `X-Sindri-Token`. `deps.yaml` declares them as the `SindriUi -> Sindri`
  **http** and **file** edges (one file edge covers both files); `node scripts/check-deps.mjs` finds them in the app's
  C# projects (a literal starting with Sindri's route prefix `/sindri/`; the literals `"sindri-app"` with
  `"control.json"`, and with `"auth"` and `"human.token"`) and fails on an undeclared or stale one. The head's path
  rule (`ControlFilePath.From` and `.TokenFrom`) follows the documented one in `docs/csharp/sindri.md` "Files".
- **Compiled bindings only.** `AvaloniaUseCompiledBindingsByDefault=true`, `x:DataType` on the window and every
  template, no `ReflectionBinding`, so the head publishes under NativeAOT.
- **View models carry no Avalonia types** (plain C#, `INotifyPropertyChanged`), so they test in the unit lane.
- **Hand composition, no container.** The app has a handful of view models and one client; the head builds its adapters
  and hands them to `SindriApp.Create(platform, clock)`, which builds the shell view model and the API client (a loopback
  `HttpClient` that never uses a proxy). A service locator outside a Host would fail `ARCH-DI-001`.
- **Packages.** MIT Avalonia packages only, all `[12.1.3]`: `Avalonia`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`
  (the library), `Avalonia.Desktop` (the head), `Avalonia.Headless` and `Avalonia.Skia` (tests).

## U0: theme, ports and preferences (UI spec §14, §15, §18 U0)

U0 turns the S1b window into the shell of the UI spec (`docs/superpowers/specs/2026-10-11-sindri-ui-design.md`). Its
contract unit added:

| Part | Where | What |
|---|---|---|
| Tokens | `Aw.Sindri.Ui/Theme/Tokens.axaml` | Every §14.1 colour as `Color.<Key>` and `Brush.<Key>`; the §14.2 ramp as `Type.<Step>.Size` and `.LineHeight` with one `Type.Scale`; `Space.*` (Double and `.Thickness`), `Radius.*`, `Size.*`, `Motion.*`, `Easing.Standard`; `Font.Text` (Inter) and `Font.Mono` |
| Type ramp | `Theme/Typography.axaml` | `TextBlock` classes `page-title`, `detail-title`, `body`, `body-strong`, `caption`, `eyebrow`, `button`, `mono`, `kbd` (a Styles file: a ResourceDictionary can't hold styles). `TextScale.Apply` scales them 0.9–1.3 from the token bases |
| Fluent | `Theme/FluentOverrides.axaml` | The §14.4 dark palette, button and tooltip brushes, `ControlCornerRadius` = `Radius.MD`, and the §11.1 focus ring: 2 px `PrimaryHover` at full opacity, 2 px outside the control, with the adorner layer's clip off (it clips an adorner to its control's bounds, which hid the ring); a full-width region (`focus-region`: the top bar, the page, the sheet) draws it 2 px inside instead. `Views/FocusRingTests` checks it as rendered on each control the keyboard reaches. No shadows |
| Mono font | `Theme/Fonts/` | JetBrains Mono 2.304 Regular and Medium, unmodified, with `OFL.txt` (SIL OFL 1.1, no Reserved Font Name), embedded as `AvaloniaResource`. From the release asset `JetBrainsMono-2.304.zip` (SHA-256 `6f6376c6ed2960ea8a963cd7387ec9d76e3f629125bc33d1fdcd7eb7012f7bbf`) |

`SindriApp.axaml` merges the tokens and includes the two style files. **No hex colour outside `Theme/`**, and
`InkTertiary` only for disabled text: the unit lane checks both, recomputes every §11.1 contrast pair from the token
values, and holds every Fluent value equal to its token (`tests/Aw.Sindri.Ui.Tests/Theme/`). `docs/sindri/DESIGN.md`
takes the ring, the darker primary hover and pressed fills, and `semantic-error` (X22).

**Ports** (`Aw.Sindri.Ui/Platform`, public because heads implement them; each has a fake and a contract base in
`tests/Aw.Sindri.Ui.Testing` that the head's adapter binds to):

| Port | Desktop adapter (Unit B) | Contract base |
|---|---|---|
| `IMotionPreference` (`ReduceMotion`, `Changed`) | macOS `NSWorkspace`, Windows `SPI_GETCLIENTAREAANIMATION`, GNOME `enable-animations`; bound over a drivable OS seam | `MotionPreferenceContract` |
| `ISingleInstance` (`TryClaim`, `Send`, `Messages`, `Retire`, `HolderProcessId`; `InstanceMessages.Focus`, `MaxLength` 512) | A local channel only the user can open, under `$AW_SINDRI_HOME/ui/`. A message only navigates: the app reads it as a `DeepLink` and ignores anything else. `Send` is true only once the running instance has **handled** the message, never because a lock is held: a holder that doesn't read gives no answer, and the message isn't handed over later. Handling is one decision, `InstanceMessage.TryAct()`: the app claims it on the UI thread just before acting (or the reader coming back for the next message claims it), the first claim wins and is the sender's answer, and the message is withdrawn (`InstanceMessage.Withdrawn` cancelled, `TryAct()` false) as soon as the sender has been answered or has given up, so a message acted on is never answered "not handled" and one whose wait ended is never acted on late (`SingleInstanceContract.A_message_acted_on_is_acknowledged_and_then_withdrawn`) | `SingleInstanceContract` |
| `ITray` (`Show(TrayView)`, `Shown`, `OpenRequested`, `QuitRequested`; `TrayState` Neutral, NotRunning, Stale) | Avalonia `TrayIcon`; menu labels escape `_` | `TrayContract` |
| `IClipboard` (`SetText`: true once the platform holds it, false when it refused) | `Views/AvaloniaClipboard` over the window's `TopLevel.Clipboard`: one adapter every head shares (Avalonia's clipboard belongs to the window), so it is not in `SindriPlatform`; the view models take it as a required constructor argument (no null object) | `ClipboardContract` (fake; the adapter under headless, which accepts a write but keeps nothing, so only the acknowledgement and the refusal are checked there) |
| `IPreferencesStore` (`Location`, `Read`/`Write` of the bytes, like the endpoint record) | `$AW_SINDRI_HOME/ui/preferences.json`, 0600, temp file and rename | `PreferencesStoreContract` |

A head hands its adapters to `SindriApp.Create(SindriPlatform, TimeProvider)` (`SindriPlatform` bundles them, so a
new port doesn't change the signature).

**The desktop adapters** (`Aw.Sindri.Ui.Desktop`, Unit B) keep the app's own files in a `ui/` directory of Sindri's home
(the directory `ControlFilePath` resolves; `UiStatePaths`), created 0700, every file in it 0600 (`OwnerOnly`; on
Windows the profile's ACLs do it):

| Adapter | How |
|---|---|
| `FilePreferencesStore` | `ui/preferences.json`; a write goes to a new 0600 temp file beside it, flushed to disk, then renamed over it |
| `SocketSingleInstance` | The claim is an exclusive lock on `ui/instance.lock` (the OS drops it when the process dies, so a crash leaves no claim); its holder records its process id in `ui/instance.pid` (0600; removed on exit), then removes a crashed instance's `ui/instance.sock` and listens there (a Unix domain socket, 0600; AF_UNIX on Windows 10 and later too; when the path is longer than 100 bytes (under sun_path's 104 on macOS and 108 on Linux), as in a deep temp home, the socket goes to `$TMPDIR/sindri-ui-<hash of the path>/`, 0700). A later launch writes one UTF-8 message ending in a newline, keeps its side open and reads one byte back: 1 once the holder's reader has handled the message (`Views/InstanceListener` reads from the app's framework start-up on: before the window shows it answers `focus` at once and keeps the latest deep link; after, `AppLifetime.Receive` handles each on the UI thread before the next is read), 0 refused. The listener refuses an empty message, one over 512 characters, one that isn't UTF-8 or one without its newline; it waits 1.5 s from a message's arrival for the handling and the sender waits 2 s, so a holder that doesn't handle it (an app whose framework start-up failed or hangs, or a wedged one) gives no answer. A message nothing handled in time, or whose sender has gone, is dropped; one already handed to the app is withdrawn (`InstanceMessage.Withdrawn`), and `Views/InstanceListener` claims it with `InstanceMessage.TryAct()` on the UI thread and acts only when that returns true, so a busy UI thread never acts on it late and an acted-on message is never answered "not handled" |
| `SingleLaunch` | `Program` claims before Avalonia starts; a later launch sends `focus` (or its one argument when it is a deep link, `sindri://` optional) and exits 0 once the running app handled it. With no answer it claims again every 50 ms for up to 3 s (a quitting holder refuses at once and goes within about a second, review run 5): if the holder ended meanwhile, this launch runs the app; otherwise it exits 1 with `sindri-ui: Sindri app is running but not answering (pid N): quit it or kill it` on standard error. Exit codes: 0 handed over (or the app quit), 1 a holder that doesn't answer, 2 the app failed (SND-UI-004) |
| `AvaloniaTray` | Avalonia's `TrayIcon`, created on the first `Show` and registered with the app through `Views/AppTrayIcons` (Avalonia makes the native icon only for an icon in the Application's `TrayIcon.Icons`); `TrayMarks` draws Sindri's mark in the theme's Ink, filled (Neutral), hollow (Stale) and hollow struck through (NotRunning), as macOS template images; menu labels double each `_` |
| `PolledMotionPreference` | The OS reader at start and every 2 s on the injected `TimeProvider`: macOS `NSWorkspace.accessibilityDisplayShouldReduceMotion` through `objc_msgSend` (`MacMotion`), Windows `SPI_GETCLIENTAREAANIMATION` (`WindowsMotion`), GNOME `gsettings get org.gnome.desktop.interface enable-animations` (`GnomeMotion`; no gsettings, a failure or a hang past 3 s reads as off) |

`Views/AppLifetime` (in the app, so every desktop head shares it) is the window's life around these: closing the
window hides it (the app stays in the tray; an OS or app shutdown still closes it), the tray's Open brings it
forward and Quit ends the app (`ShutdownMode.OnExplicitShutdown`) once the window's bounds are recorded and the
preferences queue is written (at most `AppLifetime.SettleWithin`, 1 s on the injected clock); when the OS ends the app
(⌘Q, the Dock, logging out) `ShutdownRequested` isn't cancelled, which would hold up a logout, but the bounds are
recorded and the UI thread waits up to the same second for the write. The tray follows `ShellState` (Normal: Neutral,
"Sindri · running"; NotRunning: NotRunning; every other state: Stale, "Sindri not responding"), and an instance message
is either `focus` (bring forward) or a `DeepLink` (bring forward, then the shell's `Navigate`); anything else is ignored,
and no message runs an action. `SindriApp` starts `Views/InstanceListener` in `OnFrameworkInitializationCompleted`; its
`ShellOpened` (`Views/SindriApp.Lifetime.cs`) builds the `AppLifetime` over the window, the tray, the shell's `Navigate`,
the shutdown and the settle step once the shell window shows, calls its `Start(desktop, shell)`, hands it the listener's messages (`Deliver`, which first applies a deep link kept from
start-up), and disposes it on exit.

**Preferences** (§12.3) are `UiPreferences`: `version` (1), `window` (x, y, width, height), `sheetWidth`, `lastPage`,
`seenIds`, `paletteHistory`, `textScale`, `reduceMotion`, camelCase JSON. A head's store only moves bytes; the app's
`ViewModels/Preferences` (`Load`, `Update`, `Flush`; one instance, built in `SindriApp.Create` and shared by the shell
and Settings, queues every read-change-write behind the one before) parses and writes them through `Api/PreferencesJson`, so clamping is the same
on every head: the window grows to 900 × 600 (bounds that
aren't finite are dropped), the sheet is 420–760, the scale 0.9–1.3, an unknown page reads as Home, and the id lists
drop blanks and repeats and keep their newest 500 and 20. A malformed file, or one of another version, reads as the
defaults.

**Copy** the app owns (§12.4) lives in `ViewModels/Labels.cs`. **Deep links** (§3.3) are `Navigation/DeepLink`.
**The caller headers** `X-Sindri-Caller: human`, `X-Sindri-Client: ui` and `X-Sindri-Token: <the per-start token>`
go on every request (§9.1, API spec §6); never `X-Sindri-Terminal` and never `Origin`. The token comes from the
`IHumanTokenSource` port (the desktop adapter `FileHumanToken` reads `auth/human.token` under the same home as
`control.json`), read at each call like the CLI's, so a restarted control plane's new token is sent with no app
restart. With no token file, or one the platform can't read, the call goes without it (the unreadable file is logged
with its path and the exception's type), and Sindri's 403 says why. The token goes only into its header: never into an
error, the log or the window. The status
carries `apiVersion` from API P1 (`SindriApiClient.AppApiVersion` is 1; absent means no mismatch).

**The shell** (UI spec §3.2, §4.1; `Views/Shell/ShellWindow` over `ViewModels/Shell/ShellViewModel`) replaces S1b's
window. `SindriApp.Create(SindriPlatform, TimeProvider)` builds the view model over the head's endpoint record and
preferences store, restores the preferences (window bounds, sheet width, last page), then shows the window and follows
the control plane; the platform wiring (tray, single instance, close-to-tray) receives the window through the partial
`ShellOpened` hook. The head's `Composition.Compose` passes its `SindriPlatform`; S1b's `Create(records, clock)` is
gone. Every view loads its XAML through the generated `InitializeComponent()` (never `AvaloniaXamlLoader.Load(this)`,
which leaves the `x:Name` fields null; `Views/NamedControlsTests` checks each view's), and `Views/StartupTests` drives
this start-up path the way the head does (`Create`, `Initialize`, `OnFrameworkInitializationCompleted` with a classic
desktop lifetime).

**Start-up never fails silently.** The start-up and the following run as one task on the UI thread; when it throws,
the app logs `SND-UI-004` through `SindriPlatform.Log` (an `ILogger`; the desktop head's is `Aw.Kernel.Telemetry`'s,
JSON lines on standard error, `AW_LOG_LEVEL`) with the exception's type only (no message, no stack: they can carry paths
and payloads), then ends with exit code 2 (`SindriApp.FailedExitCode`) instead of staying up without a window or tray.
`SingleLaunch` then writes `sindri-ui: failed (SND-UI-004); the log names the cause unless AW_LOG_LEVEL is none`
on standard error. `SingleLaunch` is also the head's boundary: anything that throws before or around the app (composing the
platform, claiming the instance when `ui/` can't be made, a hand-over socket it can't use, ending the instance and the
motion poll after the app quits; `ui/` removed while the app ran counts as already cleaned up) is logged the same way (`SND-UI-004`, the type
only, never the message, which can name the home), with a line that names the exception's type (`sindri-ui: failed (SND-UI-004: IOException)`; neutral words, since the failure can come after a normal run), and exit 2; never an unhandled exception.

| Part | What it shows |
|---|---|
| Top bar (48 px) | Brand; Home · Queue · Decisions · Services (counts empty until their routes); Later ▾ (Agents, Repos, Today, Automatic work, each "not served yet"); the ⌘K field, which opens the palette (U1); the health pill, which opens Health (while Sindri isn't running it is only the state: disabled like the navigation, its word undimmed, since the Not running card is the health state and Health has nothing to show). When they don't all fit (the 900 px minimum, text scale up to 1.3) the bar steps down in `TopBar.axaml.cs`: the ⌘K field folds to its magnifier, then the pill's age drops (the pill's accessible name keeps it), then the brand keeps its mark alone. The pill's word always shows |
| Home panels' bodies | "not served yet", centred in the panel |
| Home | The page title, three frames (Needs you, Running · With you, Queued), each "not served yet", and the Sindri health strip: the health line (§4.8) over the fields the status carries today (`Running · pid · epoch · API v1 · N services · profile <state>`; "Not running" when it isn't), which opens Health on a click or Enter |
| Queue, Decisions, Services, Settings | The title and "not served yet"; Settings is the slot `Views/Settings*` fills (⌘, opens it) |
| Health | S1b's status card (`HealthViewModel`: lease, API, profile, services, last event) |
| Detail sheet | A deep link's subject over the page at the right, 520 px (420–760, dragged by its left edge and remembered); Esc closes it and leaves an edge tab that reopens it; below 1100 px it covers the page with a Back button, focus moves to its heading, and the page (disabled under it) leaves the tab order and F6 until the sheet closes or stops covering |

The pre-API states (`ShellState`, highest first): **version mismatch** (the status's `apiVersion` differs from
`AppApiVersion`: a full-window panel with both versions and "quit both and run" around `sindri` in the command chip, no top bar), **not running**
(S1b's panel and reasons, closing with "Looks again every 2 s", the follower's retry wait), **connecting** (skeleton rows, first load only), **unreadable** (`SND-UI-003`, or Sindri's own 403 after the token retry with its code and detail: a banner with
the code and `sindri doctor`, its parts in a wrap panel so a long reason wraps and the command moves to its own line
rather than past the window's edge at 900 px, as the not-responding banner's does; the page keeps its last data and the pill reads "Unreadable · stale"), **stale** (30 s
without an event, a status or an answered ping, on the injected clock: the pill reads "⌛ stale · updated Ns ago" with
**Refresh** beside it; otherwise "✓ Running · updated Ns ago", re-worded every second). Refresh follows §4.1's command
lifecycle: while it is in flight it is disabled with its label kept and a spinner in its icon's place (still under
reduced motion), "Sending…" is announced after 300 ms through a polite live region (not shown), and a second click does
nothing. Its answer counts only when the follow decided no state while it was in flight, the views are still normal or
stale, and it came from the pid and epoch the follow checked with ping; otherwise it is dropped, so a late answer never
shows Running over Not running or Unreadable. After three failed refreshes the pill keeps "stale" and a banner under
the top bar reads "Sindri not responding: run `sindri doctor`" in full, since the sentence doesn't fit the pill at
900 px. The view models carry no Avalonia types; views bind the app's copy through `ShellText` (compiled bindings
resolve public members only, and `Labels` is internal). F6 / Shift+F6 cycle top bar, page and sheet; on macOS ⌘⌥→ / ⌘⌥← do the same (owner decision, 2026-10-12;
UI spec §8), since with the default "Use F1, F2, etc. keys as standard function keys" off F6 is the Focus key there and
reaches the app only with fn held (fn+F6 and fn+Shift+F6). Ctrl+F6 isn't bound. The window's minimum is 900 × 600.

**Focus never goes nowhere (§11.2).** When the focused control disappears, focus moves to a neighbour: Refresh keeps
focus through its in-flight phase and gets it back when the refresh fails; when Sindri answers and Refresh hides, the
health pill gets it. Reopening the sheet from its edge tab moves focus to the sheet's heading at every width (beside
the page the tab hides as the sheet opens), as opening a covering sheet does. Closing the sheet returns focus to the edge
tab when the tab reopened it, otherwise (a deep link) to the page region. Narrowing the window below 1100 px with the
sheet open moves focus on the page (now disabled under the sheet) to the sheet's heading. A state change that hides the focused control moves focus to its region's neighbour: the pill in
the top bar, the page region in the page (`ShellWindow.KeepFocus`, `TopBar.KeepRefreshFocus`).

### Settings and reduced motion (§3.3 ⌘,, §11.4, §14.2)

`Views/SettingsView` over `ViewModels/Settings/SettingsViewModel`: the in-app **Reduce motion** switch and **Text size**
(a slider, 90–130 % in 10 % steps, clamped by `UiPreferences.Clamp`). Both are kept in the preferences: a change is
written over whatever else the file holds (read, replace the two fields, write, through the app's one preferences queue, shared with the shell), and a write
the platform refuses leaves the change applied for the session. Motion is reduced when the OS asks
(`IMotionPreference`, re-read on `Changed`, posted back to the UI thread) **or** the switch is on; while the OS asks, the
page says "On in your system settings" and the switch can't turn it off. Settings is local preferences, so it stays
reachable while Sindri is not running: ⌘, shows it over the Not running card, and Esc returns to the card.

The theme is changed in one place each: `Theme/Motion.Apply(resources, reduce)` sets every `Motion.*` duration to 0, or
back to its token value, and `Theme/TextScale.Apply` scales the ramp. `Views/SettingsTheme.For(resources)` is the view
model's apply callback over both. **At start-up** `SindriApp` builds
`new SettingsViewModel(preferences, platform.Motion, SettingsTheme.For(Resources))` (the shared `Preferences` the shell also has) on the UI
thread, awaits `Load` once before the window shows (the stored scale and motion apply before the first frame; an
unreadable preferences file leaves the defaults), puts `new SettingsView { DataContext = settings }` in the shell's
`SettingsSlot`, and disposes it on exit. The shell and Settings each write only their own fields, through the one queue, so neither writes back what it read before the other's change. Stock Fluent controls'
own transitions don't read the `Motion.*` tokens: `SettingsTheme.Follow(window, settings)` keeps the window's
`reduce-motion` class in step with `IsMotionReduced`, and `Theme/FluentOverrides.axaml` turns those transitions off
under it (today the ToggleSwitch knob); another stock control with its own transitions gets its line there.

### LiveSetting on macOS (§11.4, §18.2; U0 spike)

**Finding: supported in Avalonia 12.1.3, for a `TextBlock` whose `Text` changes; unconfirmed by ear.** The owner's
VoiceOver pass below confirms or refutes it.

**Method:** read the source at tag `12.1.3` (commit `8eeda4f6`) and checked the shipped binary
(`~/.nuget/packages/avalonia.native/12.1.3/runtimes/osx/native/libAvaloniaNative.dylib`).

- `AutomationProperties.LiveSetting` (`src/Avalonia.Controls/Automation/AutomationProperties.cs`) is read by
  `ControlAutomationPeer.GetLiveSettingCore`. `TextBlockAutomationPeer` raises a Name change when `Text` changes;
  `ControlAutomationPeer` raises none when `AutomationProperties.Name` itself changes, so a bound name on a `Border` or
  panel is **not** announced.
- macOS (`native/Avalonia.Native/src/OSX/automation.mm`): the element reports `AXARIALive` (polite or assertive); on a
  Name change it posts `NSAccessibilityTitleChangedNotification` and, when the live setting isn't Off,
  `raiseLiveRegionChanged`: `AXLiveRegionChanged`, then `NSAccessibilityAnnouncementRequestedNotification` on the
  `AvnWindow` with the new name (Polite = `NSAccessibilityPriorityMedium`, Assertive = `High`). The same shape as
  Windows' UIA `LiveRegionChanged`.
- The dylib imports `_NSAccessibilityAnnouncementRequestedNotification`, `_NSAccessibilityAnnouncementKey` and
  `_NSAccessibilityPostNotificationWithUserInfo`, and holds `AXARIALive`, `AXLiveRegionChanged` and
  `raiseLiveRegionChanged`.
- History: PR #20473 (2026-01) added the attribute ("problems getting VoiceOver to actually handle the events");
  PR #21045 (2026-04) added the announcement. Open issue #22312 (2026-09): setting a live element's name to `""`
  crashes the app (`NSInvalidArgumentException`) once VoiceOver or Accessibility Inspector has read the window.

**Rules for later phases' announcements** ("N now need you", "<id> cleared", §12.4): a dedicated `TextBlock` with
`LiveSetting="Polite"` whose `Text` changes; never set it to `""` (hide it instead); give it no separate
`AutomationProperties.Name` (the peer announces the name, and a second binding may lag the text); only in a top-level
window (popups aren't `AvnWindow`s). Two more from the source: once assistive tech has read the element, every `Text`
change is announced even while it is hidden, so a text that must not be spoken (a resting text, set so the next
announcement is a change again) is set only while `LiveSetting` is Off; and a hidden element has no native element, so
show it a moment before its text changes. Refresh's "Sending…" (`TopBar`'s `RefreshSending`) does both: the region is in
the tree from the click (`!Health.CanRefresh`), is live (`Polite`, through its `live` class) only while
`Health.IsSending`, which turns on before the text becomes "Sending…" and off before it goes back to its resting
"Refresh"; it is zero-width and transparent, so it takes no room and shows nothing. The Unreadable banner, the Not responding banner and the Not running card follow the same rules (WCAG 4.1.3; review run 2): `ShellWindow`'s `UnreadableLive`, `NotRespondingLive` and `NotRunningLive` sit outside the banners, zero-width and transparent, so each is in the tree before its state comes; each is live only while its state holds (`IsUnreadable`, `IsNotRespondingBanner`, `IsNotRunning`), and `ShellViewModel` changes that flag before its announcement ("Unreadable: " or "Not running: " and the reason, or the not-responding sentence), resting at "Sindri" while Off. Two more follow the same rules (review run 5): `MismatchLive` announces the version-mismatch card's sentence, and the card's heading (`VersionMismatchText`, focusable) takes focus when the state is entered, also mid-session, since the card hides the whole shell and leaves no neighbour for `KeepFocus`; `RunningLive` says "Running" (the pill's own label; spec §12 has no recovery sentence) when the state returns to Normal after trouble (Not running, Unreadable, stale or not responding, a mismatch), is silent at the first connect, and rests when trouble comes again (`ShellViewModel.IsRecovered`, `RecoveredAnnouncement`). `CommandOutcomeLive` (review run 5, K2) follows the same rules: it speaks a command the palette started and closed on before its outcome (see "A closed palette" under the palette), flag (`CommandOutcome.IsLive`) then text. If the pass finds it silent, the fallback is a head adapter posting
`NSAccessibilityAnnouncementRequestedNotification` through ObjC interop; no production seam is added before that.

**The owner's VoiceOver pass** (a throwaway probe window: a polite `TextBlock` changing every 3 s, an assertive one, a
`Border` with a bound name and `LiveSetting="Polite"`, and a focused `TextBox`):
1. With focus in the TextBox, VoiceOver speaks each polite change after the current speech, and the assertive one
   interrupts it.
2. With the VoiceOver cursor on the live TextBlock, it still speaks, and nothing is said twice.
3. The Border's name change stays silent (as the source predicts).
4. A change right after launch, before VoiceOver has walked the window, is or isn't spoken (the native element exists
   only once something has read the tree).
5. Accessibility Inspector shows `AXARIALive` on the element and the `AXLiveRegionChanged` and announcement events.
6. The app in the background: is the announcement still spoken?

**Linux (owner question, provisional owner default (2026-10-14 night)).** The Linux real-app proof found that Avalonia's
AT-SPI bridge exposes no live-region attribute and emits no announcement signal, only name changes, so Orca probably speaks
none of the announcements above. macOS VoiceOver is the primary accessibility target; on Linux the announcements are a
known platform limit, tracked for an Avalonia upstream fix or a later AT-SPI workaround. Nothing in U1 depends on it:
every outcome is also shown on screen.

## U1: catalog, headers and freshness (UI spec §18 U1)

U1 gives the app the API's catalog of actions, the headers and token every call carries, and the views' freshness.

**Requests (§9.1, API §6).** Every request sends `X-Sindri-Caller: human` and `X-Sindri-Client: ui`, and
`X-Sindri-Token` read from `<home>/auth/human.token` before each request (a restart mints a new one; a missing file
sends none, so the API answers `SND-AUTH-001`). Never `X-Sindri-Terminal` or `Origin`. After an accepted command the
next view query carries `X-Sindri-After: <sequence>` once (best effort, below). A refusal Sindri explains is that error: a 403 with a `title`,
or any other non-2xx whose ProblemDetails has an SND `title` and a `detail` (a command's 409 `SND-API-010`, for one),
shown as served; anything else stays `SND-UI-003`. The token comes through U0's port (`IHumanTokenSource`, above):
on `SND-AUTH-001` the call re-reads the token and retries once, then a status or ping refusal is the Unreadable banner
with Sindri's code and detail. Every such decision keys off the code, never the detail sentence. Only `SND-UI-002`
(nothing answered) is Not running.

| Route | Read when | Shown as |
|---|---|---|
| `GET /sindri/commands` | on connect and after every reconnect (a new epoch) | the controls, Health's ACTIONS card, the palette's Actions |
| `GET /sindri/status` | on connect and on each event | the pill (`health.word`, `health.problems`, `views.projectedAt`) |
| `GET /sindri/services` | on connect and on each event | the stalled / rebuilding banner (`asOf`, `empty`) |
| `POST /sindri/views/rebuild` | Rebuild views (banner, palette) | the command lifecycle |

**Controls (§7, `ViewModels/Controls/*`, `Views/Controls/CommandControl`).** One control per catalog row, keyed by
action id (labels in `Labels.Actions.cs`): `ready` is a button (it runs only when the app has a sender for it; U1 sends
`RebuildViews`), `pending:<phase>` a disabled button with a focusable `InkSubtle` line "arrives in <phase> · <cli>" and
Copy, `cli-only:<reason>` the copy block (the CLI, Copy, and "Needs a typed confirmation at a terminal." for
`typed-confirmation`). A CLI with a control, bidi or invisible character is neither shown nor copied. Lifecycle (§4.1):
sending (enabled for the keyboard so focus stays on it, the view model refuses a second run; label kept, spinner; "Sending…" announced after 300 ms; a second click does nothing), accepted
("Rebuild views: done", 2 s), refused or failed (the code and `detail` inline, as served, and announced as "<code>: <detail>"; every run first rests the live region on the label so a repeat outcome is a change; a retry sets the phase to Sending before it clears the old outcome, so nothing reads half-cleared), Copy ("Copied", 1.5 s; a second Copy inside the 1.5 s rests the region and says it again). A send cancelled with its token (the window closing) leaves the phase Idle. The control's live region is dark (`IsMuted`) only for a run the command palette started and still announces; otherwise the control speaks its own lifecycle.
The clipboard is a port (`Platform/IClipboard`, `Views/AvaloniaClipboard`; see the ports table).

**Freshness (U10, E-14).** The pill reads `health.word` (Healthy, Degraded, Stalled, Rebuilding), its age from
`views.projectedAt`, then each `health.problems[].text`. Connection states win. While `views` or `health` is null (P1,
or P2 before unit E) it reads "Running" with the age from the last event or ping (Q1; the one decision point is
`HealthPill.Show`). A stalled services view shows the banner under the top bar with the view's own `empty` sentence and
**Rebuild views** (the catalog's row through the control renderer); rebuilding shows a progress banner. Each event
re-reads the status and the services view at most once per view per 250 ms (§9.2; `HealthViewModel.RefreshEvery`,
`ControlPlaneFollower.RefreshOnSignal`): the first event of a burst re-reads at once, the rest collapse into one
re-read when the window ends. A projection still behind the ledger (`cursor < ledgerHead`) or rebuilding is read again
on the same beat with no event needed (a stalled one waits for an event, it won't move by itself). `X-Sindri-After`
(the accepted command's sequence) is best effort and has one lifetime: it rides the view queries only (status, services;
never ping, the catalog, a command or the stream), goes on the stale-token retry too, gives that call a 5 s deadline (the
server waits up to 2 s when the views are behind, and answers a stalled projection at once), and is used up by the first
answer of any kind, SND-API-011 included. A ping that shows another pid or epoch (another ledger) drops it unused. So
SND-API-011 ("views behind") is not trouble and never repeats: the follower reads the status once more at once, without
the header, and shows what Sindri says (stalled, rebuilding); only if that is behind too does the last data stay and the
next beat read again. On connect, a first status that is behind is read the same way, and the URL is followed once a
status was shown. A manual Refresh that hears SND-API-011 re-reads once more at once, without the header (two status calls: the first carries the sequence, the second does not), exactly as the follower does, and is not a failed refresh (it doesn't count toward "not responding"). A services read that fails for any reason but the
missing route before P2 (SND-UI-003) leaves the banner as it was; a catalog that failed on connect is read again with
the next refresh. The views banner has live regions (stalled sentence, "Rebuilding views", "Views are up to date"), flag
then text. E-15's `invalidates` waits for P5.

**The palette (§8, `ViewModels/Palette/*`, `Views/Palette/CommandPalette*`, `Views/Palette/ShortcutsSheet*`).** ⌘K (Ctrl+K off macOS), a click on the ⌘K field,
or Enter or Space on it opens it with focus in its field. Sections: **Actions**, every catalog row not in Sindri,
ready first, then pending (shown with "arrives in <phase>", never run), then cli-only ("Copy: sindri …"; Enter copies,
"Copied" for 1.5 s, the palette stays open); U1 has no selected row, so Actions lists the whole catalog (Q3). **Go to**:
Home G H, Queue G Q, Decisions G D, Services G S, Health G ., Settings ⌘,. **Sindri**: Rebuild views, Stop Sindri
(Enter goes to Health, where it lives; never sent from the palette, its confirm is U4: Q4) and Health. Every catalog row
appears once. Typing filters by a fuzzy match (the query's letters in order, spaces ignored) on labels and ids; ↑/↓
move between entries that do something, Enter runs one, Esc or a click on the scrim closes it, and focus returns to
what had it (the ⌘K field when that is gone). Each entry run is remembered in `paletteHistory`, newest last.
It opens, and each refilter shows, at the top with the first heading in view and the first entry that does something
selected; only ↑/↓ scroll the list to follow the selection (`FollowsSelection`; U1 finding F1). Enter on a ready
command (Rebuild views) follows `CommandPaletteViewModel.RunPolicy`, the one place that decision lives (U1 finding F2,
provisional until the owner decides): by default (`StayOpen`) the palette stays open and the entry shows §4.1's
lifecycle in place, a spinner beside its kept label and "Sending…" (disabled, a second Enter does nothing), then
"<label>: done" for 2 s, or Sindri's code as its status with the detail as served under it (an accepted entry's accessible
name is the outcome alone: "Rebuild views: done"). Esc closes the palette at any point and the command keeps running.
`CloseFirst` closes, then sends. **Who speaks a run.** The palette never mutes controls as a whole. When it runs an entry it
mutes that entry's control only, for that run (`CommandPaletteViewModel.Runs.cs`), and it announces the lifecycle of every
entry it ran, whatever the selection or the query became meanwhile: "Sending…" after 300 ms, then the outcome or the
refusal; the selected entry is announced with its position ("Rebuild views: done, 3 of 14"), another ran entry by its name
alone. Nothing is said in a send's first 300 ms and nothing when the outcome settles back to ready. Each run says an entry's status once
(the outcome arrives through several property changes, and moving the selection off the entry after "Sending…" does not
say "Sending…" again), and a run says its outcome even when it equals the last run's (a retry's same refusal, an
accepted retry inside the 2 s, a second Copy inside the 1.5 s): the palette's region rests on a neutral text with its gate
(`IsAnnouncing`) dark, then says it, flag before text. The mute ends when the palette has announced the run's outcome
(accepted or refused) or, for a copy, when "Copied" clears. **A closed palette.** When the palette closes while a send is
still in flight (Esc, or the `CloseFirst` policy), the control stays muted and the shell's command-outcome region
(`ShellWindow`'s `CommandOutcomeLive`, `ShellViewModel.CommandOutcome`, a `CommandOutcomeRegion` in `ViewModels/Palette`; always in the tree, zero-width and transparent,
flag then text, never "") speaks the rest of the run once: "Sending…" if it was not said yet, then the outcome or the
refusal. Only a control whose own view is on show (the Rebuild views control of the stalled banner while the banner
shows) is handed back at the close and speaks itself; the Rebuild views control has no view otherwise, so its outcome
would be spoken by nobody. The decision is made at the close: a view that appears or goes away mid-send is not
re-decided. A cancelled send returns the control to Idle, so the palette lets go of it. Known limit: a catalog
replaced mid-run (a reconnect) releases every run, so a send that was already in flight is spoken by no live region.
A send that did not start from the palette is never muted: its control speaks the outcome even while the palette is
open, and the palette says nothing about it. A ready row the app has no sender for (the catalog's `IssueChallenge`,
`RegisterRecipe`) is shown as "not in the app yet" and skipped by Enter. U2's actions are not among them: `CreateItem`
runs (ready; Enter closes the palette and opens the Create item form, whose own control sends it), and `CloseItem:done` /
`CloseItem:cancelled` read "on the item's sheet" and are skipped, because they send the version the sheet shows. Every action id in the catalog has its own
label, and an id without one shows a humanised form of itself, never the raw `X:y`.

**The `?` sheet.** `?` (not while typing in a field, nor over the palette) opens §8's keys plus each catalog row's own
`shortcut` (none yet); it takes focus, Tab cycles inside it, Esc closes it and returns focus. The sheet lists §8's
table; the keys U1 serves are ⌘K, `?`, ⌘, and F6 (the G-chords, J/K, O, A and the rest arrive with their views).

**Proof fixture (§18.2): `scripts/sindri-ui-fixture.sh`.** Everything goes through `scripts/sindri-prove.sh`, whose
guard runs first (a temp or proof `AW_SINDRI_HOME` only). `up` writes the live profile (a 5 s heartbeat, port 0),
approves it as the owner and starts `sindri --headless`; `stall` inserts one `service.ran` row with an unparseable
payload into `ledger.db` (`STALL_TYPE` overrides the type); `unstall` deletes it; `drift` changes the heartbeat's
cadence so the approval no longer matches; `down` stops it; `--large` is reserved for U2/U3. Test:
`bash scripts/tests/sindri-ui-fixture.test.sh` (the guard, usage, the stall SQL on a temp ledger, a stub SINDRI).

```bash
H=$(mktemp -d "${TMPDIR:-/tmp}/sindri-u1.XXXXXX"); export AW_SINDRI_HOME=$H SINDRI=<built sindri>
scripts/sindri-ui-fixture.sh up
AW_SINDRI_HOME=$H <published sindri-ui> &      # bash src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh <dir>
scripts/sindri-ui-fixture.sh stall             # the stalled banner and Rebuild views
scripts/sindri-ui-fixture.sh unstall           # then click Rebuild views
sqlite3 -readonly $H/ledger.db "select type, actor from events where type in ('command.handled','views.rebuilt') order by seq desc limit 2"
scripts/sindri-ui-fixture.sh down
```

**Tests.** Unit lane: `Palette/*` (the palette and the sheet), `Controls/ControlViewModelTests`, `Shell/FreshnessTests`
(the pill, the banners, the catalog on connect, the 250 ms re-read), `Api/*` (headers, token, ProblemDetails, and
`GoldenContractTests`, which deserializes every golden through `SindriUiJsonContext`). Integration lane:
`Views/CommandPaletteTests` (⌘K, the field's click, arrows, Enter, Esc returning focus, `?` never while typing; frames
`palette`, `palette-filtered`, `palette-top`, `palette-sending`, `palette-done`, `palette-refused`, `shortcuts`) and `Views/CatalogFramesTests` (frames `u1-banner-stalled`,
`u1-banner-rebuilding`, `u1-control-sending`, `u1-control-accepted`, `u1-health-controls`). The fake serves P1's and
P2's goldens (`tests/Aw.Sindri.Tests/Golden/Api/**`) and U1's spec-shaped ones (`tests/Aw.Sindri.Ui.Tests/Golden/SpecShaped/**`, to be replaced by P2's as they land).

**Real-app runs (the Linux proof).** A run of the real app on Linux uses its own Xvfb display (for example `:99`) and a
private AT-SPI bus with a private `XDG_RUNTIME_DIR`. It never inherits `DISPLAY`, never uses the owner's desktop (`:0`,
`:1`) and never touches `/run/user/<uid>/at-spi`. The proof tools under
`~/.agentic-workflow/jthorare-agentic-workflow/proof/sindri-ui-u1/raw/tools` enforce this: `session-up.sh` brings the
session up, `env.sh` refuses to load unless the session is private, and `privsession.py` is the private-bus helper.

## U2: queue, item detail, timeline, Create and Close item (UI spec §18 U2)

Built against the fake `ISindriApi` because API P3 isn't built: every P3 route is served from a **spec-shaped
fixture** under `tests/Aw.Sindri.Ui.Tests/Golden/SpecShaped/` (listed below), each to be replaced by P3's golden
(E-17). The real catalog keeps `CreateItem`, `CloseItem:done` and `CloseItem:cancelled` at `pending:P3`, so against
today's control plane the Create button and the sheet's Close controls draw disabled with "arrives in P3"; the tests
use `commands/catalog` (P1's catalog with those three rows `ready`).

**Routes (API P3).** `GET /sindri/queue` (`QueueView`: `groups[]` of `{group, items, empty}` in the API's order,
every group present, E-18; the view's `empty`; `asOf`), `GET /sindri/items/{id}` (`ItemView`: the summary fields plus
`version`, `createdBy`, `waitingOn`, `instance`, `createdAt`, `updatedAt`, `asOf`), `GET /sindri/items/{id}/timeline?after=`
(`entries[]` of `{sequence, time, type, actor, line}`, `next`), `POST /sindri/items` (`{title, repo?, body?, link?}` →
`{accepted: {commandId, sequence}, itemId}`), `POST /sindri/items/{id}/close` (`{reason, itemVersion}` →
`{commandId, sequence}`). Field names the API spec leaves open are the U2-API edits in the PR description. The client
takes `X-Sindri-After` from `ItemCreated.accepted.sequence` as from every accepted command.

**Codes.** `SND-ITEM-404` (the sheet shows its `detail` with Back), `SND-ITEM-409` (the sheet re-reads the item; the
pressed control shows the `detail` inline while the item still allows it, else the sheet's own line does, §9.1).
Tests key off the code and the golden's `detail`, never a copy of the sentence.

**What the app shows.**
- **Queue page** (`G Q`; `Views/Queue/QueuePage`): the API's groups as collapsible sections with counts; empty groups
  hidden, a filtered group (`G R` running, `G W` with you) shows its own sentence; the view's `empty` when every group
  is empty; skeleton rows on the first load only. Rows (`ItemRow`; Home uses `CompactItemRow`): id, title, repo, the state as a word, step, age,
  and the API's `primaryAction` as the catalog's control (pending rows draw disabled with their phase). A row sends
  nothing itself: Enter or a title click opens its sheet.
- **Home** (`HomePage`): Running and With you (With you first, at most five, then "Show all N in Queue", which opens
  the Queue on `G R`) and Queued (five, then "Show all N"), each with its group's own sentence when empty (E-18), in a
  compact row. Needs you stays "not served yet" until U3. The Queue tab carries the queue's count.
- **Item sheet** (`Views/Item/ItemSheet`, in the shell's sheet when the subject is `item/<ItemId>`): title, repo, the
  state as a word, version, age, created by, waiting on, instance; the item's `actions` ∩ the catalog as controls
  (kept across refreshes so their lifecycle survives); `CloseItem:*` send the version shown; the timeline paged by
  `next` with **Load more** (a refresh re-reads the pages loaded so far).
- **Create item** (`C`, the Queue page's button; `Views/Item/CreateItemDialog`): title, repo, body, sent by the
  catalog's `CreateItem` control; accepted shows "Create item: done" for 2 s, then the form closes. The new row comes
  from the stream's re-query, never from the answer. The form is a modal dialog: counted as an overlay (F6, Ctrl+K,
  Ctrl+, and `?` do nothing), Tab cycles in the card, focus returns to the opener (else the Create button) on Esc, Cancel
  and auto-close. The title field is "Title (required)"; Create stays enabled, and a blank submit shows "Title is
  required" under the field, speaks it once, focuses the title and sends nothing. No second create while the outcome shows.
- **Destructive confirm** (E-8, all controls): a destructive row's press asks "<label> <subject>?" with **Confirm**
  and **Keep** (focused; Esc keeps). Controls on a subject are named with it ("Close item ITM-…", §11.2).
- **Refresh:** `HealthViewModel.Follow` registers the queue and the open item; the follower re-reads them after the
  status and services on connect and on every event (U1's 250 ms debounce). No payload is folded.
- **Keys** (`ShellViewModel.Keys`): the `G` chord (1 s on the injected clock, "G …" while it waits: `N`/`H` Home, `Q`,
  `R`, `W` Queue filters, `D`, `S`, `.`), `J`/`K` rows, `Enter` opens the selected row, `C` create; never while typing
  in a field or over a layer. Esc closes the create form, then a confirm, then the sheet.

**Spec-shaped fixtures (replace with P3 goldens of the same names).** `queue/{fixture,empty,stalled,large}`,
`items-id/{new,after-close,not-found}`, `items-id-timeline/{fixture,page-2}` (the fixture a full page of 100),
`items/created`, `items-id-close/{accepted,stale-version}`, `commands/catalog` (P1's catalog with CreateItem and
CloseItem:* `ready`). Marked extras, not P3's: `queue/extra-p4-waiting` (a needs-decision row), `queue/extra-p11-parked`,
`queue/extra-p11-running`, `items-id/extra-p4-needs-decision`. In P3 every `primaryAction` is null, `waitingOn` and
`instance` are null, and `owner` is null (README X61: multiplayer later; the records accept it, no owner UI).

**Owner decisions (2026-10-12).** Four queue groups (API §3.6); a parked item shows in Waiting on you with a "parked"
badge from its `state`. The sheet shows `body` (scrubbed, plain text) and `link`. The sheet stays open after Close.
The destructive confirm reads "<label> <id>?" with Confirm and Keep. Actors (`createdBy`, the timeline's `actor`) are
opaque strings shown exactly as served; the app maps no fixed set (README X61), and assumes nothing about where an
agent session runs. Running rows' agent, place and budget arrive with P11 (open for U5/P11).

**§8's guards and filter.** While the pointer is over the Queue panel or Home's queue panels, only rows the page does
not show yet wait behind the "N new" pill (spoken through the shell's gated queue region); removals and in-place
updates show at once. Leaving, or the pill, shows the held rows. For 500 ms after a refresh moved the selection because
its row cleared, single keys do nothing. `Y` copies the selected row's primary-action CLI (through its control:
"Copied"); `/` opens the Queue page's filter (id, title, repo; "Nothing matches"), the last Esc layer after the sheet;
Home's panels ignore it. The top bar shows "G …" while the chord waits: a visual cue only, with no live region and
not a control element for a screen reader.

**Deviation: the page narrows beside the sheet (owner question, provisional owner default (2026-10-15)).** UI spec §3.2 has
the sheet sit "over the page". With the sheet beside the page (1100 px and wider) the page's right margin reserves the
sheet's clamped width (420 to 760 px), so the page is narrower while it is open (340 to 540 px at 1100 to 1300 px with a
760 px sheet) and the focused row's ring, drawn in the adorner layer above the sheet, is never drawn over it. Below
1100 px the sheet covers the page as before. The alternative is a full-width page clipped at the sheet's edge
(`ClipToBounds`); the owner decides.

**Selection is focus (U2 review run 1).** A refresh reconciles in place: groups by name (so a collapsed group stays
collapsed), rows by id and timeline rows by sequence are updated, and the bound lists (`ObservableCollection`, via
`Patch`) change only where they differ, so the focused control keeps focus. One selection is the focus: J / K select
the next or previous row of the page they are pressed on (Home: Running and With you, then Queued, as drawn; the
Queue page: its visible, expanded groups) and focus that row's frame; a row, or a control in it, that gets focus
selects it; Enter and Y act on the selected row only while it is on the page, and J / K / Enter / Y belong to the sheet
while focus is inside it. A filter change that hides the selection clears it silently; a refresh compares against the
unfiltered view, so a row hidden by the filter is not "cleared". When the selected row clears, the selection moves to
the row now at its place, "<id> cleared" is spoken once through the gated queue region, and focus goes there if the
old row had it. The sheet's subject is set by every deep link (before the connection too: the first re-read loads it),
and Esc from the sheet returns focus to the row that opened it, by id.

**Focus and speech, review runs 2 and 3.** Every selection call names its `QueueScope` (`Page`, the Queue page's visible,
expanded groups; `Home`, the two panels as drawn); there is no default, so a caller cannot pick the wrong page. A
selected row that a refresh leaves off the page it was selected on (served in a group Home does not draw, pushed past
Home's cap of five, or in a group the Queue page's filter hides) is treated as cleared from that page: the selection
passes to the row now at its place, "<id> cleared" is spoken once, and focus follows if the old row had it. "N new" and
"<id> cleared" in one refresh are one sentence, spoken once. Pressing the "N new" pill hides it and moves focus once, onto
the first row it showed (the window's keep-focus fallback waits while a row request is pending). Esc from the sheet
returns focus to the opener row by id, else the edge tab that opened it, else the row the selection moved to, else the
page region. Load more is keyed to the item it was sent for (a page in flight for an item the sheet left blocks nothing
on the next); a page that fails is spoken once and the rows stay, and a page that lands for another item, or after a
refresh moved the cursor, is dropped. A Close that meets a 409 does not return until a read has settled the conflict
(the sheet speaks it when the item no longer allows the action, the control when it still does), so it is spoken once.

**The event stream (P5).** API P5 replaces `/events`' body with `PublicEvent`; the follower switches its event record
when P5 merges. U2 doesn't touch the stream: it only re-reads views on each event.

**Proof recipe (once P3 is on main).** Under `scripts/sindri-prove.sh`'s guard:

```bash
H=$(mktemp -d "${TMPDIR:-/tmp}/sindri-u2.XXXXXX"); export AW_SINDRI_HOME=$H SINDRI=<published sindri>
scripts/sindri-ui-fixture.sh up && scripts/sindri-ui-fixture.sh items      # items --large for the fold test
AW_SINDRI_HOME=$H <published sindri-ui> &                                  # bash src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh <dir>
scripts/sindri-prove.sh human item add "Proof: appears live"               # the row appears within 1 s, no click
scripts/sindri-ui-fixture.sh close <id>                                    # then Close in the app's sheet: SND-ITEM-409
scripts/sindri-prove.sh curl GET /sindri/queue | jq -r '.groups[] | "\(.group) \(.items|length)"'
scripts/sindri-ui-fixture.sh down
```

**Tests.** Unit lane: `Queue/QueueViewModelTests`, `Item/ItemSheetViewModelTests`, `Item/CreateItemViewModelTests`,
`Shell/ShellItemsTests` (a new item appears on the next event without a click; the item link; the keys), the confirm
facts in `Controls/ControlViewModelTests`, and the P3 facts in `SindriApiContract` (fake and client). Integration lane:
`Views/QueueFramesTests` (frames `u2-queue-populated`, `u2-queue-empty`, `u2-queue-loading`, `u2-home-panels`,
`u2-item-sheet`, `u2-item-timeline`, `u2-item-404`, `u2-item-409`, `u2-create`, `u2-close-confirm`).

## How it finds the control plane

Every call carries this start's human token (`IHumanTokenSource`, read at each call; see the caller headers
above). A call Sindri refuses with 403 `SND-AUTH-001` (a missing or stale token) is retried exactly once with the token
read again from `auth/human.token`, since a restarted control plane has minted a new one; there is never a third call.
A follower call (ping, status, the stream) refused after that retry (or any other 403 with Sindri's ProblemDetails) is the **unreadable** banner with
Sindri's code and `detail` as they are ("SND-AUTH-001: A human caller needs this start's token in X-Sindri-Token (fix:
restart the app or rerun the command)"), never the token. The app keeps looking every 2 s and recovers on its own once
the token is good. A ping refused while following ends the follow, so the window names the refusal within one ping
beat (10 s). A refused Refresh click counts only as a failed refresh; the next ping beat shows the refusal.

1. The endpoint-record port (`IEndpointRecordSource`) reads `control.json`. The desktop adapter reads it from
   `AW_SINDRI_HOME`, else `$AW_STATE_DIR/sindri-app`, else `~/.agentic-workflow/sindri-app`, the same place the control
   plane writes it. No record means "Not running". A record whose URL isn't on this machine (loopback: 127.0.0.0/8, ::1
   or localhost) reads as Not running with "The endpoint record can't be read: … isn't on this machine" and the record's path; the app never calls that address.
2. `GET /sindri/ping` must answer the record's pid **and** epoch; otherwise the record is stale and reads as "Not running".
3. `GET /sindri/status` fills the window. A lower epoch than the last one means a new ledger, so the stream cursor
   starts over. Each `/sindri/events` event (server-sent events, resumed from the last
   sequence seen) triggers one status refresh; a burst of events triggers one. When the stream ends (the control
   plane stopped or was killed), a status refresh fails, or `control.json` changes (checked every 2 s; a stopping
   control plane can hold its stream open), the app looks again, every 2 s while nothing answers. A record the
   platform can't read, or one with a relative `url`, reads as "can't be read" with the reason, and the app keeps trying. The port
says why in its own terms, as a value and not an exception (`IEndpointRecordSource.Read` returns an `EndpointRecordRead`: the bytes, none, or an `EndpointRecordFailure`: `NoPermission`, `NotAFile`
or `Unreadable`; `EndpointRecordSourceContract` binds the desktop adapter to a real directory and a mode-000 file), since
.NET on macOS and Linux throws the same `UnauthorizedAccessException` for both; the desktop adapter checks for a
directory at the path first. The reason is the app's own words ("this user has no permission to read it", "it isn't a
file (something else, such as a directory, is in its place)", or "the platform couldn't read it"), never the platform's
message, which names the full path; the path shows shortened (`~/…`) on its own line, as every path in the window does.
4. While it follows, the app pings every 10 s (S1's stream sends no keep-alive, and a control plane with nothing
   scheduled sends no events). A ping answered with the followed pid and epoch keeps the views fresh; failed pings, or
   another process's answer, for 30 s read as stale.

## What the Health page shows

| Part | From the status record | Words |
|---|---|---|
| State pill | `running` | `Running`, `Not running` (with the reason and `sindri` as the fix), `Connecting…` |
| Lease | `pid`, `epoch` | `pid 4242 · epoch 7`, and the API's `127.0.0.1:<port>` |
| Profile | `profile.approved`, `live`, `changedSinceApproval`, `problem`, `source` | `approved` · `changed since approval` · `not approved` · `unreadable`, with the hash and file |
| Profile fix | `profile.needsApproval` (Sindri's verdict; the app doesn't derive it) | `sindri profile approve` when set |
| Services | `services[]` | `heartbeat · every 5s · last run 12:00:05Z` (`not run yet`) |
| Last event | the newest stream event | `service.heartbeat · 12:00:05Z · #42` |

State is always a word, never colour alone; each status text carries an `AutomationProperties.Name`. The look follows
`docs/sindri/DESIGN.md`: the near-black canvas, charcoal cards with hairline borders, Inter, and the lavender accent only
on the brand mark and the state pill.

## Launching

Bare `sindri` opens the app after it starts (or finds) the control plane: the `sindri-ui` binary next to `sindri`, or
the one `AW_SINDRI_UI` names, started detached (`exec nohup`, no pipes to the terminal). The summary's `ui` line says
what happened: `opened (sindri-ui, pid N)`, or `not found at <path>` with the fix. `sindri --headless` opens nothing and
prints no `ui` line. A relative `AW_SINDRI_UI` is resolved against the working directory before it is checked, and a file
without an execute bit reads `not executable at <path>` with the fix. Opening the app never changes `sindri`'s exit
code. A second `sindri-ui` hands over to the running one (`focus`) and exits, so a bare `sindri` while the app runs brings its window forward.

**`sindri` goes framework-dependent; `sindri-ui` stays NativeAOT** (csharp.md "AOT scope", README X54, engine spec
rev 8). The Temporal .NET SDK can't be trimmed, so the Sindri host publishes framework-dependent once its Temporal
adapter lands (the host project still sets `PublishAot` until then, and `src/Sindri/aot-smoke/run.sh` still runs).
Either way `sindri` is the binary in a publish directory (framework-dependent: an apphost beside its assemblies), and
`AppContext.BaseDirectory`, where the launcher looks for `sindri-ui`, is that directory. Copy the NativeAOT `sindri-ui` there together with its native libraries (`libSkiaSharp`, `libHarfBuzzSharp`,
`libAvaloniaNative`; without them it exits 2 with `SND-UI-004: TypeInitializationException`):
`bash src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh <dir>` copies all of them. Or point
`AW_SINDRI_UI` at it; under `dotnet run` the base directory is the host's build output, so set `AW_SINDRI_UI`. The app
depends on nothing else about how `sindri` is built: it talks to the control plane only through the HTTP API and two files under its home, `control.json` and
`auth/human.token`.

**`opened` is only "started".** `sindri` starts `sindri-ui` detached with its output to `/dev/null` and reports
`opened (sindri-ui, pid N)` as soon as the process starts, so the line can still claim success when that `sindri-ui`
then exits 1 (a running app that doesn't answer) or 2 (a failed start-up), and N is the launched process, not the
running app's. The UI package can't fix this: the line is `Aw.Sindri.Host`'s `UiLauncher`. Run `sindri-ui` directly to
see its exit code and standard error.

**A declared edge with a cycle exception (owner decision, 2026-10-12).** Starting `sindri-ui` from `sindri` is the
`Sindri -> SindriUi` cli edge in `deps.yaml`. It closes a cycle with the `SindriUi -> Sindri` http edge, so it carries a
`DEPS-CYCLE-001` exception that names that edge: the launch only spawns a separate process, shares no code or data,
and Sindri's HTTP API (with `control.json` and `auth/human.token`) stays the app's only contract. The edge is `status: planned` only because check-deps doesn't
scan C# for cli edges (a current edge its scan can't find fails `DEPS-STALE-001`).

## Errors

The app shows the first three as words in the window, never a dialog or a crash. A call Sindri refuses (a 403 with
its ProblemDetails, such as `SND-AUTH-001`, after the one retry) keeps Sindri's own code: the unreadable banner shows
that code and its detail as they are (API spec §3.6).

| Code | When |
|---|---|
| `SND-UI-001` | No usable endpoint record, or it names a process that isn't the one answering |
| `SND-UI-002` | Nothing answered at the recorded endpoint, or not within 2 s |
| `SND-UI-003` | The API answered, but not with the record this build reads (an error status other than a 403 with Sindri's ProblemDetails, or another body). The banner says it in words ("SND-UI-003: /sindri/status answered with something this build can't read", or "… answered 500"); a body's parser detail (record, JSON path, byte position) goes only to the log, as a `SND-UI-003` warning |
| `SND-UI-004` | The app's start-up, its following of the control plane, the launch before it (claiming the single instance, handing over) or ending the instance and the motion poll after it threw: logged (code and exception type) and the app exits 2. Not shown in a window: there may be none |

## Tests

| Lane | What |
|---|---|
| unit | The view models (the shell, Health, and Settings against the in-memory preferences and a fake OS setting) against the in-memory API, endpoint record and human token; the `ISindriApi` contract for the fake and for the real client over a canned handler that writes Sindri's wire format by hand; Sindri's API goldens (UI spec E-17: `tests/Aw.Sindri.Tests/Golden/Api/{status,ping,events}`, linked into the test output and read through `GoldenFiles`) deserialized through the app's JSON context, the handler's hand-written bodies held to their shape, and the SND-AUTH-001 refusal goldens served verbatim (one retry, then their code and detail; `Api/GoldenContractTests`); the control.json path; the theme (tokens, contrast, Fluent values, no hex outside `Theme/`); the port contracts over their fakes; preferences, deep links, labels |
| integration | The shell window under Avalonia's headless platform (Skia rendering), one key frame per pre-API state plus Health, the sheet, its edge tab and its covering form, saved as `frames/shell-*.png` under the test output (`artifacts/bin/Aw.Sindri.Ui.Tests/debug/frames/`); F6 (and ⌘⌥←/→ on macOS), Esc and ⌘,; the remembered bounds; Settings (frames `settings-motion-off`, `-on`, `settings-system-reduces-motion`, `settings-text-120`); `Motion.Apply` over a token dictionary; the theme as the app resolves it (palette, overrides, the embedded mono font, the text scale); the head's adapters against temp directories (the endpoint-record, human-token, preferences, single-instance and tray contracts, owner-only modes, a crashed instance's socket, the listener's checks), the OS motion readers read-only (GNOME over a stand-in gsettings, macOS against `defaults read com.apple.universalaccess reduceMotion`), and `AppLifetime` on a headless window |

`Avalonia.Headless.XUnit` 12.1.3 is compiled against xUnit v3 3.2.2 and fails discovery under the repo's xUnit v3 4.0.1
(`MissingMethodException` in `TestIntrospectionHelper.GetTestCaseDetails`). The view tests are plain xUnit v3 facts that
dispatch onto `HeadlessUnitTestSession.GetOrStartForAssembly(...)`, with the app builder in `[assembly:
AvaloniaTestApplication]`. Revisit when an `Avalonia.Headless.XUnit` built against xUnit v3 4 is 14 days old.

```bash
dotnet test --project tests/Aw.Sindri.Ui.Tests --filter-trait "Lane=unit"
dotnet test --project tests/Aw.Sindri.Ui.Tests --filter-trait "Lane=integration"
bash src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh [<dir>]   # NativeAOT sindri-ui on a copy (CI's AOT step); copies it to <dir> when given
```
