#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
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

        /// <summary>
        /// Build Android APK without save dialog (MCP-friendly).
        /// Called via reflection from TrontMCP build_player tool.
        /// Returns JSON with build result.
        /// </summary>
        public static string BuildAndroidHeadless(string outputPath, bool development)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                string buildsDir = Path.Combine(Application.dataPath, "..", "Builds");
                Directory.CreateDirectory(buildsDir);
                outputPath = Path.Combine(buildsDir, $"{PlayerSettings.productName}.apk");
            }

            // Ensure output directory exists
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 0)
                return "{\"success\":false,\"error\":\"No scenes in Build Settings. Add scenes before building.\"}";

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            string mode = development ? "Development" : "Release";
            Debug.Log($"[EOS Build] Starting headless Android build → {outputPath} ({mode})");

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                float sizeMB = summary.totalSize / (1024f * 1024f);
                Debug.Log($"[EOS Build] SUCCESS — {sizeMB:F1}MB, {summary.totalTime.TotalSeconds:F1}s → {outputPath}");
                return $"{{\"success\":true,\"outputPath\":\"{EscapeJson(outputPath)}\",\"sizeMB\":{sizeMB:F1},\"buildTimeSeconds\":{summary.totalTime.TotalSeconds:F1},\"errors\":0,\"warnings\":{summary.totalWarnings}}}";
            }
            else
            {
                Debug.LogError($"[EOS Build] FAILED — {summary.totalErrors} error(s)");
                // Collect error messages from the report
                var sb = new System.Text.StringBuilder();
                sb.Append("{\"success\":false,");
                sb.Append($"\"error\":\"{summary.totalErrors} error(s)\",");
                sb.Append($"\"buildTimeSeconds\":{summary.totalTime.TotalSeconds:F1},");
                sb.Append("\"steps\":[");
                bool first = true;
                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        {
                            if (!first) sb.Append(",");
                            sb.Append($"\"").Append(EscapeJson(msg.content)).Append("\"");
                            first = false;
                        }
                    }
                }
                sb.Append("]}");
                return sb.ToString();
            }
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

            if (summary.result == BuildResult.Succeeded)
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

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}
#endif
