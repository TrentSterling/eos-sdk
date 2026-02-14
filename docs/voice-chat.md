# Voice Chat

EOSVoiceManager provides lobby-based RTC (Real-Time Communication) voice chat. Voice rooms are tied to EOS lobbies, which means voice persists through host migration and does not require any separate networking setup.

## How It Works

- When a lobby is created or joined with `EnableVoice = true` (the default), all members automatically join a voice room
- Voice is managed by EOS servers, not peer-to-peer -- it works regardless of NAT or firewall configuration
- The voice room lifetime is tied to the lobby lifetime

## Setup

EOSVoiceManager auto-creates as a singleton. No manual setup is needed.

When you create or join a lobby with voice enabled (the default), the voice room connects automatically. Check the Console for:

```
[EOSVoice] RTC Room connected
[EOSVoice] Auto-unmute result: Success, AudioStatus=Enabled
```

## Mute / Unmute

```csharp
var voice = EOSVoiceManager.Instance;

// Mute local microphone
voice.SetMuted(true);

// Unmute
voice.SetMuted(false);

// Toggle
voice.ToggleMute();
```

## Check Connection State

```csharp
var voice = EOSVoiceManager.Instance;

bool connected = voice.IsConnected;    // Connected to voice room
bool muted = voice.IsMuted;            // Local mic muted
bool enabled = voice.IsVoiceEnabled;   // Voice room exists for current lobby
string room = voice.CurrentRoomName;   // RTC room name
```

## Per-Participant Controls

### Volume

```csharp
// Set volume for a specific player (0-100, 50 = normal)
voice.SetParticipantVolume(playerPuid, 75f);

// Mute a specific player locally (only affects your playback)
voice.SetParticipantMuted(playerPuid, true);
```

### Speaking Detection

```csharp
// Check if a specific player is speaking
bool speaking = voice.IsSpeaking(playerPuid);

// Get all currently speaking participants
List<string> speakers = voice.GetSpeakingParticipants();

// Get all participants in the voice room
List<string> participants = voice.GetAllParticipants();
int count = voice.ParticipantCount;
```

## Events

```csharp
// Voice connection state changed
voice.OnVoiceConnectionChanged += (bool connected) => {
    Debug.Log(connected ? "Voice connected" : "Voice disconnected");
};

// Participant started/stopped speaking
voice.OnParticipantSpeaking += (string puid, bool isSpeaking) => {
    Debug.Log($"{puid} is {(isSpeaking ? "speaking" : "silent")}");
};

// Participant audio status changed (muted/unmuted)
voice.OnParticipantAudioStatusChanged += (string puid, RTCAudioStatus status) => {
    Debug.Log($"{puid} audio status: {status}");
};
```

## Audio Devices

Query and switch between input/output devices:

```csharp
// Query available devices
voice.QueryAudioDevices();

// After query completes, read device lists
foreach (var device in voice.InputDevices)
{
    Debug.Log($"Mic: {device.DeviceName} (default={device.DefaultDevice})");
}

foreach (var device in voice.OutputDevices)
{
    Debug.Log($"Speaker: {device.DeviceName}");
}

// Switch input device
voice.SetInputDevice(deviceId);

// Switch output device
voice.SetOutputDevice(deviceId);

// Listen for device hotplug events
voice.OnAudioDevicesChanged += () => {
    Debug.Log("Audio devices changed!");
};
```

## Mic Level Meter

The `LocalMicLevel` property provides a 0-1 float value representing the current microphone input level, suitable for driving a UI level meter:

```csharp
void Update()
{
    float level = EOSVoiceManager.Instance.LocalMicLevel;
    micLevelBar.fillAmount = level;
}
```

On Android, `LocalMicLevel` uses the EOS SDK's speaking callback as a proxy instead of Unity's Microphone API, to avoid conflicts with the SDK's own audio capture.

## Spatial Voice (3D Audio)

Spatial voice makes player voices come from their position in the game world. This works automatically when using the [FishNet EOS Transport](https://github.com/TrentSterling/fishnet-eos-transport).

### How It Works

1. The transport sets `EOSVoiceManager.UseManualAudioOutput = true` before creating/joining lobbies
2. When a networked player spawns, an `EOSVoicePlayer` component is automatically added to the player GameObject
3. The `EOSVoicePlayer` receives raw audio frames from the EOS SDK and plays them through an AudioSource with 3D spatial settings

### Manual Spatial Voice Setup

If you are not using the FishNet EOS Transport, you can set up spatial voice manually:

```csharp
// Before creating/joining a lobby, enable manual audio output
EOSVoiceManager.Instance.UseManualAudioOutput = true;

// On each player's GameObject, add an EOSVoicePlayer
var voicePlayer = playerGameObject.AddComponent<EOSVoicePlayer>();
voicePlayer.ParticipantPuid = playerPuid; // Set the EOS ProductUserId
```

### EOSVoicePlayer Settings

The `EOSVoicePlayer` component has several configurable settings:

| Setting | Default | Description |
|---|---|---|
| **Spatial Blend** | `1.0` | 0 = 2D (no spatialization), 1 = full 3D |
| **Doppler Level** | `1.0` | Doppler effect intensity (0 = off) |
| **Min Distance** | `1.0` | Distance before volume starts attenuating |
| **Max Distance** | `50.0` | Maximum audible distance |
| **Rolloff Mode** | `Logarithmic` | How volume attenuates with distance |

### Voice Effects

EOSVoicePlayer supports real-time voice effects:

**Pitch Shifting:**

```csharp
voicePlayer.EnablePitchShift = true;
voicePlayer.PitchShift = 0.7f; // Lower pitch (0.5 = octave down, 2.0 = octave up)
```

**Reverb:**

```csharp
voicePlayer.EnableReverb = true;
voicePlayer.ReverbPreset = AudioReverbPreset.Cave;
voicePlayer.ReverbLevel = -500f; // mB, -10000 to 0
```

## Android Notes

- The `RECORD_AUDIO` permission is automatically added to your Android manifest by the build processor
- On Android, Unity's Microphone API is not used (it would conflict with the EOS SDK's native audio capture)
- Voice transmission works correctly on Android; only the mic level meter uses a simplified approach

## Troubleshooting

- **No voice after joining lobby** -- Check Console for `[EOSVoice] RTC Room connected`. If missing, verify that the lobby was created with `EnableVoice = true`.
- **LocalAudioStatus is Unsupported** -- No audio input device available, or the audio pipeline failed to initialize. Check `voice.InputDevices` after calling `QueryAudioDevices()`.
- **Voice works on PC but not Android** -- Ensure the EOS Android native library is loading correctly. Check for `AndroidJavaInitSuccess` errors in the console.
