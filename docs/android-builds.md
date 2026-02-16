# Android / Quest Builds

Building for Android (including Meta Quest) requires additional Gradle configuration because the EOS SDK's AAR library uses Java 8+ APIs that need D8 desugaring. The build processor handles all configuration automatically, including version-specific workarounds for Unity 2022 through Unity 6.

**Tested Unity versions:** 2022.3, 6000.0, 6000.1, 6000.3 -- all produce working APKs.

## Quick Setup

The fastest way to configure your project for Android:

1. Open **Tools > EOS SDK > Android Build Validator**
2. Click **"Generate EOS Gradle Templates"**
3. Done -- your project is now configured for Android builds

This generates four Gradle template files in `Assets/Plugins/Android/` that handle all the necessary configuration automatically.

## What the Templates Do

The generator creates four files that configure your Android build:

| File | Purpose |
|---|---|
| `mainTemplate.gradle` | Enables Java 17 source/target compatibility and `coreLibraryDesugaring` |
| `launcherTemplate.gradle` | Enables desugaring and adds AndroidX + desugar_jdk_libs dependencies |
| `gradleTemplate.properties` | Enables AndroidX and Jetifier for legacy support libraries |
| `settingsTemplate.gradle` | Adds `google()` and `mavenCentral()` repositories |

These templates work with Unity's Custom Gradle Templates system. If you already have custom templates, the generator will merge the required changes.

## Build Validator

The Android Build Validator window (`Tools > EOS SDK > Android Build Validator`) runs 10 checks on your project:

1. **Build Target** -- Must be Android
2. **Scripting Backend** -- IL2CPP required for ARM64
3. **Target Architecture** -- ARM64 required
4. **minSdkVersion** -- Must be 23 or higher
5. **targetSdkVersion** -- Must be 32-34 for Quest (Unity 6.1+ defaults to 35/36)
6. **Gradle Templates** -- Custom templates must exist
7. **Desugaring Config** -- `coreLibraryDesugaring` must be enabled
8. **AndroidX** -- Must be enabled in gradle.properties
9. **Repositories** -- `google()` and `mavenCentral()` must be present
10. **EOS Native Libraries** -- AAR/SO files must be present

Each check shows a green checkmark or red X, with auto-fix buttons for most issues.

## Pre-Build Validator

A separate pre-build validator runs automatically before every Android build (via `IPreprocessBuildWithReport`). It checks critical settings and logs warnings to the Console if issues are detected. It warns but does not block the build, so you can still iterate quickly.

## Build Processor

The `EOSAndroidBuildProcessor` runs automatically at callback order 99 during Android builds (via `IPostGenerateGradleAndroidProject`). It handles:

- **Desugaring** -- Injects `coreLibraryDesugaringEnabled true` and `desugar_jdk_libs:2.1.4`
- **compileOptions** -- Ensures Java 17 source and target compatibility
- **AndroidX Dependencies** -- Adds `appcompat`, `core-ktx`, and other required libraries
- **ProGuard Rules** -- Adds keep rules for EOS SDK classes
- **Android Permissions** -- Adds `INTERNET`, `ACCESS_NETWORK_STATE`, `RECORD_AUDIO`, and others
- **JNI Library Loader** -- Ensures EOS native libraries are loaded correctly
- **Login Scheme** -- Registers the EOS login URI scheme for external auth flows

The build processor acts as a safety net. Even if your Gradle templates are correct, it verifies and patches the generated Gradle project as a final step.

## Unity Version Compatibility

The build processor automatically adapts its configuration based on your Unity version:

| | Unity 2022.3 | Unity 6000.0 | Unity 6000.1+ |
|---|---|---|---|
| AGP | 7.4.2 | ~8.x | 8.x |
| Java Version | 1.8 | 11 | 17 |
| Desugaring | Skipped (D8 bug) | 2.0.4 | 2.1.4 |
| D8 Workaround | Yes | No | No |
| Namespace Fix | No | No | Yes |

### Unity 2022 D8 Workaround

Unity 2022's AGP 7.4.2 ships a D8 dexer (R8 8.2.2-dev) that crashes with a `NullPointerException` when processing the EOS SDK's Java 11 class files. This affects 20 of the 35 classes in the AAR.

The build processor applies a fully automatic workaround:

1. **Empties the AAR's `classes.jar`** -- prevents the broken D8 from seeing the Java 11 classes
2. **Copies pre-dexed classes to `libs/`** -- these were pre-compiled with Unity 6's working D8, and AGP passes `.dex` entries through without re-processing
3. **Adds compile-only classes for javac** -- the original bytecode is available for compilation but excluded from dexing

No user action is required. The workaround only activates on Unity 2022 and earlier.

### Unity 6.1+ Namespace Fix

AGP 8.x requires all Android library modules to declare a `namespace` in their build.gradle. Legacy `.androidlib` modules (like EOS resources) use the old format and lack this. The build processor auto-generates the namespace from AndroidManifest.xml.

## Common Build Errors

### D8 NullPointerException (Unity 2022)

```
D8: java.lang.NullPointerException
  at com.android.tools.r8.graph.u2.<init>
```

**Cause:** AGP 7.4.2's D8 dexer (R8 8.2.2-dev) crashes on Java 11 class files in the EOS SDK AAR. This only affects Unity 2022 and earlier.

**Fix:** This is handled automatically by the build processor's D8 workaround. If you see this error, ensure you have `com.tront.eos-sdk` v1.5.0+ which includes the `eos-classes-predexed.jar` and `eos-classes-compile.jar` files. The build processor will detect Unity 2022 and apply the fix.

### FakeDependency.jar Transform Error

```
Execution failed for task ':launcher:transformClassesWithDesugarForRelease'.
> Could not resolve all files for configuration ':launcher:desugaredMethodsOutput'.
  > FakeDependency.jar
```

**Cause:** The EOS SDK's AAR uses Java 8+ APIs, but Unity's default Gradle configuration does not enable desugaring.

**Fix:** Open **Tools > EOS SDK > Android Build Validator** and click **"Generate EOS Gradle Templates"**. This adds the required desugaring configuration.

### targetSdkVersion Too High

```
Installation failed: INSTALL_FAILED_OLDER_SDK
```

**Cause:** Unity 6.1+ defaults `targetSdkVersion` to 35 or 36, but Meta Quest devices require 32-34.

**Fix:**
1. Open **Edit > Project Settings > Player > Android > Other Settings**
2. Set **Target API Level** to `API Level 32` (or 33/34 for newer Quest firmware)

### minSdkVersion Too Low

```
Manifest merger failed: uses-sdk:minSdkVersion 21 cannot be smaller than version 23
```

**Fix:**
1. Open **Edit > Project Settings > Player > Android > Other Settings**
2. Set **Minimum API Level** to `API Level 23` or higher

### Duplicate Class Errors

```
Duplicate class android.support.v4.xxx found in modules
```

**Cause:** Mixing old Android Support Library with AndroidX.

**Fix:** Ensure `gradleTemplate.properties` contains:
```properties
android.useAndroidX=true
android.enableJetifier=true
```

The Gradle template generator handles this automatically.

## Manual Gradle Setup

If you prefer to configure Gradle manually instead of using the generator, here are the required changes. The Java version and desugaring library version depend on your Unity version:

| Unity Version | Java | desugar_jdk_libs |
|---|---|---|
| 2022.3 | VERSION_1_8 | 1.2.3 |
| 6000.0 | VERSION_11 | 2.0.4 |
| 6000.1+ | VERSION_17 | 2.1.4 |

> **Note:** On Unity 2022, skip the `coreLibraryDesugaringEnabled` and `desugar_jdk_libs` lines entirely -- the build processor uses a pre-dexed workaround instead.

### mainTemplate.gradle

```groovy
android {
    compileOptions {
        sourceCompatibility JavaVersion.VERSION_17  // see table above
        targetCompatibility JavaVersion.VERSION_17  // see table above
        coreLibraryDesugaringEnabled true            // skip on Unity 2022
    }
}

dependencies {
    coreLibraryDesugaring 'com.android.tools:desugar_jdk_libs:2.1.4'  // skip on Unity 2022
}
```

### launcherTemplate.gradle

```groovy
android {
    compileOptions {
        sourceCompatibility JavaVersion.VERSION_17  // see table above
        targetCompatibility JavaVersion.VERSION_17  // see table above
        coreLibraryDesugaringEnabled true            // skip on Unity 2022
    }
}

dependencies {
    coreLibraryDesugaring 'com.android.tools:desugar_jdk_libs:2.1.4'  // skip on Unity 2022
    implementation 'androidx.appcompat:appcompat:1.6.1'
    implementation 'androidx.core:core-ktx:1.12.0'
    implementation 'androidx.activity:activity:1.8.0'
    implementation 'androidx.fragment:fragment:1.6.2'
}
```

### gradleTemplate.properties

```properties
android.useAndroidX=true
android.enableJetifier=true
```

### settingsTemplate.gradle

Ensure these repositories are present:

```groovy
repositories {
    google()
    mavenCentral()
}
```

## Quest-Specific Notes

- Set **Target API Level** to 32-34 (not 35+)
- Set **Minimum API Level** to 23+
- Set **Scripting Backend** to IL2CPP
- Set **Target Architecture** to ARM64 only
- The `RECORD_AUDIO` permission is added automatically for voice chat
- The EOS SDK includes native `.so` libraries for `arm64-v8a`

## Checking Your Build Configuration

You can verify your Android configuration at any time:

1. Open **Tools > EOS SDK > Android Build Validator**
2. All 10 checks should show green checkmarks
3. If any are red, click the auto-fix button or follow the instructions

The pre-build validator also logs warnings to the Console automatically before each build, so you will be notified of issues even without opening the validator window.
