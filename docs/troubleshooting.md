# Troubleshooting

## "SDK is in a corrupted state"

**Symptom:** Console shows `[EOS-Native] SDK is in a corrupted state` on play mode entry.

**Cause:** The EOS native DLL keeps state across play mode cycles. If the SDK encounters a fatal error (crash, forced shutdown, domain reload issue), the corrupted state persists until the editor is restarted.

**Fix:** Restart the Unity Editor. There is no way to recover without a full restart -- the native DLL state cannot be reset from C#.

## Login Fails with NotFound or InvalidAuth

**Symptom:** Console shows login failure with `Result.NotFound` or `Result.InvalidAuth`.

**Possible causes:**

1. **Wrong credentials** -- Double-check that your Product ID, Sandbox ID, Deployment ID, Client ID, and Client Secret all match what is shown in the [EOS Developer Portal](https://dev.epicgames.com/portal).

2. **Deployment not active** -- Make sure the deployment is in an active state in the portal. Archived or deleted deployments will reject logins.

3. **DeviceID not enabled** -- In the EOS Developer Portal, go to your product's Client policies. Ensure the client policy allows `DeviceIdToken` as a login method. When creating a new client, select **"GameClient"** as the policy type.

4. **Wrong sandbox/deployment pairing** -- The Deployment ID must belong to the Sandbox ID you specified. Cross-sandbox deployments will fail.

## SDK Not Initializing

**Symptom:** No `[EOS-Native]` messages in the Console at all.

**Possible causes:**

1. **EOSConfig not assigned** -- Select your EOSManager GameObject and check that the Config field has an EOSConfig asset assigned.

2. **Auto Initialize disabled** -- Check that "Auto Initialize" is enabled on the EOSManager component, or call initialization manually.

3. **Missing native binaries** -- Verify that the package's native libraries are present. Look for `.dll`, `.dylib`, `.so`, or `.aar` files in the package's `Runtime/Plugins` folder.

4. **Platform not supported** -- The SDK supports Windows, macOS, Linux, Android, and iOS. WebGL and consoles are not supported.

## Android Build Errors

See the dedicated [Android / Quest Builds](android-builds.md) page for comprehensive Android troubleshooting, including:

- `FakeDependency.jar` transform errors
- `targetSdkVersion` too high for Quest
- Duplicate class / AndroidX conflicts

## Voice Not Working

**Symptom:** Voice room connects but no audio is heard.

1. **Check connection** -- Look for `[EOSVoice] RTC Room connected` in the Console. If missing, the lobby may not have been created with voice enabled.

2. **Check auto-unmute** -- Look for `[EOSVoice] Auto-unmute result: Success`. If the result is not `Success`, the audio pipeline may have failed to initialize.

3. **Check audio devices** -- Call `EOSVoiceManager.Instance.QueryAudioDevices()` and check `InputDevices.Count`. If zero, no microphone is available.

4. **LocalAudioStatus is Unsupported** -- This means the SDK could not find a usable audio input device. On Android, this can happen if the `RECORD_AUDIO` permission was denied.

5. **Android-specific** -- See the "Android Notes" section in [Voice Chat](voice-chat.md).

## Lobby Search Returns No Results

**Symptom:** `SearchLobbiesAsync` returns an empty list even though lobbies exist.

1. **Bucket mismatch** -- If you specified a `BucketId` when creating the lobby, the search must use the same `BucketId`. Different bucket IDs create separate matchmaking pools.

2. **Self-filtering** -- The search automatically excludes lobbies owned by the local player. This is intentional.

3. **Ghost lobbies** -- Lobbies with zero members are filtered out automatically.

4. **Platform/sandbox mismatch** -- Players must be using the same Sandbox ID and Deployment ID to see each other's lobbies.

## Lobby Join Fails

**Symptom:** `JoinLobbyByCodeAsync` or `JoinLobbyByIdAsync` returns an error.

1. **Lobby full** -- Check `lobby.AvailableSlots` before joining.
2. **Lobby no longer exists** -- The host may have left or the lobby was destroyed.
3. **Already in a lobby** -- Leave the current lobby first with `LeaveLobbyAsync()` before joining another.

## ParrelSync Clone Issues

**Symptom:** Two editors show the same ProductUserId or interfere with each other.

This should not happen with DeviceID login, as each editor instance gets a unique device token. If you see duplicate PUIDs:

1. Delete the device token cache: `PlayerPrefs.DeleteKey("EOSDeviceToken")`
2. Each ParrelSync clone should automatically get a unique display name suffix

## Editor vs Build Differences

- **Editor:** The EOS native library is loaded at play mode start and persists across domain reloads. Corrupted SDK state requires an editor restart.
- **Builds:** The native library loads once at app start and unloads cleanly on quit. No corruption issues.
- **Android:** Some features (Unity Microphone API, certain UI overlays) behave differently on device vs editor. Always test voice chat on a real device or emulator.

## Getting Help

If you encounter an issue not covered here:

1. Check the Unity Console for `[EOS-Native]` and `[EOSVoice]` log messages -- they often contain diagnostic details
2. Enable verbose logging in EOSManager's Debug Settings
3. Open an issue on [GitHub](https://github.com/TrentSterling/eos-sdk/issues)
