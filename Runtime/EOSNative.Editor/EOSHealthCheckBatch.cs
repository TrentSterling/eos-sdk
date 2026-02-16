#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EOSNative.Editor
{
    /// <summary>
    /// Batch mode entry point for running EOSHealthCheck integration tests.
    /// Called via:
    ///   Unity.exe -batchmode -projectPath ... -executeMethod EOSNative.Editor.EOSHealthCheckBatch.Run
    ///     -healthCheckMode Solo -healthCheckTimeout 120
    /// Enters Play Mode, waits for EOSHealthCheck to auto-create, runs it via reflection,
    /// writes healthcheck_result.json, and exits with 0 (all pass) or 1 (any fail/timeout).
    ///
    /// Uses [InitializeOnLoad] + EditorPrefs to survive Play Mode domain reloads.
    /// </summary>
    [InitializeOnLoad]
    public static class EOSHealthCheckBatch
    {
        // EditorPrefs keys to persist state across domain reloads
        private const string PREF_ACTIVE = "EOSHealthCheckBatch_Active";
        private const string PREF_MODE = "EOSHealthCheckBatch_Mode";
        private const string PREF_TIMEOUT = "EOSHealthCheckBatch_Timeout";
        private const string PREF_START_TIME = "EOSHealthCheckBatch_StartTime";
        private const string PREF_RESULT_PATH = "EOSHealthCheckBatch_ResultPath";

        // Runtime state (rebuilt after domain reload)
        private static bool _triggered;
        private static object _healthCheck;
        private static Type _hcType;
        private static FieldInfo _isRunningField;
        private static FieldInfo _stepsField;
        private static FieldInfo _passField;
        private static FieldInfo _failField;
        private static FieldInfo _skipField;
        private static FieldInfo _testModeField;

        static EOSHealthCheckBatch()
        {
            // [InitializeOnLoad] fires after every domain reload.
            // If we're in the middle of a batch health check, re-attach the update callback.
            if (!EditorPrefs.GetBool(PREF_ACTIVE, false))
                return;

            Debug.Log("[EOSHealthCheckBatch] Domain reload detected — re-attaching update callback");
            EditorApplication.update += OnUpdate;
        }

        /// <summary>
        /// Batch mode entry point. Called via -executeMethod.
        /// </summary>
        public static void Run()
        {
            string mode = "Solo";
            float timeout = 120f;

            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-healthCheckMode" && i + 1 < args.Length)
                    mode = args[i + 1];
                if (args[i] == "-healthCheckTimeout" && i + 1 < args.Length)
                    float.TryParse(args[i + 1], out timeout);
            }

            string resultPath = Path.Combine(Application.dataPath, "..", "TestResults", "healthcheck_result.json");
            string resultDir = Path.GetDirectoryName(resultPath);
            if (!string.IsNullOrEmpty(resultDir))
                Directory.CreateDirectory(resultDir);

            // Persist state in EditorPrefs so it survives the Play Mode domain reload
            EditorPrefs.SetBool(PREF_ACTIVE, true);
            EditorPrefs.SetString(PREF_MODE, mode);
            EditorPrefs.SetFloat(PREF_TIMEOUT, timeout);
            EditorPrefs.SetString(PREF_START_TIME, EditorApplication.timeSinceStartup.ToString("F2"));
            EditorPrefs.SetString(PREF_RESULT_PATH, resultPath);

            Debug.Log($"[EOSHealthCheckBatch] Starting: mode={mode}, timeout={timeout}s");

            // In batch mode, no scene is open by default. Load the first enabled scene
            // from Build Settings so Play Mode has something to work with.
            string scenePath = null;
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    scenePath = scene.path;
                    break;
                }
            }

            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError("[EOSHealthCheckBatch] No scenes in Build Settings! Add a scene first.");
                WriteTimeout(resultPath, mode, timeout, 0);
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[EOSHealthCheckBatch] Opening scene: {scenePath}");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);

            EditorApplication.update += OnUpdate;

            // Enter Play Mode — the scene we just loaded will be used.
            // This triggers a domain reload, after which [InitializeOnLoad] re-attaches OnUpdate.
            EditorApplication.isPlaying = true;
        }

        private static void OnUpdate()
        {
            // Only run when active and in Play Mode
            if (!EditorPrefs.GetBool(PREF_ACTIVE, false))
            {
                EditorApplication.update -= OnUpdate;
                return;
            }

            if (!EditorApplication.isPlaying)
                return;

            // Read persisted state
            string mode = EditorPrefs.GetString(PREF_MODE, "Solo");
            float timeout = EditorPrefs.GetFloat(PREF_TIMEOUT, 120f);
            string resultPath = EditorPrefs.GetString(PREF_RESULT_PATH, "");

            // timeSinceStartup resets on domain reload, so measure from when Play Mode
            // actually started (which is approximately when this update first fires in play mode).
            // We use a simple monotonic approach: track elapsed via EditorPrefs.
            double elapsed = GetElapsed();

            // Wait 3s for scene load + EOSHealthCheck auto-create + EOS init
            if (!_triggered && elapsed > 3.0)
            {
                if (!TryTriggerHealthCheck(mode))
                {
                    if (elapsed > timeout)
                    {
                        Debug.LogError("[EOSHealthCheckBatch] Timed out — EOSHealthCheck never appeared");
                        WriteTimeout(resultPath, mode, timeout, elapsed);
                        Finish(1);
                    }
                    return;
                }
            }

            if (!_triggered)
                return;

            // Poll for completion
            if (_healthCheck != null)
            {
                // The MonoBehaviour might have been destroyed (scene change etc.)
                if (_healthCheck is UnityEngine.Object uo && uo == null)
                {
                    Debug.LogError("[EOSHealthCheckBatch] EOSHealthCheck was destroyed during test");
                    WriteTimeout(resultPath, mode, timeout, elapsed);
                    Finish(1);
                    return;
                }

                bool isRunning = (bool)_isRunningField.GetValue(_healthCheck);
                if (!isRunning && elapsed > 4.0)
                {
                    WriteResults(resultPath, mode, elapsed, true);
                    int fail = (int)_failField.GetValue(_healthCheck);
                    Finish(fail > 0 ? 1 : 0);
                    return;
                }
            }

            // Timeout
            if (elapsed > timeout)
            {
                Debug.LogError($"[EOSHealthCheckBatch] Timed out after {timeout}s");
                if (_healthCheck != null)
                    WriteResults(resultPath, mode, elapsed, false);
                else
                    WriteTimeout(resultPath, mode, timeout, elapsed);
                Finish(1);
            }
        }

        private static double GetElapsed()
        {
            // We can't rely on a single timeSinceStartup reference across domain reloads.
            // Instead, count frames. But simplest: just use Time.realtimeSinceStartup in play mode.
            if (EditorApplication.isPlaying)
                return Time.realtimeSinceStartupAsDouble;
            return 0;
        }

        private static bool TryTriggerHealthCheck(string mode)
        {
            _hcType = FindType("FishNet.Transport.EOSNative.Diagnostics.EOSHealthCheck");
            if (_hcType == null)
            {
                Debug.Log("[EOSHealthCheckBatch] Waiting for EOSHealthCheck type...");
                return false;
            }

            var instance = UnityEngine.Object.FindAnyObjectByType(_hcType);
            if (instance == null)
            {
                // AutoCreate may not fire in batch mode — create it ourselves.
                // Look for EOSNativeTransport or EOSManager to attach to.
                var transportType = FindType("FishNet.Transport.EOSNative.EOSNativeTransport");
                var eosManagerType = FindType("EOSNative.EOSManager");

                GameObject host = null;
                if (transportType != null)
                {
                    var allObjects = UnityEngine.Object.FindObjectsByType(transportType, FindObjectsSortMode.None);
                    if (allObjects.Length > 0)
                        host = (allObjects[0] as Component)?.gameObject;
                }
                if (host == null && eosManagerType != null)
                {
                    var allObjects = UnityEngine.Object.FindObjectsByType(eosManagerType, FindObjectsSortMode.None);
                    if (allObjects.Length > 0)
                        host = (allObjects[0] as Component)?.gameObject;
                }

                if (host == null)
                {
                    Debug.Log("[EOSHealthCheckBatch] Waiting for EOSNativeTransport or EOSManager...");
                    return false;
                }

                Debug.Log($"[EOSHealthCheckBatch] Creating EOSHealthCheck on '{host.name}'");
                instance = host.AddComponent(_hcType);
            }

            _healthCheck = instance;

            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            _isRunningField = _hcType.GetField("_isRunning", flags);
            _stepsField = _hcType.GetField("_steps", flags);
            _passField = _hcType.GetField("_passCount", flags);
            _failField = _hcType.GetField("_failCount", flags);
            _skipField = _hcType.GetField("_skipCount", flags);
            _testModeField = _hcType.GetField("_testMode", flags);

            // Set test mode
            var testModeType = _hcType.GetNestedType("TestMode");
            if (testModeType != null && Enum.IsDefined(testModeType, mode))
            {
                var modeValue = Enum.Parse(testModeType, mode);
                _testModeField.SetValue(instance, modeValue);
            }

            // Trigger
            var runMethod = _hcType.GetMethod("RunHealthCheck", BindingFlags.Public | BindingFlags.Instance);
            runMethod.Invoke(instance, null);
            _triggered = true;

            Debug.Log($"[EOSHealthCheckBatch] Triggered RunHealthCheck (mode={mode})");
            return true;
        }

        private static void WriteResults(string resultPath, string mode, double elapsed, bool completed)
        {
            int pass = (int)_passField.GetValue(_healthCheck);
            int fail = (int)_failField.GetValue(_healthCheck);
            int skip = (int)_skipField.GetValue(_healthCheck);

            var stepsJson = BuildStepsJson();
            bool success = completed && fail == 0;

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"success\":{(success ? "true" : "false")},");
            sb.Append($"\"mode\":\"{Esc(mode)}\",");
            sb.Append($"\"completed\":{(completed ? "true" : "false")},");
            sb.Append($"\"passCount\":{pass},");
            sb.Append($"\"failCount\":{fail},");
            sb.Append($"\"skipCount\":{skip},");
            sb.Append($"\"elapsedSeconds\":{elapsed:F1},");
            sb.Append($"\"steps\":{stepsJson}");
            sb.Append("}");

            File.WriteAllText(resultPath, sb.ToString());
            Debug.Log($"[EOSHealthCheckBatch] Results: {pass} pass, {fail} fail, {skip} skip → {resultPath}");
        }

        private static void WriteTimeout(string resultPath, string mode, float timeout, double elapsed)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"success\":false,");
            sb.Append($"\"mode\":\"{Esc(mode)}\",");
            sb.Append("\"completed\":false,");
            sb.Append($"\"error\":\"Timed out after {timeout}s — EOSHealthCheck not found or did not complete\",");
            sb.Append("\"passCount\":0,\"failCount\":0,\"skipCount\":0,");
            sb.Append($"\"elapsedSeconds\":{elapsed:F1},");
            sb.Append("\"steps\":[]");
            sb.Append("}");

            File.WriteAllText(resultPath, sb.ToString());
        }

        private static string BuildStepsJson()
        {
            var stepsList = _stepsField.GetValue(_healthCheck) as System.Collections.IList;
            if (stepsList == null || stepsList.Count == 0)
                return "[]";

            var stepType = _hcType.GetNestedType("Step");
            var nameField = stepType.GetField("Name");
            var statusField = stepType.GetField("Status");
            var detailField = stepType.GetField("Detail");
            var elapsedField = stepType.GetField("ElapsedMs");

            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < stepsList.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var step = stepsList[i];
                sb.Append("{");
                sb.Append($"\"name\":\"{Esc(nameField.GetValue(step) as string)}\",");
                sb.Append($"\"status\":\"{statusField.GetValue(step)}\",");
                sb.Append($"\"detail\":\"{Esc(detailField.GetValue(step) as string)}\",");
                sb.Append($"\"elapsedMs\":{elapsedField.GetValue(step)}");
                sb.Append("}");
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static void Finish(int exitCode)
        {
            // Clean up persisted state
            EditorPrefs.DeleteKey(PREF_ACTIVE);
            EditorPrefs.DeleteKey(PREF_MODE);
            EditorPrefs.DeleteKey(PREF_TIMEOUT);
            EditorPrefs.DeleteKey(PREF_START_TIME);
            EditorPrefs.DeleteKey(PREF_RESULT_PATH);

            EditorApplication.update -= OnUpdate;
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(exitCode);
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }

        private static string Esc(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
#endif
