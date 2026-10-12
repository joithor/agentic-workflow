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
  "Command layer and API": `GET /sindri/ping`, `GET /sindri/status` and `GET /sindri/events`) and the endpoint
  record it finds the API by (`control.json`, "Files"). `deps.yaml` declares them as the `SindriUi -> Sindri`
  **http** and **file** edges; `node scripts/check-deps.mjs` finds them in the app's C# projects (a literal starting
  with Sindri's route prefix `/sindri/`; the literals `"sindri-app"` and `"control.json"`) and fails on an undeclared
  or stale one. The head's path rule (`ControlFilePath`) follows the documented one in `docs/csharp/sindri.md` "Files".
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
| `ISingleInstance` (`TryClaim`, `Send`, `Messages`, `HolderProcessId`; `InstanceMessages.Focus`, `MaxLength` 512) | A local channel only the user can open, under `$AW_SINDRI_HOME/ui/`. A message only navigates: the app reads it as a `DeepLink` and ignores anything else. `Send` is true only once the running instance has **handled** the message (its reader came back for the next one), never because a lock is held: a holder that doesn't read gives no answer, and the message isn't handed over later | `SingleInstanceContract` |
| `ITray` (`Show(TrayView)`, `Shown`, `OpenRequested`, `QuitRequested`; `TrayState` Neutral, NotRunning, Stale) | Avalonia `TrayIcon`; menu labels escape `_` | `TrayContract` |
| `IPreferencesStore` (`Location`, `Read`/`Write` of the bytes, like the endpoint record) | `$AW_SINDRI_HOME/ui/preferences.json`, 0600, temp file and rename | `PreferencesStoreContract` |

A head hands its adapters to `SindriApp.Create(SindriPlatform, TimeProvider)` (`SindriPlatform` bundles them, so a
new port doesn't change the signature).

**The desktop adapters** (`Aw.Sindri.Ui.Desktop`, Unit B) keep the app's own files in a `ui/` directory of Sindri's home
(the directory `ControlFilePath` resolves; `UiStatePaths`), created 0700, every file in it 0600 (`OwnerOnly`; on
Windows the profile's ACLs do it):

| Adapter | How |
|---|---|
| `FilePreferencesStore` | `ui/preferences.json`; a write goes to a new 0600 temp file beside it, flushed to disk, then renamed over it |
| `SocketSingleInstance` | The claim is an exclusive lock on `ui/instance.lock` (the OS drops it when the process dies, so a crash leaves no claim); its holder records its process id in `ui/instance.pid` (0600; removed on exit), then removes a crashed instance's `ui/instance.sock` and listens there (a Unix domain socket, 0600; AF_UNIX on Windows 10 and later too; when the path is longer than 100 bytes (under sun_path's 104 on macOS and 108 on Linux), as in a deep temp home, the socket goes to `$TMPDIR/sindri-ui-<hash of the path>/`, 0700). A later launch writes one UTF-8 message ending in a newline, keeps its side open and reads one byte back: 1 once the holder's reader has handled the message (`Views/InstanceListener` reads from the app's framework start-up on: before the window shows it answers `focus` at once and keeps the latest deep link; after, `AppLifetime.Receive` handles each on the UI thread before the next is read), 0 refused. The listener refuses an empty message, one over 512 characters, one that isn't UTF-8 or one without its newline; it waits 1.5 s from a message's arrival for the handling and the sender waits 2 s, so a holder that doesn't handle it (an app whose framework start-up failed or hangs, or a wedged one) gives no answer. A message nothing handled in time, or whose sender has gone, is dropped |
| `SingleLaunch` | `Program` claims before Avalonia starts; a later launch sends `focus` (or its one argument when it is a deep link, `sindri://` optional) and exits 0 once the running app handled it. With no answer it claims again: if the holder ended meanwhile, this launch runs the app; otherwise it exits 1 with `sindri-ui: Sindri app is running but not answering (pid N): quit it or kill it` on standard error. Exit codes: 0 handed over (or the app quit), 1 a holder that doesn't answer, 2 the app failed (SND-UI-004) |
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
**The caller headers** `X-Sindri-Caller: human` and `X-Sindri-Client: ui` go on every request (§9.1), and the status
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
`SingleLaunch` then writes `sindri-ui: couldn't start (SND-UI-004); the log names the cause unless AW_LOG_LEVEL is none`
on standard error. `SingleLaunch` is also the head's boundary: anything that throws before or around the app (claiming the
instance when `ui/` can't be made, a hand-over socket it can't use) is logged the same way (`SND-UI-004`, the type
only, never the message, which can name the home), with a line that names the exception's type (`sindri-ui: couldn't start (SND-UI-004: IOException)`), and exit 2; never an unhandled exception.

| Part | What it shows |
|---|---|
| Top bar (48 px) | Brand; Home · Queue · Decisions · Services (counts empty until their routes); Later ▾ (Agents, Repos, Today, Automatic work, each "not served yet"); the ⌘K field, inert until U1; the health pill, which opens Health (while Sindri isn't running it is only the state: disabled like the navigation, its word undimmed, since the Not running card is the health state and Health has nothing to show). When they don't all fit (the 900 px minimum, text scale up to 1.3) the bar steps down in `TopBar.axaml.cs`: the ⌘K field folds to its magnifier, then the pill's age drops (the pill's accessible name keeps it), then the brand keeps its mark alone. The pill's word always shows |
| Home panels' bodies | "not served yet", centred in the panel |
| Home | The page title, three frames (Needs you, Running · With you, Queued), each "not served yet", and the Sindri health strip: the health line (§4.8) over the fields the status carries today (`Running · pid · epoch · API v1 · N services · profile <state>`; "Not running" when it isn't), which opens Health on a click or Enter |
| Queue, Decisions, Services, Settings | The title and "not served yet"; Settings is the slot `Views/Settings*` fills (⌘, opens it) |
| Health | S1b's status card (`HealthViewModel`: lease, API, profile, services, last event) |
| Detail sheet | A deep link's subject over the page at the right, 520 px (420–760, dragged by its left edge and remembered); Esc closes it and leaves an edge tab that reopens it; below 1100 px it covers the page with a Back button and focus moves to its heading |

The pre-API states (`ShellState`, highest first): **version mismatch** (the status's `apiVersion` differs from
`AppApiVersion`: a full-window panel with both versions and "quit both and run" around `sindri` in the command chip, no top bar), **not running**
(S1b's panel and reasons), **connecting** (skeleton rows, first load only), **unreadable** (`SND-UI-003`: a banner with
the code and `sindri doctor`; the page keeps its last data and the pill reads "Unreadable · stale"), **stale** (30 s
without an event, a status or an answered ping, on the injected clock: the pill reads "⌛ stale · updated Ns ago" with
**Refresh** beside it; otherwise "✓ Running · updated Ns ago", re-worded every second). Refresh follows §4.1's command
lifecycle: while it is in flight it is disabled with its label kept and a spinner in its icon's place (still under
reduced motion), "Sending…" is announced after 300 ms through a polite live region (not shown), and a second click does
nothing. After three failed refreshes the pill keeps "stale" and a banner under
the top bar reads "Sindri not responding: run `sindri doctor`" in full, since the sentence doesn't fit the pill at
900 px. The view models carry no Avalonia types; views bind the app's copy through `ShellText` (compiled bindings
resolve public members only, and `Labels` is internal). F6 / Shift+F6 cycle top bar, page and sheet; the window's
minimum is 900 × 600.

**Focus never goes nowhere (§11.2).** When the focused control disappears, focus moves to a neighbour: Refresh keeps
focus through its in-flight phase and gets it back when the refresh fails; when Sindri answers and Refresh hides, the
health pill gets it. Closing the sheet returns focus to the edge tab when the tab reopened it, otherwise (a deep link)
to the page region. A state change that hides the focused control moves focus to its region's neighbour: the pill in
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
"Refresh"; it is zero-width and transparent, so it takes no room and shows nothing. If the pass finds it silent, the fallback is a head adapter posting
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

## How it finds the control plane

1. The endpoint-record port (`IEndpointRecordSource`) reads `control.json`. The desktop adapter reads it from
   `AW_SINDRI_HOME`, else `$AW_STATE_DIR/sindri-app`, else `~/.agentic-workflow/sindri-app`, the same place the control
   plane writes it. No record means "Not running". A record whose URL isn't on this machine (loopback: 127.0.0.1, ::1
   or localhost) is refused as unreadable, with the reason and the record's path; the app never calls that address.
2. `GET /sindri/ping` must answer the record's pid **and** epoch; otherwise the record is stale and reads as "Not running".
3. `GET /sindri/status` fills the window. A lower epoch than the last one means a new ledger, so the stream cursor
   starts over. Each `/sindri/events` event (server-sent events, resumed from the last
   sequence seen) triggers one status refresh; a burst of events triggers one. When the stream ends (the control
   plane stopped or was killed), a status refresh fails, or `control.json` changes (checked every 2 s; a stopping
   control plane can hold its stream open), the app looks again, every 2 s while nothing answers. A record the
   platform can't read, or one with a relative `url`, reads as "can't be read" with the reason, and the app keeps trying.
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
depends on nothing else about how `sindri` is built: it talks to the control plane only through `control.json` and the
HTTP API.

**`opened` is only "started".** `sindri` starts `sindri-ui` detached with its output to `/dev/null` and reports
`opened (sindri-ui, pid N)` as soon as the process starts, so the line can still claim success when that `sindri-ui`
then exits 1 (a running app that doesn't answer) or 2 (a failed start-up), and N is the launched process, not the
running app's. The UI package can't fix this: the line is `Aw.Sindri.Host`'s `UiLauncher`. Run `sindri-ui` directly to
see its exit code and standard error.

**A declared edge with a cycle exception (owner decision, 2026-10-12).** Starting `sindri-ui` from `sindri` is the
`Sindri -> SindriUi` cli edge in `deps.yaml`. It closes a cycle with the `SindriUi -> Sindri` http edge, so it carries a
`DEPS-CYCLE-001` exception that names that edge: the launch only spawns a separate process, shares no code or data,
and Sindri's HTTP API stays the app's only contract. The edge is `status: planned` only because check-deps doesn't
scan C# for cli edges (a current edge its scan can't find fails `DEPS-STALE-001`).

## Errors

The app shows the first three as words in the window, never a dialog or a crash.

| Code | When |
|---|---|
| `SND-UI-001` | No usable endpoint record, or it names a process that isn't the one answering |
| `SND-UI-002` | Nothing answered at the recorded endpoint, or not within 2 s |
| `SND-UI-003` | The API answered, but not with the record this build reads (an error status, or another body) |
| `SND-UI-004` | The app's start-up, its following of the control plane, or the launch before it (claiming the single instance, handing over) threw: logged (code and exception type) and the app exits 2. Not shown in a window: there may be none |

## Tests

| Lane | What |
|---|---|
| unit | The view models (the shell, Health, and Settings against the in-memory preferences and a fake OS setting) against the in-memory API and endpoint record; the `ISindriApi` contract for the fake and for the real client over a canned handler that writes Sindri's wire format by hand; the control.json path; the theme (tokens, contrast, Fluent values, no hex outside `Theme/`); the port contracts over their fakes; preferences, deep links, labels |
| integration | The shell window under Avalonia's headless platform (Skia rendering), one key frame per pre-API state plus Health, the sheet, its edge tab and its covering form, saved as `frames/shell-*.png` under the test output (`artifacts/bin/Aw.Sindri.Ui.Tests/debug/frames/`); F6, Esc and ⌘,; the remembered bounds; Settings (frames `settings-motion-off`, `-on`, `settings-system-reduces-motion`, `settings-text-120`); `Motion.Apply` over a token dictionary; the theme as the app resolves it (palette, overrides, the embedded mono font, the text scale); the head's adapters against temp directories (the endpoint-record, preferences, single-instance and tray contracts, owner-only modes, a crashed instance's socket, the listener's checks), the OS motion readers read-only (GNOME over a stand-in gsettings, macOS against `defaults read com.apple.universalaccess reduceMotion`), and `AppLifetime` on a headless window |

`Avalonia.Headless.XUnit` 12.1.3 is compiled against xUnit v3 3.2.2 and fails discovery under the repo's xUnit v3 4.0.1
(`MissingMethodException` in `TestIntrospectionHelper.GetTestCaseDetails`). The view tests are plain xUnit v3 facts that
dispatch onto `HeadlessUnitTestSession.GetOrStartForAssembly(...)`, with the app builder in `[assembly:
AvaloniaTestApplication]`. Revisit when an `Avalonia.Headless.XUnit` built against xUnit v3 4 is 14 days old.

```bash
dotnet test --project tests/Aw.Sindri.Ui.Tests --filter-trait "Lane=unit"
dotnet test --project tests/Aw.Sindri.Ui.Tests --filter-trait "Lane=integration"
bash src/Sindri/Aw.Sindri.Ui.AotSmoke/run.sh [<dir>]   # NativeAOT sindri-ui on a copy (CI's AOT step); copies it to <dir> when given
```
