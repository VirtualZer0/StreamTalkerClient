# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build
dotnet build src/StreamTalkerClient.csproj

# Run (debug)
dotnet run --project src/StreamTalkerClient.csproj

# Publish (Windows)
dotnet publish src/StreamTalkerClient.csproj -c Release -r win-x64

# Publish (Linux)
dotnet publish src/StreamTalkerClient.csproj -c Release -r linux-x64
```

No test project exists. There is no linter configured.

## What This App Does

StreamTalkerClient is a cross-platform AvaloniaUI desktop app that reads Twitch and VK Play Live chat messages aloud using a Qwen TTS server. It was migrated from a Windows Forms app (TwitchToQwenTTSBridge). Chat messages are queued, synthesized in batches via an HTTP TTS API, cached to disk, and played back sequentially.

## Architecture

**Stack**: .NET 10, Avalonia 11.3 (Fluent Dark theme), CommunityToolkit.Mvvm, Serilog

**MVVM pattern** — all source code lives in `src/`:

- **ViewModels/** — `MainWindowViewModel` is the primary orchestrator. It owns all services/managers, handles events, and drives timers (UI update 250ms, auto-save 30s, GPU poll 5s). Two dialog VMs: `VoiceBindingViewModel`, `VoiceManagementViewModel`.
- **Views/** — Avalonia AXAML. `MainWindow` is a thin shell (status bar + ScrollViewer) that composes 6 UserControl panels, each in its own file. All panels share `MainWindowViewModel` as DataContext (inherited from the parent Window). Two modal dialogs for voice management.
  - `PlatformTabsPanel` — Twitch / VK Play connection tabs
  - `VoiceSettingsPanel` — voice selection, syntax mode, manage/bindings dialogs (has code-behind for opening modal dialogs via `TopLevel.GetTopLevel(this)`)
  - `ModelControlPanel` — model load/unload, attention, quantization, GPU usage
  - `QueuePanel` — message queue list, skip/clear, volume, timeout
  - `CachePanel` — cache size, progress bar, compress/clear
  - `SettingsPanel` — TTS server URL, delay, cache limit, language selector
  - Shared resources (converters, `group-box` style) live in `App.axaml`, not in individual views
- **Services/** — External I/O layer:
  - `TwitchService` / `VKPlayService` — implement `IStreamingService` for unified chat message/reward events
  - `QwenTtsClient` — HTTP client to TTS server (batch synthesis returns ZIP of WAVs, model load/unload, GPU usage)
  - `TtsConnectionManager` — health-checks TTS server every 5s, auto-reloads voices/models on reconnect
  - `AudioPlayer` — wraps NetCoreAudio, writes temp WAV files for playback
  - `GlobalHotkeyService` — system-wide hotkeys via SharpHook (NumPad5=skip, NumPad4=clear)
- **Managers/** — Core business logic pipeline:
  - `MessageQueueManager` — voice extraction (bracket `[voice] text` or firstword mode), per-voice ConcurrentQueues, cache-key deduplication
  - `SynthesisOrchestrator` — runs synthesis loop (100ms) and playback loop (100ms), batches messages, checks cache before synthesizing
  - `PlaybackController` — sequential playback with configurable delay, volume mixing (per-voice × global), cache pinning during playback
  - `CacheManager` — disk-based WAV cache with SHA256 keys, JSON index, LRU eviction at 85% capacity, pinning to protect playing files
- **Models/** — `AppSettings` (JSON serialized to `data/settings.json`), `QueuedMessage` (state machine: Queued→Synthesizing→WaitingForCache→Ready→Playing→Done), platform abstractions (`IStreamMessage`, `IStreamReward`), VK Play API models
- **Infrastructure/** — `Constants`, `DebouncedTimer` (prevents overlapping async executions), `AsyncExtensions` (retry with backoff), `AppLoggerFactory` (Serilog → file + console)
- **Converters/** — Avalonia value converters for bool/connection-state → color
- **Lang/** — `en.axaml` and `ru.axaml` ResourceDictionaries for runtime language switching via `DynamicResource`

## Data Flow: Chat → Audio

1. `TwitchService`/`VKPlayService` fires `OnMessage`/`OnReward` event
2. `MainWindowViewModel` applies filters (ReadAllMessages, RequireVoice, RewardId, voice bindings) then calls `MessageQueueManager.AddMessage()`
3. `MessageQueueManager` extracts voice from text, validates against server voices, creates `QueuedMessage` with cache key, enqueues to per-voice queue
4. `SynthesisOrchestrator` synthesis loop: pulls batch → checks `CacheManager` → calls `QwenTtsClient.SynthesizeBatchAsync()` → stores WAVs in cache → marks Ready
5. `SynthesisOrchestrator` playback loop: calls `PlaybackController.TryPlayNextAsync()` → applies delay → pins cache → plays via `AudioPlayer` → unpins → marks Done

## Key Patterns

- **No DI container** — services are constructed manually in `MainWindowViewModel`
- **UI thread marshaling** — use `Dispatcher.UIThread.Post()` (replaces WinForms `Invoke`)
- **Event-driven** — services/managers communicate via C# events; the ViewModel subscribes
- **Thread safety** — `ConcurrentQueue` for message queues, internal locks on `QueuedMessage` state transitions
- **i18n** — language switching works at runtime by swapping ResourceDictionary in `App.SetLanguage()`. All UI strings use `{DynamicResource Key}`
- **Cross-platform target** — `net10.0` (not `net10.0-windows`). Audio uses NetCoreAudio (`aplay` on Linux), hotkeys use SharpHook (X11 on Linux)

## Runtime Data

All runtime data goes to `data/` relative to the executable:
- `data/settings.json` — app settings
- `data/cache/` — synthesized WAV files + `index.json`
- `data/logs/app-{date}.log` — Serilog daily logs (7-day retention)

## UI Resources

**Semi Avalonia Icons** — Uses Semi Design icon library from [irihitech/Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia)

Icon resource files location in Semi.Avalonia repository:
- `src/Semi.Avalonia/Icons/FillIcons.axaml` — Filled icon geometries
- `src/Semi.Avalonia/Icons/StrokedIcons.axaml` — Stroked/outline icon geometries
- `src/Semi.Avalonia/Icons/AIIcons.axaml` — AI-themed icons

Window control icons (used in `CustomTitleBar` component):
- **Minimize**: `SemiIconMinus` (from FillIcons.axaml)
- **Maximize**: `SemiIconMaximize` (from FillIcons.axaml)
- **Restore**: `SemiIconShrink` (from FillIcons.axaml)
- **Close**: `SemiIconClose` (from FillIcons.axaml)

Reference icons via `{StaticResource SemiIcon[Name]}` in XAML (e.g., `Data="{StaticResource SemiIconClose}"`)

To browse all available icons, see the demo app at: `demo/Semi.Avalonia.Demo/Pages/IconDemo.axaml`
