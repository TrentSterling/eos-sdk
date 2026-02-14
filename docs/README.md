# EOS SDK for Unity

**v1.4.6** -- Lightweight Unity wrapper for the Epic Online Services (EOS) C# SDK v1.18.1.2.

No PlayEveryWare dependency. No bloated plugin manager. Just the raw EOS SDK with thin convenience wrappers that get out of your way.

## Features

- **SDK Initialization** -- Auto-initializes EOS platform with your credentials on Play
- **DeviceID Authentication** -- Automatic login with no user interaction required (no Epic Account needed)
- **Lobby Management** -- Create, join, search, and leave lobbies with numeric join codes
- **Voice Chat** -- Lobby-based RTC voice with mute, per-participant volume, and spatial audio support
- **Player Registry** -- Persistent PUID-to-display-name cache with local friends/block lists
- **Android Build Tools** -- Gradle template generator, build validator, and automatic desugaring for Quest/Android
- **Setup Wizard** -- One-click configuration with sample credentials for quick testing
- **Platform Support** -- Windows, macOS, Linux, Android, iOS, Meta Quest

## Quick Install

Unity Package Manager > Add package from git URL:

```
https://github.com/TrentSterling/eos-sdk.git
```

Or add directly to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.tront.eos-sdk": "https://github.com/TrentSterling/eos-sdk.git"
  }
}
```

## Quick Start

1. Install the package
2. Add an empty GameObject to your scene
3. Add the **EOSManager** component
4. Hit Play

That's it. The SDK initializes, logs in with a DeviceID token, and all managers (lobby, voice, player registry) auto-create as singletons.

Use the Setup Wizard (`Tools > EOS SDK > Setup Wizard`) to configure your own EOS Developer Portal credentials. Click **"Fill Sample Credentials"** for quick testing with public demo keys.

## What's New in v1.4.6

- **Gradle Template Generator** -- one-click fix for the notorious `FakeDependency.jar` build error on Android
- **Build Processor Robustness** -- compileOptions fallback, post-injection verification
- **Pre-Build Validator** -- automatic warnings about Android config issues before you build
- **Version Checker** -- `Tools > EOS SDK > Check for Updates`

## Requirements

- Unity 6000.0+
- Input System package (optional, for mobile Canvas UI)

## Architecture

```
EOSManager (MonoBehaviour singleton)
  |-- EOSConfig (ScriptableObject -- your EOS credentials)
  |-- EOSLobbyManager (auto-created -- lobby CRUD + join codes)
  |-- EOSVoiceManager (auto-created -- RTC voice chat)
  |-- EOSPlayerRegistry (auto-created -- player name cache)
```

All managers are singletons that auto-create as children of EOSManager. You never need to manually add them to your scene.

## Author

Trent Sterling -- [tront.xyz](https://tront.xyz)
