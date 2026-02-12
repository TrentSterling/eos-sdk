using UnityEngine;
using UnityEditor;
using EOSNative.Lobbies;
using EOSNative.Voice;

namespace EOSNative.Editor
{
    /// <summary>
    /// Tools/EOS SDK menu for SDK-level setup and utilities.
    /// Parallel structure to FishNet EOS menu but SDK-only — no networking framework.
    /// </summary>
    public static class EOSNativeMenu
    {
        private const string MenuRoot = "Tools/EOS SDK/";

        #region Scene Setup

        /// <summary>
        /// Sets up the scene with EOSManager and basic subsystems (lobby, voice).
        /// If EOSManager already exists, re-selects it.
        /// </summary>
        [MenuItem(MenuRoot + "Setup Scene", priority = 0)]
        public static void SetupScene()
        {
            var existing = Object.FindAnyObjectByType<EOSManager>();
            if (existing != null)
            {
                Debug.Log("[EOS SDK] EOSManager already exists in scene.");
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            var go = new GameObject("EOSManager");
            Undo.RegisterCreatedObjectUndo(go, "Setup EOS SDK Scene");

            var mgr = go.AddComponent<EOSManager>();

            // Add lobby manager if not present
            if (Object.FindAnyObjectByType<EOSLobbyManager>() == null)
                go.AddComponent<EOSLobbyManager>();

            // Add voice manager if not present
            if (Object.FindAnyObjectByType<EOSVoiceManager>() == null)
                go.AddComponent<EOSVoiceManager>();

            // Try to assign config
            var guids = AssetDatabase.FindAssets("t:EOSConfig");
            if (guids.Length > 0)
            {
                var configPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var config = AssetDatabase.LoadAssetAtPath<EOSConfig>(configPath);
                if (config != null)
                {
                    var so = new SerializedObject(mgr);
                    var prop = so.FindProperty("_config");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = config;
                        so.ApplyModifiedProperties();
                    }
                    Debug.Log($"[EOS SDK] Auto-assigned config: {configPath}");
                }
            }

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            Debug.Log("[EOS SDK] Scene setup complete! EOSManager created with lobby + voice subsystems.");
        }

        #endregion

        #region Config

        [MenuItem(MenuRoot + "Select Config", priority = 1)]
        public static void SelectConfig()
        {
            var guids = AssetDatabase.FindAssets("SampleEOSConfig t:EOSConfig");
            if (guids.Length == 0)
                guids = AssetDatabase.FindAssets("EOSConfig t:EOSConfig");
            if (guids.Length == 0)
                guids = AssetDatabase.FindAssets("t:EOSConfig");

            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var asset = AssetDatabase.LoadAssetAtPath<EOSConfig>(path);
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                    Debug.Log($"[EOS SDK] Selected config: {path}");
                    return;
                }
            }

            if (EditorUtility.DisplayDialog(
                "EOSConfig Not Found",
                "No EOSConfig asset found in the project.\n\nWould you like to create one?",
                "Create Config",
                "Cancel"))
            {
                CreateEOSConfig();
            }
        }

        [MenuItem(MenuRoot + "Create New Config", priority = 2)]
        public static void CreateEOSConfig()
        {
            var config = ScriptableObject.CreateInstance<EOSConfig>();

            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/EOSConfig.asset");
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);

            Debug.Log($"[EOS SDK] Created new EOSConfig at {path}. Configure your EOS credentials in the Inspector.");
        }

        #endregion

        #region Validation

        [MenuItem(MenuRoot + "Validate Setup", priority = 50)]
        public static void ValidateSetup()
        {
            var issues = new System.Collections.Generic.List<string>();
            var warnings = new System.Collections.Generic.List<string>();

            var eosManager = Object.FindAnyObjectByType<EOSManager>();
            if (eosManager == null)
            {
                issues.Add("EOSManager not found in scene");
            }
            else
            {
                var so = new SerializedObject(eosManager);
                var configProp = so.FindProperty("_config");
                if (configProp == null || configProp.objectReferenceValue == null)
                    issues.Add("EOSConfig not assigned on EOSManager");
            }

            if (Object.FindAnyObjectByType<EOSLobbyManager>() == null)
                warnings.Add("EOSLobbyManager not found (required for lobby features)");

            if (Object.FindAnyObjectByType<EOSVoiceManager>() == null)
                warnings.Add("EOSVoiceManager not found (required for voice features)");

            if (issues.Count == 0 && warnings.Count == 0)
            {
                EditorUtility.DisplayDialog("Validation Passed",
                    "EOS SDK components are properly configured!", "OK");
                Debug.Log("[EOS SDK] Validation passed.");
            }
            else
            {
                var message = "";
                if (issues.Count > 0)
                {
                    message += "ERRORS:\n";
                    foreach (var issue in issues)
                    {
                        message += $"  - {issue}\n";
                        Debug.LogError($"[EOS SDK] {issue}");
                    }
                }
                if (warnings.Count > 0)
                {
                    if (issues.Count > 0) message += "\n";
                    message += "WARNINGS:\n";
                    foreach (var warning in warnings)
                    {
                        message += $"  - {warning}\n";
                        Debug.LogWarning($"[EOS SDK] {warning}");
                    }
                }
                message += "\nUse 'Tools > EOS SDK > Setup Scene' to fix.";
                EditorUtility.DisplayDialog("Validation Issues Found", message, "OK");
            }
        }

        [MenuItem(MenuRoot + "Log Platform Info", priority = 51)]
        public static void LogPlatformInfo()
        {
            EOSPlatformHelper.LogPlatformInfo();
        }

        #endregion
    }
}
