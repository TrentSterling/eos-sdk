# Changelog

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
