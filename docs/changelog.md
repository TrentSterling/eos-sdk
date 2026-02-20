# Changelog

## v1.6.4

- **Local mute state tracking** -- `SetParticipantMuted` now tracks which participants are locally muted. New APIs: `IsParticipantLocallyMuted(puid)` to check if you've muted a specific player, `GetLocallyMutedParticipants()` to get all muted PUIDs. Mute state is automatically cleaned up when a participant leaves or the lobby is destroyed.
- **EOSVoicePlayer.IsLocallyMuted** -- new `bool` property on `EOSVoicePlayer` that reads the local mute state directly from the voice manager.

## v1.6.3

- **External auth login** -- `LoginWithExternalAuthAsync()` supports all 20+ EOS credential types for server-authoritative authentication. `LoginWithOculusNonceAsync()` convenience method for Meta Quest VR apps.
- **Security docs** -- new `security.md` covering auth architecture, token handling, and platform-specific guidance.
- **Auth docs** -- new `auth.md` covering authentication flows and credential types.

## v1.6.2

- **Fix** -- Exclude full lobbies filter now works reliably. The search was client-side filtering with a server-side limit of 10 results, meaning full lobbies consumed result slots. Now over-fetches from EOS (3x or 50 minimum) when client-side filters are active, then caps to the requested `MaxResults`.
- **Fix** -- `LeaveLobbyAsync` cleanup ordering. Notifications are now unsubscribed and state cleared BEFORE the EOS leave call (was after). Prevents stale notifications from triggering during the leave sequence, which caused race conditions during host migration.
- **Fix** -- `LeaveLobbySync` now fires `OnLobbyLeft` event so FishNet and other subscribers get notified on sync leave (was missing).
- **Fix** -- `GetLobbyDataAsync` retry loop reduced from 7.5s worst case (15 iters, 500ms cap) to 1.75s (10 iters, 250ms cap, check-first). Directly speeds up host migration.
- **Ghost lobby defense** -- Added `LobbyData.IsGhost` property. Ghost checks added to `JoinLobbyByIdAsync` (auto-leave), `SearchByLobbyIdAsync`, `SearchByMemberAsync`, `FindFriendLobbiesAsync`, and `ProcessSearchResults`.

## v1.6.1

- **Batch attribute setting** -- `SetLobbyAttributesBatchAsync` sets all lobby attributes in a single EOS modification (atomic, 1 round trip instead of N individual calls). `CreateLobbyAsync` now uses this internally for join code, migration support, and all custom attributes.
- **Fix** -- `OnLobbyCreated` → `OnLobbyJoined` in `CreateLobbyAsync`. Voice init was calling the wrong method, which could cause the host to miss the RTC room connection on some timing paths.
- **Search debug logging** -- lobby searches now log all search parameters, raw EOS result count vs post-filter count, and per-lobby attribute summaries. Makes it much easier to diagnose "why didn't my search find anything?" issues.

## v1.6.0

- **Auto-leave before create** -- if the player is already in a lobby when `CreateLobbyAsync` is called, the old lobby is left automatically before creating the new one, preventing `LimitExceeded` errors
- **LimitExceeded retry** -- if `CreateLobby` returns `LimitExceeded`, automatically leaves any current lobby and retries once

## v1.5.0

- **Unity 2022 Android build fix** -- AGP 7.4.2's D8 dexer crashes on Java 11 class files in the EOS AAR (NullPointerException on NestHost/NestMembers attributes and `$values()` enum pattern). The build processor now auto-applies a 3-part workaround: empties the AAR's classes.jar, includes pre-dexed classes (built with Unity 6's D8), and adds a compile-only JAR for javac resolution. Fully automatic, no user action needed.
- **Multi-version Android support** -- build processor adapts Gradle config per Unity version (Java 8/11/17, desugar_jdk_libs 1.2.3/2.0.4/2.1.4, D8 workaround on/off). Tested and working on Unity 2022.3, 6000.0, 6000.1, and 6000.3.
- **Minimum Unity version lowered** -- now supports Unity 2021.3+ (previously required Unity 6000.0+)
- **VoIP init timing instrumentation** -- every voice init step now logs with `[TIMING]` tag showing elapsed milliseconds (`GetRTCRoomName`, `RegisterAudioNotifications`, `QueryAudioDevices`, auto-unmute callback)
- **Pre-query audio devices at login** -- `PreQueryAudioDevices()` runs automatically on `EOSManager.OnLoginSuccess`, warming up the device cache so voice connects faster when joining a lobby
- **`OnVoiceInitProgress` event** -- fires step-by-step messages during voice init (`"Getting room name..."`, `"Querying audio devices..."`, `"Voice ready!"`) for UI progress indicators
- **Skip redundant device query** -- if devices were already pre-queried at login, the voice connect path skips `QueryAudioDevices()` entirely

## v1.4.6

- **Gradle Template Generator** -- one-click fix for `FakeDependency.jar` errors on Android. Open `Tools > EOS SDK > Android Build Validator` and click "Generate EOS Gradle Templates".
- **Build Processor Robustness** -- `compileOptions` fallback injection, post-injection verification to confirm Gradle changes were applied correctly.
- **Pre-Build Validator** -- automatic warnings about Android configuration issues before build starts (`IPreprocessBuildWithReport`). Warns but does not block.
- **Version Checker** -- `Tools > EOS SDK > Check for Updates` to check for new releases.
- **Fix** -- `EOSConfig.EncryptionKey` no longer defaults to null (defaults to empty string to prevent NullReferenceException).
- **Fix** -- `EOSPlayerRegistry.RegisterPlayer` no longer crashes on short PUIDs (less than 32 characters).

## v1.4.5

- **Android Build Validator** -- new editor window (`Tools > EOS SDK > Android Build Validator`) with 10 checks and auto-fix buttons for common Android build issues.
- **Enhanced Build Processor Logging** -- detailed logs showing exactly what the `EOSAndroidBuildProcessor` injects into each Gradle file.

## v1.4.4

- **Fix** -- `EOSVoicePlayer` not receiving audio when `EOSVoiceManager` initializes late (race condition where audio frame callback was registered before the voice manager was ready).
