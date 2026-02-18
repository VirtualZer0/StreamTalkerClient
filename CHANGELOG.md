# Changelog

All notable changes to StreamTalkerClient will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.2] - 2026-02-18

### Fixed
- Server reconnection not reloading inference timeout and MaxVram settings (stale values after server restart)
- Warmup voice selection lost after server reconnection (AvailableVoices.Clear() wiped it without restoration)
- Default voice not falling back to first available voice when saved voice no longer exists on server after reconnection
- Language validation falling back to "Russian" instead of "Auto" for invalid/blank settings values
- "Arabic" listed as TTS language option despite server not supporting it (caused 400 errors)
- MaxBatchSize field initializer inconsistency (was 1, should be 2 to match AppSettings default)

### Changed
- Consolidated all duplicated option lists (TTS languages, attention modes, quantization, warmup, platforms, extraction modes) into single source of truth in AppConstants.Options
- Deduplicated culture-to-language mapping in AppSettings (was identical in TtsServerSettings and WarmupSettings)

## [1.0.1] - 2026-02-16

### Added
- User Rules window (renamed from Voice Bindings) with two tabs: Voice Bindings and Blacklist
- Blacklist tab to ignore specific users from TTS entirely
- Platform selector (Any / Twitch / VK Play) for voice bindings and blacklist entries
- Platform badges in DataGrid columns (purple for Twitch, blue for VK Play)
- Batch Size slider (1-6) in Inference Parameters section

### Fixed
- Auto-update not working on Windows (app closed but update script failed silently due to relative download path and child process termination)

### Changed
- "Voice Bindings" button renamed to "User Rules" (EN) / "Правила" (RU)
- Default batch size changed from 1 to 2
- Maximum batch size reduced from 50 to 6
- Release asset naming: self-contained builds now use `-sc` suffix for clarity

## [1.0.0] - 2026-02-08

### Added
- Initial release of StreamTalkerClient
- Cross-platform support for Windows 10/11 and Linux (X11)
- Twitch and VK Play Live chat integration
- Qwen TTS server connection with local Docker setup wizard
- Voice customization with per-user bindings
- WAV cache with LRU eviction and SHA256 keys
- Global hotkeys (NumPad5=skip, NumPad4=clear)
- Bilingual UI (English/Russian) with runtime switching
- Dark theme with Semi Avalonia icon library
- MVVM architecture with CommunityToolkit.Mvvm
- Audio playback using NetCoreAudio
- System-wide hotkeys via SharpHook
- Configurable message queue with per-voice management
- Batch synthesis with ZIP compression for network efficiency
- Disk-based cache with JSON index and LRU eviction
- Automated cache compression and cleanup
- GPU usage monitoring and model management
- Serilog logging with daily rotation (7-day retention)
- Persistent settings with auto-save every 30 seconds

[1.0.2]: https://github.com/VirtualZer0/StreamTalkerClient/releases/tag/v1.0.2
[1.0.1]: https://github.com/VirtualZer0/StreamTalkerClient/releases/tag/v1.0.1
[1.0.0]: https://github.com/VirtualZer0/StreamTalkerClient/releases/tag/v1.0.0
