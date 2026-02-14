#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace EOSNative.Editor
{
    /// <summary>
    /// Cross-platform build helper. Builds Android APKs without switching the
    /// editor's active build platform — no more waiting for platform switch.
    /// </summary>
    public static class EOSBuildHelper
    {
        [MenuItem("Tools/EOS SDK/Build Android APK (Development)", false, 60)]
        public static void BuildAndroidDev()
        {
            BuildAndroid(BuildOptions.Development);
        }

        [MenuItem("Tools/EOS SDK/Build Android APK (Release)", false, 61)]
        public static void BuildAndroidRelease()
        {
            BuildAndroid(BuildOptions.None);
        }

        private static void BuildAndroid(BuildOptions extraOptions)
        {
            string defaultName = $"{PlayerSettings.productName}.apk";
            string path = EditorUtility.SaveFilePanel("Save Android APK", "", defaultName, "apk");
            if (string.IsNullOrEmpty(path)) return;

            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[EOS Build] No scenes in Build Settings. Add scenes before building.");
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                options = extraOptions,
            };

            Debug.Log($"[EOS Build] Starting Android build → {path} ({(extraOptions.HasFlag(BuildOptions.Development) ? "Development" : "Release")})");

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                float sizeMB = summary.totalSize / (1024f * 1024f);
                Debug.Log($"[EOS Build] SUCCESS — {sizeMB:F1}MB, {summary.totalTime.TotalSeconds:F1}s → {path}");
                EditorUtility.RevealInFinder(path);
            }
            else
            {
                Debug.LogError($"[EOS Build] FAILED — {summary.totalErrors} error(s)");
            }
        }

        private static string[] GetEnabledScenes()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                    scenes.Add(scene.path);
            }
            return scenes.ToArray();
        }
    }
}
#endif
