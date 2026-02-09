# Changelog

All notable changes to StreamTalkerClient will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[1.0.0]: https://github.com/VirtualZer0/StreamTalkerClient/releases/tag/v1.0.0
