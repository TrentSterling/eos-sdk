#if UNITY_ANDROID
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EOSNative.Editor
{
    /// <summary>
    /// Pre-build validator that runs before Android builds (callbackOrder 0, before EOSAndroidBuildProcessor at 99).
    /// Logs warnings for common EOS configuration issues. Does NOT block builds.
    /// </summary>
    public class EOSPreBuildValidator : IPreprocessBuildWithReport
    {
        private const string SuppressDialogPref = "EOS_SuppressPreBuildDialog";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android)
                return;

            Debug.Log("[EOS-Native] Pre-build validation running...");

            bool hasCritical = false;

            // 1. Build processor class exists
            var processorType = System.Type.GetType("EOSNative.Editor.EOSAndroidBuildProcessor, EOSNative.Editor");
            if (processorType == null)
            {
                Debug.LogError("[EOS-Native] PRE-BUILD: EOSAndroidBuildProcessor class not found! " +
                               "Desugaring, AndroidX deps, and ProGuard rules will NOT be injected. Build will likely fail.");
                hasCritical = true;
            }

            // 2. minSdkVersion >= 23
            int minSdk = (int)PlayerSettings.Android.minSdkVersion;
            if (minSdk < 23)
            {
                Debug.LogError($"[EOS-Native] PRE-BUILD: minSdkVersion is {minSdk}, but EOS SDK requires >= 23.");
                hasCritical = true;
            }

            // 3. targetSdkVersion — auto-clamp to 34 if too high or Automatic.
            // Unity 6.1+ defaults to API 35/36 which causes desugaring/gradle errors
            // and Quest doesn't support API 35+ yet. Auto-fix to 34 (Android 14).
            int targetSdk = (int)PlayerSettings.Android.targetSdkVersion;
            if (targetSdk >= 35)
            {
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
                Debug.LogWarning($"[EOS-Native] PRE-BUILD: targetSdkVersion was {targetSdk} (API 35+ causes " +
                                 "desugaring/gradle errors). Auto-set to 34 (Android 14).");
            }
            else if (targetSdk == 0)
            {
                // 0 = Automatic (highest installed). Unity 6.1+ ships with SDK 35/36.
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
                Debug.LogWarning("[EOS-Native] PRE-BUILD: targetSdkVersion was Automatic (highest installed). " +
                                 "Unity 6.1+ defaults to API 35/36 which causes gradle errors. Auto-set to 34 (Android 14).");
            }

            // 4. EOSConfig asset exists
            var configGuids = AssetDatabase.FindAssets("t:EOSConfig");
            if (configGuids.Length == 0)
            {
                Debug.LogWarning("[EOS-Native] PRE-BUILD: No EOSConfig asset found. " +
                                 "Android login callbacks require a ClientId for the protocol scheme.");
            }

            // 5. Custom gradle templates exist
            string androidDir = Path.Combine(Application.dataPath, "Plugins", "Android");
            bool hasMainTemplate = File.Exists(Path.Combine(androidDir, "mainTemplate.gradle"));
            bool hasLauncherTemplate = File.Exists(Path.Combine(androidDir, "launcherTemplate.gradle"));
            if (!hasMainTemplate || !hasLauncherTemplate)
            {
                Debug.LogWarning("[EOS-Native] PRE-BUILD: Custom gradle templates not found. " +
                                 "The build processor will inject desugaring at build time, but this can fail on Unity 6.1+. " +
                                 "Generate templates via Tools > EOS SDK > Android Build Validator.");
            }

            // Show dialog for critical issues (suppressible)
            if (hasCritical && !EditorPrefs.GetBool(SuppressDialogPref, false))
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "EOS Android Build Issues",
                    "Critical EOS configuration issues were detected that may cause your build to fail.\n\n" +
                    "Open the Android Build Validator for details?",
                    "Open Validator",
                    "Build Anyway",
                    "Don't Show Again");

                if (choice == 0)
                    EOSAndroidBuildValidator.ShowWindow();
                else if (choice == 2)
                    EditorPrefs.SetBool(SuppressDialogPref, true);
            }

            Debug.Log("[EOS-Native] Pre-build validation complete.");
        }
    }
}
#endif
