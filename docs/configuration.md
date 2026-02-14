# Configuration

## Creating an EOSConfig Asset

The EOSConfig ScriptableObject stores your EOS Developer Portal credentials.

1. In the Project window, right-click
2. Select **Assets > Create > EOS Native > Config**
3. Name the asset (e.g., `MyEOSConfig`)
4. Fill in your credentials from the [EOS Developer Portal](https://dev.epicgames.com/portal)

## Required Fields

All credentials come from your product's settings in the EOS Developer Portal.

| Field | Description | Where to Find |
|---|---|---|
| **Product Name** | Your game's name | Product Settings |
| **Product ID** | Unique product identifier | Product Settings > Product ID |
| **Sandbox ID** | Environment identifier (Dev, Stage, Live) | Product Settings > Sandboxes |
| **Deployment ID** | Deployment within a sandbox | Product Settings > Deployments |
| **Client ID** | Client credential ID | Product Settings > Clients |
| **Client Secret** | Client credential secret | Product Settings > Clients |

> **Important:** When creating Client credentials in the portal, select **"GameClient"** as the client policy type. The DeviceID login flow requires this policy.

## Optional Fields

| Field | Default | Description |
|---|---|---|
| **Encryption Key** | *(empty)* | 64-character hex string (32 bytes) for P2P and storage encryption. Required if using P2P transport. Leave empty to skip. |
| **Default Display Name** | `"Player"` | Display name for DeviceID login (max 32 characters). ParrelSync clones auto-append a suffix. |
| **Is Server** | `false` | Enable for dedicated server builds. |
| **Tick Budget** | `0` | Milliseconds per tick for SDK processing. 0 = process all pending work. |

## Setup Wizard

The fastest way to get started:

1. Go to **Tools > EOS SDK > Setup Wizard**
2. Click **"Fill Sample Credentials"** to auto-fill with public demo keys for testing
3. The wizard creates an EOSConfig asset and assigns it to the EOSManager in your scene

The sample credentials use PlayEveryWare's public demo product, which is suitable for development and testing. Replace them with your own credentials before shipping.

## Scene Setup

1. Create an empty GameObject in your scene (or use an existing one)
2. Add the **EOSManager** component
3. Assign your EOSConfig asset to the **Config** field
4. That's it -- all other managers auto-create at runtime

### EOSManager Inspector Settings

| Setting | Default | Description |
|---|---|---|
| **Auto Initialize** | `true` | Automatically initialize the EOS SDK on Start |
| **Auto Login** | `true` | Automatically login with DeviceID after initialization |
| **Display Name** | `"Player"` | Display name for the local user |
| **Overlay Mode** | `Auto` | Status overlay: Auto (Canvas on mobile, OnGUI on desktop), Both, or None |
| **Show Console** | `true` | Show Canvas-based runtime console (captures Debug.Log on mobile) |
| **Enable Health Check UI** | `true` | Auto-create debug overlay (toggle with F11 at runtime) |

## Login Flow

EOSManager handles authentication automatically:

1. **SDK Init** -- Platform binaries are loaded and the EOS SDK is initialized with your config
2. **DeviceID Login** -- A device-specific token is created (or reused) for passwordless auth
3. **Connect Login** -- The device token is used to get a ProductUserId (PUID)
4. **Ready** -- `EOSManager.IsLoggedIn` becomes `true`, `OnLoginSuccess` fires

No user interaction is needed. The same device always gets the same PUID, so player identity persists across sessions.

### Events

```csharp
EOSManager.Instance.OnInitialized += () => {
    Debug.Log("SDK initialized!");
};

EOSManager.Instance.OnLoginSuccess += (ProductUserId puid) => {
    Debug.Log($"Logged in as {puid}");
};

EOSManager.Instance.OnLoginFailed += (Result error) => {
    Debug.LogError($"Login failed: {error}");
};
```

## Verify Setup

After entering Play Mode, check your Console for:

```
[EOS-Native] SDK initialized successfully
[EOS-Native] Login successful - PUID: 000abcdef1234567890...
```

If you see these messages, the SDK is configured correctly and ready to use.
