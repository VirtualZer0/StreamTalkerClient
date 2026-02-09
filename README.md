# StreamTalkerClient

🌐 **Language:** English | [Русский](docs/ru/README.md)

**Desktop app for reading Twitch and VK Play chat messages aloud with AI voices**

---

## 📖 Description

StreamTalkerClient is a cross-platform desktop application that reads your Twitch and VK Play Live chat messages aloud using AI-generated voices. It connects to your streaming chat, puts incoming messages in a queue, sends them to a TTS (text-to-speech) server for voice synthesis, and plays the resulting audio through your speakers.

**How it works:** Connect to chat → Messages are queued → AI synthesizes speech → Audio plays

**Supported platforms:**
- Windows 10/11 (64-bit)
- Linux (X11 desktop environment)

---

## 🚀 Installation

### Step 1: Download the application

1. Go to the [Releases page](https://github.com/VirtualZer0/StreamTalkerClient/releases)
2. Download the archive for your platform:
   - **Windows:** `StreamTalkerClient-win-x64.zip`
   - **Linux:** `StreamTalkerClient-linux-x64.tar.gz`
3. Extract the archive to any folder

### Step 2: Install .NET Runtime

StreamTalkerClient requires the .NET 10 Runtime — a free component from Microsoft that is needed to run the application.

**Windows:**
1. Go to the [.NET 10 Runtime download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
2. Under **".NET Desktop Runtime"**, click the **x64** installer for Windows
3. Run the downloaded installer
4. Click **Install** → wait for it to finish → click **Close**
5. Restart your computer (recommended)

**Linux (Ubuntu/Debian):**
```bash
sudo apt update
sudo apt install dotnet-runtime-10.0
```

**Linux (Fedora):**
```bash
sudo dnf install dotnet-runtime-10.0
```

### Step 3: Set up the TTS Server

StreamTalkerClient needs a TTS server to synthesize speech. You have two options:

- **Run the server locally** — Requires an NVIDIA GPU with 8GB+ VRAM. See the [StreamTalkerServer installation guide](https://github.com/VirtualZer0/StreamTalkerServer#-installation)
- **Connect to a remote server** — If someone else is hosting the server, you just need its URL (e.g., `http://192.168.1.100:7860`)

### Step 4: First launch

1. Run `StreamTalkerClient.exe` (Windows) or `StreamTalkerClient` (Linux)
2. The setup wizard will guide you through:
   - **Server selection** — Choose local Docker server or enter a remote server URL
   - **Docker setup** (local only) — The app checks for Docker/WSL and downloads necessary files
   - **Resource configuration** (local only) — Set GPU memory limits and model options
   - **Health check** — Verifies the TTS server is running and accessible
   - **Voice loading** — Downloads the list of available voices from the server

### Step 5: Connect to chat

1. Select the platform tab (Twitch or VK Play)
2. Enter your channel name
3. Click **Connect**
4. Chat messages will start appearing in the queue and playing as audio!

---

## ❓ FAQ

<details>
<summary><b>App won't start or crashes on launch</b></summary>

Most likely the .NET 10 Runtime is not installed. Download and install it from the [.NET download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) — choose **".NET Desktop Runtime"** for Windows. Restart your computer after installation.
</details>

<details>
<summary><b>Can't connect to TTS server</b></summary>

- Make sure the TTS server is running (check `http://localhost:7860/health` in your browser)
- If using a remote server, verify the URL is correct and the server is accessible
- Check your firewall settings — port 7860 must be open
- Try restarting the TTS server
</details>

<details>
<summary><b>No sound playing</b></summary>

- Check your system volume and make sure the correct audio output device is selected
- Check the volume slider in the app (Queue panel)
- Try clearing the cache (Cache panel → Clear) and reconnecting
- On Linux, make sure `aplay` is installed (`sudo apt install alsa-utils`)
</details>

<details>
<summary><b>Hotkeys not working</b></summary>

- Make sure NumLock is enabled (hotkeys use NumPad keys: NumPad5 = skip, NumPad4 = clear)
- On Linux, the app requires X11 for global hotkeys — Wayland is not supported
- Try running the app with elevated permissions if hotkeys still don't work
</details>

<details>
<summary><b>Messages not appearing in the queue</b></summary>

- Check that you're connected to the correct channel name
- If `RequireVoice` is enabled in settings, messages must include a voice tag (e.g., `[voice_name] message text`)
- Enable `ReadAllMessages` in settings to process all chat messages regardless of voice tags
</details>

<details>
<summary><b>How to change the voice for messages</b></summary>

Two ways to select a voice:
- **Per-message:** Include the voice name in the message using bracket syntax: `[voice_name] Your text here`
- **Per-user binding:** Go to **Manage Voices** → **Bindings** tab, add a username and assign a voice. All messages from that user will use the bound voice.
</details>

<details>
<summary><b>High memory usage</b></summary>

The app caches synthesized audio on disk. If disk usage is too high:
- Lower the cache size limit in Settings
- Click **Compress** in the Cache panel to reduce size
- Click **Clear** to delete all cached files
</details>

---

## 🔗 Related Projects

### [StreamTalkerServer](https://github.com/VirtualZer0/StreamTalkerServer)

The AI text-to-speech server powered by Qwen3-TTS that synthesizes voice for StreamTalkerClient. Runs locally via Docker on an NVIDIA GPU or on a remote machine.

---

## Features

- **Multi-Platform Streaming** — Simultaneous support for Twitch and VK Play Live chat
- **Voice Customization** — Per-user voice bindings and custom synthesis parameters (speed, temperature, repetition penalty)
- **Smart Queueing** — Voice extraction from messages (`[voice] text` syntax or first-word mode), automatic cache-key deduplication
- **Local WAV Cache** — Disk-based cache with SHA256 keys, LRU eviction, and compression
- **First-Launch Wizard** — Guided setup for local Docker server or remote TTS connection
- **Global Hotkeys** — Skip current/all messages with NumPad keys (cross-platform via SharpHook)
- **Dark Theme** — Fluent design with Semi Avalonia icon library
- **Bilingual UI** — Runtime language switching between English and Russian

---

## Configuration

### First Launch Wizard
On first run, StreamTalkerClient guides you through server setup:

1. **Server Selection** — Choose between local Docker server or remote server
2. **Docker Setup** (local only) — Checks for Docker/WSL, downloads compose files
3. **Resource Configuration** (local only) — Set GPU memory limits and model optimizations
4. **Health Check** — Verifies TTS server connectivity
5. **Voice Loading** — Fetches available voices from the server

### Manual Configuration
Settings are stored in `data/settings.json` and can be edited manually:

```json
{
  "Services": {
    "Twitch": {
      "Channel": "your_channel_name",
      "ReadAllMessages": false,
      "RequireVoice": true
    }
  },
  "Voice": {
    "DefaultVoice": "female_calm",
    "VoiceExtractionMode": "bracket",
    "Speed": 1.0,
    "Temperature": 0.7
  },
  "Server": {
    "BaseUrl": "http://localhost:7860",
    "Language": "Russian"
  }
}
```

---

## Usage

### Connecting to Chat
1. Select platform tab (Twitch or VK Play)
2. Enter channel name
3. Click **Connect**
4. Load rewards (optional) to filter by channel point redemptions

### Voice Syntax
Two modes for extracting voice from messages:

**Bracket Mode (default):**
```
[voice_name] Text to synthesize
```

**First-Word Mode:**
```
voice_name Text to synthesize
```

### Voice Bindings
Assign specific voices to users:
1. Click **Manage Voices** → **Bindings** tab
2. Add username and select voice
3. User messages will automatically use the assigned voice

### Queue Controls
- **Volume Slider** — Global volume (0-100%) with per-voice overrides
- **Playback Delay** — Seconds between messages (default: 5s)
- **Skip Current** — NumPad5 or button click
- **Clear Queue** — NumPad4 or button click

---

## Project Structure

```
StreamTalkerClient/
├── src/
│   ├── ViewModels/          # MVVM view models (MainWindowViewModel orchestrates all services)
│   │   ├── MainWindowViewModel.cs
│   │   ├── MainWindowViewModel.Settings.cs
│   │   ├── VoiceBindingViewModel.cs
│   │   └── Wizard/          # First-launch wizard VMs
│   ├── Views/               # Avalonia AXAML views (thin UI shells)
│   │   ├── MainWindow.axaml
│   │   ├── PlatformTabsPanel.axaml
│   │   ├── VoiceSettingsPanel.axaml
│   │   ├── ModelControlPanel.axaml
│   │   ├── QueuePanel.axaml
│   │   ├── CachePanel.axaml
│   │   ├── SettingsPanel.axaml
│   │   └── Wizard/          # Wizard views
│   ├── Services/            # External I/O layer
│   │   ├── TwitchService.cs          # TwitchLib WebSocket client
│   │   ├── VKPlayService.cs          # VK Play custom WebSocket
│   │   ├── QwenTtsClient.cs          # HTTP TTS API client
│   │   ├── TtsConnectionManager.cs   # Health checks, auto-reconnect
│   │   ├── AudioPlayer.cs            # NetCoreAudio wrapper
│   │   └── GlobalHotkeyService.cs    # SharpHook system hotkeys
│   ├── Managers/            # Core business logic pipeline
│   │   ├── MessageQueueManager.cs    # Voice extraction, per-voice queues
│   │   ├── SynthesisOrchestrator.cs  # Synthesis loop + playback loop
│   │   ├── PlaybackController.cs     # Sequential playback, volume mixing
│   │   └── CacheManager.cs           # Disk WAV cache, LRU eviction
│   ├── Models/              # Data models
│   │   ├── AppSettings.cs            # Settings JSON schema
│   │   ├── QueuedMessage.cs          # Message state machine
│   │   └── VoiceInfo.cs              # TTS voice metadata
│   ├── Infrastructure/      # Cross-cutting concerns
│   │   ├── Constants.cs              # App-wide constants (no magic numbers)
│   │   ├── SettingsRepository.cs     # Settings load/save/validation
│   │   └── AppLoggerFactory.cs       # Serilog configuration
│   ├── Converters/          # Avalonia value converters
│   ├── Lang/                # i18n resource dictionaries
│   │   ├── en.axaml
│   │   └── ru.axaml
│   └── Styles/              # Shared AXAML styles
├── data/                    # Runtime data (auto-created)
│   ├── settings.json
│   ├── cache/
│   │   ├── index.json
│   │   └── *.wav
│   └── logs/
└── docs/                    # Documentation
    └── ru/
        └── README.md
```

### Key Architectural Layers
- **Views** — Thin AXAML shells that bind to ViewModels (no business logic)
- **ViewModels** — Orchestration layer (MainWindowViewModel owns all services/managers)
- **Services** — External I/O (streaming platforms, TTS server, audio playback)
- **Managers** — Core business logic (queueing, synthesis, playback, cache)
- **Models** — Data structures and settings schema

---

## Tech Stack

- **.NET 10** — Cross-platform runtime
- **Avalonia 11.3** — XAML-based UI framework with Fluent Dark theme
- **Semi.Avalonia** — Icon library and design components
- **CommunityToolkit.Mvvm** — Source generators for MVVM boilerplate
- **TwitchLib** — Twitch IRC/WebSocket client
- **NetCoreAudio** — Cross-platform audio playback (`aplay` on Linux)
- **SharpHook** — Global hotkeys via X11 on Linux, native hooks on Windows
- **Serilog** — Structured logging with daily file rotation

---

## Building from Source

```bash
# Clone the repository
git clone https://github.com/VirtualZer0/StreamTalkerClient.git
cd StreamTalkerClient

# Build the project
dotnet build src/StreamTalkerClient.csproj

# Run the application
dotnet run --project src/StreamTalkerClient.csproj

# Publish platform-specific executable
# Windows
dotnet publish src/StreamTalkerClient.csproj -c Release -r win-x64

# Linux
dotnet publish src/StreamTalkerClient.csproj -c Release -r linux-x64
```

---

## Contributing

Contributions are welcome! To contribute:

1. **Fork** the repository
2. **Create a feature branch** (`git checkout -b feature/your-feature`)
3. **Follow MVVM patterns** — Keep Views thin, logic in ViewModels/Services/Managers
4. **Test on both Windows and Linux** (if modifying platform-specific code)
5. **Submit a Pull Request** with a clear description of changes

### Code Style
- Use C# 12 features (file-scoped namespaces, record types, pattern matching)
- Follow existing architecture (Services → Managers → ViewModels → Views)
- Add XML doc comments for public APIs
- Use constants from `AppConstants` instead of magic numbers

---

## License

This project is licensed under the **MIT License**. See [LICENSE](LICENSE) for details.

---

## Acknowledgments

- **[Qwen3-TTS](https://github.com/QwenLM/Qwen3-TTS)** for the AI voice synthesis engine
- **[Qwen3-TTS-streaming](https://github.com/rekuenkdr/Qwen3-TTS-streaming)** for the streaming optimizations fork
- **Semi Design** for the icon library
- **Avalonia UI** community for the excellent cross-platform framework
