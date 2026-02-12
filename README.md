# EOS SDK for Unity

Lightweight Unity package wrapping the Epic Online Services (EOS) C# SDK v1.18.1.2. No PlayEveryWare dependency — raw SDK access with thin convenience wrappers.

## Features

- EOS SDK v1.18.1.2 with platform binaries (Windows, macOS, Linux, Android, iOS)
- One-click setup: drop EOSManager, everything auto-creates
- DeviceID authentication (no Epic Account required)
- Lobby management (create, join, search, member attributes)
- Lobby text chat via member attributes (survives host migration)
- Voice chat (lobby-based RTC)
- Player registry (PUID to display name cache)
- Canvas-based runtime UI (Status, Lobbies, Voice, Friends tabs)
- Runtime debug console
- Android build processor (Gradle, desugaring, AndroidX)
- Setup wizard with sample credentials

## Install

Unity Package Manager > Add package from git URL:

```
https://github.com/TrentSterling/eos-sdk.git
```

## Quick Start

1. Install the package
2. Add an empty GameObject to your scene
3. Add the `EOSManager` component
4. Hit Play — SDK initializes, logs in with DeviceID, all managers auto-create

Use the Setup Wizard (`Tools > EOS Native > Setup Wizard`) to configure credentials. Click "Fill Sample Credentials" for quick testing with PlayEveryWare's public demo keys.

## Requirements

- Unity 6000.0+
- Input System package (optional, for mobile UI)

## Author

Trent Sterling — [tront.xyz](https://tront.xyz)
