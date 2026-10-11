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
| Fluent | `Theme/FluentOverrides.axaml` | The §14.4 dark palette, button and tooltip brushes, `ControlCornerRadius` = `Radius.MD`, and the §11.1 focus ring: 2 px `PrimaryHover` at full opacity, 2 px outside the control. No shadows |
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
| `ISingleInstance` (`TryClaim`, `Send`, `Messages`; `InstanceMessages.Focus`, `MaxLength` 512) | A local channel only the user can open, under `$AW_SINDRI_HOME/ui/`. A message only navigates: the app reads it as a `DeepLink` and ignores anything else | `SingleInstanceContract` |
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
| `SocketSingleInstance` | The claim is an exclusive lock on `ui/instance.lock` (the OS drops it when the process dies, so a crash leaves no claim); its holder removes a crashed instance's `ui/instance.sock` and listens there (a Unix domain socket, 0600; AF_UNIX on Windows 10 and later too; when the path won't fit sun_path's 104 bytes, as in a deep temp home, the socket goes to `$TMPDIR/sindri-ui-<hash of the path>/`, 0700). A later launch writes one UTF-8 message and reads one byte back (1 taken, 0 refused); the listener refuses an empty message, one over 512 characters or one that isn't UTF-8, and gives a sender 2 s |
| `SingleLaunch` | `Program` claims before Avalonia starts; a later launch sends `focus` (or its one argument when it is a deep link, `sindri://` optional) and exits 0, or 1 when the holder doesn't answer |
| `AvaloniaTray` | Avalonia's `TrayIcon`, created on the first `Show` and registered with the app; `TrayMarks` draws Sindri's mark in the theme's Ink, filled (Neutral), hollow (Stale) and hollow struck through (NotRunning), as macOS template images; menu labels double each `_` |
| `PolledMotionPreference` | The OS reader at start and every 2 s on the injected `TimeProvider`: macOS `NSWorkspace.accessibilityDisplayShouldReduceMotion` through `objc_msgSend` (`MacMotion`), Windows `SPI_GETCLIENTAREAANIMATION` (`WindowsMotion`), GNOME `gsettings get org.gnome.desktop.interface enable-animations` (`GnomeMotion`; no gsettings, a failure or a hang past 1 s reads as off) |

`Views/AppLifetime` (in the app, so every desktop head shares it) is the window's life around these: closing the
window hides it (the app stays in the tray; an OS or app shutdown still closes it), the tray's Open brings it
forward and Quit ends the app (`ShutdownMode.OnExplicitShutdown`), the tray follows `ShellState` (Normal: Neutral,
"Sindri · running"; NotRunning: NotRunning; every other state: Stale, "Sindri not responding"), and an instance message
is either `focus` (bring forward) or a `DeepLink` (bring forward, then the shell's `Navigate`); anything else is ignored,
and no message runs an action. `SindriApp`'s `ShellOpened` (`Views/SindriApp.Lifetime.cs`) calls
`AppLifetime.Start(desktop, window, platform, shell)` once the shell window shows, and disposes it on exit.

**Preferences** (§12.3) are `UiPreferences`: `version` (1), `window` (x, y, width, height), `sheetWidth`, `lastPage`,
`seenIds`, `paletteHistory`, `textScale`, `reduceMotion`, camelCase JSON. A head's store only moves bytes; the app's
`ViewModels/Preferences` (`Load`, `Save`) parses and writes them through `Api/PreferencesJson`, so clamping is the same
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
gone.

| Part | What it shows |
|---|---|
| Top bar (48 px) | Brand; Home · Queue · Decisions · Services (counts empty until their routes); Later ▾ (Agents, Repos, Today, Automatic work, each "not served yet"); the ⌘K field, inert until U1; the health pill, which opens Health |
| Home | The page title and four frames (Needs you, Running · With you, Queued, Sindri health), each "not served yet" |
| Queue, Decisions, Services, Settings | The title and "not served yet"; Settings is the slot `Views/Settings*` fills (⌘, opens it) |
| Health | S1b's status card (`HealthViewModel`: lease, API, profile, services, last event) |
| Detail sheet | A deep link's subject over the page at the right, 520 px (420–760, dragged by its left edge and remembered); Esc closes it and leaves an edge tab that reopens it; below 1100 px it covers the page with a Back button and focus moves to its heading |

The pre-API states (`ShellState`, highest first): **version mismatch** (the status's `apiVersion` differs from
`AppApiVersion`: a full-window panel with both versions and "quit both and run sindri", no top bar), **not running**
(S1b's panel and reasons), **connecting** (skeleton rows, first load only), **unreadable** (`SND-UI-003`: a banner with
the code and `sindri doctor`; the page keeps its last data and the pill reads "Unreadable · stale"), **stale** (30 s
without an event or a status, on the injected clock: the pill reads "Running · stale"; otherwise "Running · updated Ns
ago", re-worded every second). The view models carry no Avalonia types; views bind the app's copy through
`ShellText` (compiled bindings resolve public members only, and `Labels` is internal). F6 / Shift+F6 cycle top bar,
page and sheet; the window's minimum is 900 × 600.

### Settings and reduced motion (§3.3 ⌘,, §11.4, §14.2)

`Views/SettingsView` over `ViewModels/Settings/SettingsViewModel`: the in-app **Reduce motion** switch and **Text size**
(a slider, 90–130 % in 10 % steps, clamped by `UiPreferences.Clamp`). Both are kept in the preferences: a change is
written over whatever else the file holds (read, replace the two fields, write; one write after another), and a write
the platform refuses leaves the change applied for the session. Motion is reduced when the OS asks
(`IMotionPreference`, re-read on `Changed`, posted back to the UI thread) **or** the switch is on; while the OS asks, the
page says "On in your system settings" and the switch can't turn it off.

The theme is changed in one place each: `Theme/Motion.Apply(resources, reduce)` sets every `Motion.*` duration to 0, or
back to its token value, and `Theme/TextScale.Apply` scales the ramp. `Views/SettingsTheme.For(resources)` is the view
model's apply callback over both. **Shell hook:** the shell builds
`new SettingsViewModel(new Preferences(platform.Preferences), platform.Motion, SettingsTheme.For(Application.Current.Resources))`
on the UI thread, awaits `Load` once at start-up (it applies the stored scale and motion before the first frame), shows
`new SettingsView { DataContext = settings }` as its Settings page, and disposes it on exit. Stock Fluent controls'
own transitions don't read the `Motion.*` tokens: Settings' switch drops its knob transition while motion is reduced
(a view style on `IsMotionReduced`); other stock controls with transitions need the same when a page uses them.

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
window (popups aren't `AvnWindow`s). If the pass finds it silent, the fallback is a head adapter posting
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
   plane writes it. No record means "Not running".
2. `GET /sindri/ping` must answer the record's pid **and** epoch; otherwise the record is stale and reads as "Not running".
3. `GET /sindri/status` fills the window. A lower epoch than the last one means a new ledger, so the stream cursor
   starts over. Each `/sindri/events` event (server-sent events, resumed from the last
   sequence seen) triggers one status refresh; a burst of events triggers one. When the stream ends (the control
   plane stopped or was killed), a status refresh fails, or `control.json` changes (checked every 2 s; a stopping
   control plane can hold its stream open), the app looks again, every 2 s while nothing answers. A record the
   platform can't read, or one with a relative `url`, reads as "can't be read" with the reason, and the app keeps trying.

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

**Not a declared edge (owner question, PR #168).** Starting `sindri-ui` from `sindri` is a `Sindri -> SindriUi` cli
dependency by `deps.yaml`'s definition, but declaring it would close a cycle with the `SindriUi -> Sindri` http edge
(`DEPS-CYCLE-001`), and check-deps doesn't scan C# for cli edges. The two ways out are to invert the launch (the app
starts `sindri --headless` itself) or to record a reasoned exception for this one cycle; the owner decides. Until then
the launch is named in the `SindriUi` note in `deps.yaml`.

## Errors

The app shows each as words in the window, never a dialog or a crash.

| Code | When |
|---|---|
| `SND-UI-001` | No usable endpoint record, or it names a process that isn't the one answering |
| `SND-UI-002` | Nothing answered at the recorded endpoint, or not within 2 s |
| `SND-UI-003` | The API answered, but not with the record this build reads (an error status, or another body) |

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
