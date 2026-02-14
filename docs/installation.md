# Installation

## UPM Git URL (Recommended)

1. Open Unity
2. Go to **Window > Package Manager**
3. Click the **+** button in the top-left corner
4. Select **"Add package from git URL..."**
5. Paste:

```
https://github.com/TrentSterling/eos-sdk.git
```

6. Click **Add**

## manifest.json

Alternatively, add the package directly to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.tront.eos-sdk": "https://github.com/TrentSterling/eos-sdk.git"
  }
}
```

Save the file and Unity will automatically download and import the package.

## Local Development

If you have the package locally (e.g., for development or modification), use a local file path:

```json
{
  "dependencies": {
    "com.tront.eos-sdk": "file:../../path/to/com.tront.eos-sdk"
  }
}
```

## Requirements

| Requirement | Version |
|---|---|
| Unity | 6000.0+ |
| EOS C# SDK | v1.18.1.2 (bundled) |
| Input System | Optional (for mobile Canvas UI) |

The EOS SDK native binaries for all supported platforms (Windows, macOS, Linux, Android, iOS) are bundled with the package. No additional downloads are needed.

## Verify Installation

After installing, check your Console for messages from the package:

1. Add an **EOSManager** component to any GameObject in your scene
2. Enter Play Mode
3. You should see in the Console:

```
[EOS-Native] SDK initialized successfully
[EOS-Native] Login successful - PUID: 000...
```

If you see errors instead, check the [Troubleshooting](troubleshooting.md) page.

## Optional: FishNet EOS Transport

If you are using FishNet for multiplayer networking, you can also install the companion transport package:

```
https://github.com/TrentSterling/fishnet-eos-transport.git
```

The transport package depends on this EOS SDK package and will install it automatically if not already present.
