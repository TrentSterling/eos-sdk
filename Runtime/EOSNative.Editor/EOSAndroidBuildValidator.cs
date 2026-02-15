#if UNITY_ANDROID || UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace EOSNative.Editor
{
    /// <summary>
    /// Android Build Validator window for EOS SDK.
    /// Checks gradle templates, desugaring config, SDK versions, and permissions
    /// BEFORE building — catches issues that would otherwise surface as cryptic gradle errors.
    /// </summary>
    public class EOSAndroidBuildValidator : EditorWindow
    {
        #region Types

        private enum CheckStatus { Pass, Warning, Fail, Info }

        private class Check
        {
            public string Name;
            public CheckStatus Status;
            public string Detail;
            public string Fix; // null = no auto-fix available
            public System.Action AutoFix; // null = manual fix only
        }

        #endregion

        #region State

        private readonly List<Check> _checks = new();
        private Vector2 _scrollPos;
        private bool _hasRun;
        private bool _showAdvanced;

        // Cached paths
        private string _pluginsAndroidPath;
        private string _projectSettingsPath;

        // Styles
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _detailStyle;
        private GUIStyle _fixStyle;
        private GUIStyle _instructionStyle;
        private bool _stylesInitialized;

        // Colors
        private static readonly Color Green = new(0.2f, 0.9f, 0.2f);
        private static readonly Color Yellow = new(1f, 0.85f, 0.1f);
        private static readonly Color Red = new(1f, 0.25f, 0.2f);
        private static readonly Color Cyan = new(0.3f, 0.85f, 1f);
        private static readonly Color Gray = new(0.6f, 0.6f, 0.6f);

        // Desugaring version adapts to Unity/AGP version (matches EOSAndroidBuildProcessor)
        private static string DesugarVersion
        {
            get
            {
#if UNITY_6000_1_OR_NEWER
                return "2.1.4";
#elif UNITY_6000_0_OR_NEWER
                return "2.0.4";
#else
                return "1.2.3";
#endif
            }
        }

        private static string JavaVersionString
        {
            get
            {
#if UNITY_6000_1_OR_NEWER
                return "VERSION_17";
#elif UNITY_6000_0_OR_NEWER
                return "VERSION_11";
#else
                return "VERSION_1_8";
#endif
            }
        }

        #endregion

        #region Menu

        [MenuItem("Tools/EOS SDK/Android Build Validator", priority = 52)]
        public static void ShowWindow()
        {
            var window = GetWindow<EOSAndroidBuildValidator>("EOS Android Validator");
            window.minSize = new Vector2(550, 400);
            window.Show();
            window.RunValidation();
        }

        #endregion

        #region Lifecycle

        private void OnEnable()
        {
            _pluginsAndroidPath = Path.Combine(Application.dataPath, "Plugins", "Android");
            _projectSettingsPath = Path.Combine(Application.dataPath, "..", "ProjectSettings");
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                margin = new RectOffset(0, 0, 8, 8)
            };
            _headerStyle.normal.textColor = Cyan;

            _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(0, 0, 10, 4)
            };

            _detailStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                margin = new RectOffset(20, 0, 0, 2),
                richText = true
            };

            _fixStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                margin = new RectOffset(20, 0, 0, 4),
                richText = true
            };
            _fixStyle.normal.textColor = Yellow;

            _instructionStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                margin = new RectOffset(0, 0, 0, 8),
                padding = new RectOffset(8, 8, 4, 4)
            };

            _stylesInitialized = true;
        }

        #endregion

        #region GUI

        private void OnGUI()
        {
            InitStyles();

            EditorGUILayout.LabelField("EOS Android Build Validator", _headerStyle);
            EditorGUILayout.LabelField(
                "Checks your project for common Android build issues with the EOS SDK. " +
                "Run this before building to catch gradle/desugaring errors early.",
                _instructionStyle);

            EditorGUILayout.BeginHorizontal();
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("Run Validation", GUILayout.Height(30)))
            {
                RunValidation();
            }
            GUI.backgroundColor = oldBg;

            if (_hasRun && _checks.Any(c => c.AutoFix != null && (c.Status == CheckStatus.Fail || c.Status == CheckStatus.Warning)))
            {
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
                if (GUILayout.Button("Auto-Fix All", GUILayout.Height(30), GUILayout.Width(120)))
                {
                    AutoFixAll();
                }
                GUI.backgroundColor = oldBg;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Generate Gradle Templates button
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("FakeDependency.jar / Desugaring Fix", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "If your Android build fails with 'FakeDependency.jar' transform errors, " +
                "generating custom gradle templates is the most reliable fix. Templates pre-configure " +
                "desugaring, AndroidX deps, and Java compatibility so the build processor doesn't have to inject them.",
                _instructionStyle);
            GUI.backgroundColor = new Color(1f, 0.6f, 0.1f); // Orange
            if (GUILayout.Button("Generate EOS Gradle Templates", GUILayout.Height(28)))
            {
                GenerateAllGradleTemplates(silent: false);
                RunValidation();
            }
            GUI.backgroundColor = oldBg;
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            if (!_hasRun)
            {
                EditorGUILayout.HelpBox("Click 'Run Validation' to check your Android build configuration.", MessageType.Info);
                return;
            }

            // Summary
            int pass = _checks.Count(c => c.Status == CheckStatus.Pass);
            int warn = _checks.Count(c => c.Status == CheckStatus.Warning);
            int fail = _checks.Count(c => c.Status == CheckStatus.Fail);
            int info = _checks.Count(c => c.Status == CheckStatus.Info);

            string summary;
            MessageType summaryType;
            if (fail > 0)
            {
                summary = $"{fail} issue(s) found that will cause build failures. Fix before building.";
                summaryType = MessageType.Error;
            }
            else if (warn > 0)
            {
                summary = $"All critical checks passed. {warn} warning(s) to review.";
                summaryType = MessageType.Warning;
            }
            else
            {
                summary = $"All {pass} checks passed. Your Android build should succeed.";
                summaryType = MessageType.Info;
            }
            EditorGUILayout.HelpBox(summary, summaryType);

            EditorGUILayout.Space(4);

            // Checks list
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (var check in _checks)
            {
                DrawCheck(check);
            }

            EditorGUILayout.Space(10);

            // Advanced: show what the processor will inject
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced: Build Processor Preview", true);
            if (_showAdvanced)
            {
                DrawProcessorPreview();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCheck(Check check)
        {
            EditorGUILayout.BeginHorizontal();

            // Status icon
            Color statusColor = check.Status switch
            {
                CheckStatus.Pass => Green,
                CheckStatus.Warning => Yellow,
                CheckStatus.Fail => Red,
                CheckStatus.Info => Cyan,
                _ => Gray
            };
            string icon = check.Status switch
            {
                CheckStatus.Pass => "+",
                CheckStatus.Warning => "!",
                CheckStatus.Fail => "X",
                CheckStatus.Info => "i",
                _ => "?"
            };

            Color oldColor = GUI.color;
            GUI.color = statusColor;
            EditorGUILayout.LabelField(icon, EditorStyles.boldLabel, GUILayout.Width(16));
            GUI.color = oldColor;

            EditorGUILayout.LabelField(check.Name, EditorStyles.boldLabel);

            // Auto-fix button (show for Fail and Warning)
            if (check.AutoFix != null && (check.Status == CheckStatus.Fail || check.Status == CheckStatus.Warning))
            {
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
                if (GUILayout.Button("Fix", GUILayout.Width(50), GUILayout.Height(18)))
                {
                    check.AutoFix();
                    RunValidation(); // Re-validate after fix
                }
                GUI.backgroundColor = oldBg;
            }

            EditorGUILayout.EndHorizontal();

            // Detail
            EditorGUILayout.LabelField(check.Detail, _detailStyle);

            // Fix hint
            if (!string.IsNullOrEmpty(check.Fix) && check.Status != CheckStatus.Pass)
            {
                EditorGUILayout.LabelField($"Fix: {check.Fix}", _fixStyle);
            }

            EditorGUILayout.Space(2);
        }

        private void DrawProcessorPreview()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("EOSAndroidBuildProcessor will inject at build time:", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Into compileOptions {} block:", EditorStyles.miniLabel);
            EditorGUILayout.TextArea("coreLibraryDesugaringEnabled true", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Into dependencies {} block:", EditorStyles.miniLabel);
            EditorGUILayout.TextArea(
                $"coreLibraryDesugaring 'com.android.tools:desugar_jdk_libs:{DesugarVersion}'\n" +
                "implementation 'androidx.appcompat:appcompat:1.5.1'\n" +
                "implementation 'androidx.constraintlayout:constraintlayout:2.1.4'\n" +
                "implementation 'androidx.security:security-crypto:1.0.0'\n" +
                "implementation 'androidx.browser:browser:1.4.0'",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Into AndroidManifest.xml:", EditorStyles.miniLabel);
            EditorGUILayout.TextArea(
                "android:extractNativeLibs=\"true\"\n" +
                "android.permission.RECORD_AUDIO\n" +
                "android.permission.ACCESS_WIFI_STATE",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Also generates:", EditorStyles.miniLabel);
            EditorGUILayout.TextArea(
                "EOSNativeLoader.java (JNI classloader fix)\n" +
                "proguard-eos.pro (R8 keep rules)\n" +
                "eos_login_protocol_scheme (string resource)",
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Validation

        public void RunValidation()
        {
            _checks.Clear();
            _hasRun = true;

            CheckBuildTarget();
            CheckMinSdkVersion();
            CheckTargetSdkVersion();
            CheckScriptingBackend();
            CheckArchitecture();
            CheckGradleTemplates();
            CheckDesugaringConflicts();
            CheckBuildProcessorExists();
            CheckEOSConfig();
            CheckEOSAarPresent();
            CheckInternetPermission();

            // Log summary
            int fail = _checks.Count(c => c.Status == CheckStatus.Fail);
            int warn = _checks.Count(c => c.Status == CheckStatus.Warning);
            if (fail > 0)
                Debug.LogWarning($"[EOS Android Validator] {fail} FAIL, {warn} WARN — fix issues before building!");
            else if (warn > 0)
                Debug.Log($"[EOS Android Validator] All critical checks passed, {warn} warning(s).");
            else
                Debug.Log($"[EOS Android Validator] All {_checks.Count} checks passed.");
        }

        private void CheckBuildTarget()
        {
            bool isAndroid = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
            _checks.Add(new Check
            {
                Name = "Build Target",
                Status = isAndroid ? CheckStatus.Pass : CheckStatus.Warning,
                Detail = isAndroid
                    ? "Active build target is Android"
                    : $"Active build target is {EditorUserBuildSettings.activeBuildTarget}. Switch to Android to build.",
                Fix = isAndroid ? null : "File > Build Settings > Switch Platform to Android"
            });
        }

        private void CheckMinSdkVersion()
        {
            int minSdk = (int)PlayerSettings.Android.minSdkVersion;
            bool ok = minSdk >= 23;
            _checks.Add(new Check
            {
                Name = "Minimum SDK Version",
                Status = ok ? CheckStatus.Pass : CheckStatus.Fail,
                Detail = $"minSdkVersion = {minSdk}" + (ok ? " (>= 23 required)" : ""),
                Fix = ok ? null : "EOS SDK requires API 23+. Set in Player Settings > Android > Minimum API Level.",
                AutoFix = ok ? null : () =>
                {
                    PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
                    Debug.Log("[EOS Android Validator] Set minSdkVersion to 23");
                }
            });
        }

        private void CheckTargetSdkVersion()
        {
            int targetSdk = (int)PlayerSettings.Android.targetSdkVersion;
            // 0 = "Automatic (highest installed)" in Unity
            bool isAutomatic = targetSdk == 0;

            // Detect if this looks like a Quest project (XR settings, Oculus plugin, etc.)
            bool looksLikeQuest = false;
            string[] xrGuids = AssetDatabase.FindAssets("t:XRGeneralSettings");
            if (xrGuids.Length > 0)
                looksLikeQuest = true;
            // Also check for Oculus/Meta XR packages
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (File.Exists(manifestPath))
            {
                string manifest = File.ReadAllText(manifestPath);
                if (manifest.Contains("com.unity.xr.oculus") || manifest.Contains("com.meta.xr"))
                    looksLikeQuest = true;
            }

            if (isAutomatic)
            {
                // When automatic, Unity picks the highest installed SDK.
                // Unity 6.1+ ships with SDK 35/36 which can cause desugaring issues
                // and Quest may not support the latest API level yet.
                if (looksLikeQuest)
                {
                    _checks.Add(new Check
                    {
                        Name = "Target SDK Version",
                        Status = CheckStatus.Warning,
                        Detail = "targetSdkVersion = Automatic (highest installed). " +
                                 "Quest project detected — Unity 6.1+ may default to API 35/36 which can cause " +
                                 "desugaring/gradle errors. Meta Quest currently requires API 32-34.",
                        Fix = "Set Player Settings > Android > Target API Level to a specific version (32 or 34). " +
                              "API 34 required for new Quest Store submissions after March 2026.",
                        AutoFix = () =>
                        {
                            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
                            Debug.Log("[EOS Android Validator] Set targetSdkVersion to 34 (Android 14, Quest-compatible)");
                        }
                    });
                }
                else
                {
                    _checks.Add(new Check
                    {
                        Name = "Target SDK Version",
                        Status = CheckStatus.Info,
                        Detail = "targetSdkVersion = Automatic (highest installed). " +
                                 "Unity will use the latest SDK. If you hit desugaring errors, " +
                                 "try setting a specific version (32-34)."
                    });
                }
            }
            else if (targetSdk >= 35)
            {
                _checks.Add(new Check
                {
                    Name = "Target SDK Version",
                    Status = looksLikeQuest ? CheckStatus.Fail : CheckStatus.Warning,
                    Detail = $"targetSdkVersion = {targetSdk}. " +
                             (looksLikeQuest
                                 ? "Quest may not support API 35+ yet! This can also cause desugaring gradle errors."
                                 : "API 35+ may cause desugaring issues with some gradle plugin versions."),
                    Fix = "Set targetSdkVersion to 32 or 34 in Player Settings > Android > Target API Level.",
                    AutoFix = () =>
                    {
                        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
                        Debug.Log("[EOS Android Validator] Set targetSdkVersion to 34");
                    }
                });
            }
            else
            {
                string storeNote = targetSdk < 34
                    ? " Note: API 34+ required for new Quest Store submissions after March 2026."
                    : "";
                _checks.Add(new Check
                {
                    Name = "Target SDK Version",
                    Status = CheckStatus.Pass,
                    Detail = $"targetSdkVersion = {targetSdk}.{storeNote}"
                });
            }
        }

        private void CheckScriptingBackend()
        {
            var backend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android);
            bool ok = backend == ScriptingImplementation.IL2CPP;
            _checks.Add(new Check
            {
                Name = "Scripting Backend",
                Status = ok ? CheckStatus.Pass : CheckStatus.Warning,
                Detail = $"Backend: {backend}" + (ok ? "" : " — IL2CPP recommended for release builds"),
                Fix = ok ? null : "Player Settings > Android > Scripting Backend > IL2CPP"
            });
        }

        private void CheckArchitecture()
        {
            var target = PlayerSettings.Android.targetArchitectures;
            bool hasArm64 = (target & AndroidArchitecture.ARM64) != 0;
            bool hasArm32 = (target & AndroidArchitecture.ARMv7) != 0;

            string detail;
            CheckStatus status;

            if (hasArm64 && !hasArm32)
            {
                status = CheckStatus.Pass;
                detail = "ARM64 only — correct for Quest/modern devices";
            }
            else if (hasArm64 && hasArm32)
            {
                status = CheckStatus.Pass;
                detail = "ARM64 + ARMv7 — compatible with older devices too";
            }
            else if (hasArm32 && !hasArm64)
            {
                status = CheckStatus.Warning;
                detail = "ARMv7 only — Quest requires ARM64. Enable ARM64 for Quest builds.";
            }
            else
            {
                status = CheckStatus.Fail;
                detail = "No architecture selected!";
            }

            _checks.Add(new Check
            {
                Name = "Target Architecture",
                Status = status,
                Detail = detail,
                Fix = hasArm64 ? null : "Player Settings > Android > Target Architectures > enable ARM64"
            });
        }

        private void CheckGradleTemplates()
        {
            // Check for custom gradle templates that could conflict with EOSAndroidBuildProcessor
            string[] templateFiles = new[]
            {
                "mainTemplate.gradle",
                "launcherTemplate.gradle",
                "gradleTemplate.properties",
                "settingsTemplate.gradle",
                "baseProjectTemplate.gradle"
            };

            bool anyCustomTemplates = false;
            var issues = new List<string>();

            foreach (var template in templateFiles)
            {
                string path = Path.Combine(_pluginsAndroidPath, template);
                if (File.Exists(path))
                {
                    anyCustomTemplates = true;
                    string content = File.ReadAllText(path);

                    // Check for desugaring conflicts
                    if (template.Contains("main") || template.Contains("launcher"))
                    {
                        CheckTemplateDesugaring(template, content, issues);
                    }

                    // Check gradleTemplate.properties for problematic settings
                    if (template == "gradleTemplate.properties")
                    {
                        if (content.Contains("android.enableCoreLibraryDesugaring=false"))
                        {
                            issues.Add($"{template}: has 'enableCoreLibraryDesugaring=false' — this DISABLES desugaring and will break EOS!");
                        }
                    }
                }
            }

            if (!anyCustomTemplates)
            {
                _checks.Add(new Check
                {
                    Name = "Gradle Templates",
                    Status = CheckStatus.Warning,
                    Detail = "No custom gradle templates found. The build processor injects desugaring at build time, " +
                             "but this can fail on Unity 6.1+ if the generated gradle has no compileOptions block. " +
                             "Custom templates are more reliable — click Fix or use the 'Generate EOS Gradle Templates' button.",
                    Fix = "Generate custom gradle templates with EOS desugaring pre-configured.",
                    AutoFix = () => GenerateAllGradleTemplates(silent: true)
                });
            }
            else if (issues.Count == 0)
            {
                _checks.Add(new Check
                {
                    Name = "Gradle Templates",
                    Status = CheckStatus.Warning,
                    Detail = "Custom gradle templates detected. EOSAndroidBuildProcessor injects AFTER Unity generates " +
                             "from templates, so this usually works — but template overrides could conflict.",
                    Fix = "If you hit gradle errors, try removing custom templates from Assets/Plugins/Android/ " +
                          "and letting the build processor handle everything."
                });
            }
            else
            {
                _checks.Add(new Check
                {
                    Name = "Gradle Templates",
                    Status = CheckStatus.Fail,
                    Detail = "Gradle template conflicts detected:\n" + string.Join("\n", issues.Select(i => $"  - {i}")),
                    Fix = "Fix the template issues above, or delete the conflicting templates and let EOSAndroidBuildProcessor handle it."
                });
            }
        }

        private void CheckTemplateDesugaring(string templateName, string content, List<string> issues)
        {
            // If template already has coreLibraryDesugaringEnabled but sets it to false
            if (Regex.IsMatch(content, @"coreLibraryDesugaringEnabled\s+false"))
            {
                issues.Add($"{templateName}: sets coreLibraryDesugaringEnabled = false — this will break EOS!");
            }

            // If template has a different desugar_jdk_libs version that could conflict
            var versionMatch = Regex.Match(content, @"desugar_jdk_libs:(\d+\.\d+\.\d+)");
            if (versionMatch.Success && versionMatch.Groups[1].Value != DesugarVersion)
            {
                issues.Add($"{templateName}: has desugar_jdk_libs:{versionMatch.Groups[1].Value} " +
                           $"— EOSAndroidBuildProcessor expects {DesugarVersion}. Version mismatch may cause issues.");
            }

            // Check for missing compileOptions block (processor needs it to inject into)
            if (!content.Contains("compileOptions"))
            {
                issues.Add($"{templateName}: no compileOptions block — processor may fail to inject desugaring. " +
                           $"Add: compileOptions {{ sourceCompatibility JavaVersion.{JavaVersionString}; targetCompatibility JavaVersion.{JavaVersionString} }}");
            }
        }

        private void CheckDesugaringConflicts()
        {
            // Check for the specific scenario: custom template that already has desugaring
            // configured differently, which would cause the processor's regex to add duplicates
            string mainTemplatePath = Path.Combine(_pluginsAndroidPath, "mainTemplate.gradle");
            string launcherTemplatePath = Path.Combine(_pluginsAndroidPath, "launcherTemplate.gradle");

            bool mainExists = File.Exists(mainTemplatePath);
            bool launcherExists = File.Exists(launcherTemplatePath);

            if (!mainExists && !launcherExists)
            {
                _checks.Add(new Check
                {
                    Name = "Desugaring Config",
                    Status = CheckStatus.Pass,
                    Detail = "No template overrides — desugaring will be auto-injected by EOSAndroidBuildProcessor at build time."
                });
                return;
            }

            // If templates exist, check if they already have correct desugaring
            var details = new List<string>();
            bool allGood = true;

            if (mainExists)
            {
                string content = File.ReadAllText(mainTemplatePath);
                bool hasDesugarOption = content.Contains("coreLibraryDesugaringEnabled true");
                bool hasDesugarDep = content.Contains("desugar_jdk_libs");

                if (hasDesugarOption && hasDesugarDep)
                    details.Add("mainTemplate.gradle: desugaring already configured correctly");
                else if (!hasDesugarOption && !hasDesugarDep)
                    details.Add("mainTemplate.gradle: no desugaring — processor will inject at build time");
                else
                {
                    details.Add($"mainTemplate.gradle: partial desugaring (option={hasDesugarOption}, dep={hasDesugarDep}) — may conflict");
                    allGood = false;
                }
            }

            if (launcherExists)
            {
                string content = File.ReadAllText(launcherTemplatePath);
                bool hasDesugarOption = content.Contains("coreLibraryDesugaringEnabled true");
                bool hasDesugarDep = content.Contains("desugar_jdk_libs");

                if (hasDesugarOption && hasDesugarDep)
                    details.Add("launcherTemplate.gradle: desugaring already configured correctly");
                else if (!hasDesugarOption && !hasDesugarDep)
                    details.Add("launcherTemplate.gradle: no desugaring — processor will inject at build time");
                else
                {
                    details.Add($"launcherTemplate.gradle: partial desugaring (option={hasDesugarOption}, dep={hasDesugarDep}) — may conflict");
                    allGood = false;
                }
            }

            _checks.Add(new Check
            {
                Name = "Desugaring Config",
                Status = allGood ? CheckStatus.Pass : CheckStatus.Warning,
                Detail = string.Join("\n", details),
                Fix = allGood ? null : "Either add both coreLibraryDesugaringEnabled + desugar_jdk_libs to templates, " +
                      "or remove templates entirely and let the build processor handle it."
            });
        }

        private void CheckBuildProcessorExists()
        {
            // Verify the build processor class exists and will be compiled
            var processorType = System.Type.GetType("EOSNative.Editor.EOSAndroidBuildProcessor, EOSNative.Editor");

            if (processorType != null)
            {
                // Check it implements the right interface
                bool implementsInterface = typeof(UnityEditor.Android.IPostGenerateGradleAndroidProject)
                    .IsAssignableFrom(processorType);

                _checks.Add(new Check
                {
                    Name = "Build Processor",
                    Status = implementsInterface ? CheckStatus.Pass : CheckStatus.Fail,
                    Detail = implementsInterface
                        ? "EOSAndroidBuildProcessor found and implements IPostGenerateGradleAndroidProject (callback order 99)"
                        : "EOSAndroidBuildProcessor found but does NOT implement IPostGenerateGradleAndroidProject!",
                    Fix = implementsInterface ? null : "The build processor class is broken — reinstall com.tront.eos-sdk."
                });
            }
            else
            {
                _checks.Add(new Check
                {
                    Name = "Build Processor",
                    Status = CheckStatus.Fail,
                    Detail = "EOSAndroidBuildProcessor NOT found! This class auto-injects desugaring, AndroidX deps, " +
                             "ProGuard rules, and native lib extraction. Without it, Android builds WILL fail.",
                    Fix = "Ensure com.tront.eos-sdk is installed and the EOSNative.Editor assembly compiles. " +
                          "Check for compile errors in the Console."
                });
            }
        }

        private void CheckEOSConfig()
        {
            var guids = AssetDatabase.FindAssets("t:EOSConfig");
            if (guids.Length == 0)
            {
                _checks.Add(new Check
                {
                    Name = "EOS Config",
                    Status = CheckStatus.Warning,
                    Detail = "No EOSConfig asset found. Android login callbacks require a ClientId for the protocol scheme.",
                    Fix = "Create one via Tools > EOS SDK > Create New Config, or the build processor will use a placeholder."
                });
                return;
            }

            // Check if any config has a ClientId
            bool hasClientId = false;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<EOSConfig>(path);
                if (config != null && !string.IsNullOrEmpty(config.ClientId))
                {
                    hasClientId = true;
                    break;
                }
            }

            _checks.Add(new Check
            {
                Name = "EOS Config",
                Status = hasClientId ? CheckStatus.Pass : CheckStatus.Warning,
                Detail = hasClientId
                    ? $"EOSConfig found with ClientId ({guids.Length} config(s))"
                    : "EOSConfig exists but no ClientId set — Android login redirect won't work.",
                Fix = hasClientId ? null : "Set ClientId in your EOSConfig asset (Tools > EOS SDK > Select Config)."
            });
        }

        private void CheckEOSAarPresent()
        {
            // Search for the EOS SDK AAR file
            var aars = AssetDatabase.FindAssets("eossdk t:DefaultAsset");
            bool found = false;
            string aarPath = null;

            foreach (var guid in aars)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".aar") && path.ToLower().Contains("eossdk"))
                {
                    found = true;
                    aarPath = path;
                    break;
                }
            }

            // Also check by glob in the package
            if (!found)
            {
                // Check common locations
                string[] searchPaths = new[]
                {
                    "Packages/com.tront.eos-sdk",
                    "Assets/Plugins/Android"
                };

                foreach (var searchPath in searchPaths)
                {
                    var results = AssetDatabase.FindAssets("", new[] { searchPath });
                    foreach (var guid in results)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (path.EndsWith(".aar") && path.ToLower().Contains("eossdk"))
                        {
                            found = true;
                            aarPath = path;
                            break;
                        }
                    }
                    if (found) break;
                }
            }

            _checks.Add(new Check
            {
                Name = "EOS SDK AAR",
                Status = found ? CheckStatus.Pass : CheckStatus.Fail,
                Detail = found
                    ? $"Found: {aarPath}"
                    : "eossdk AAR not found! The EOS SDK Android native library is required.",
                Fix = found ? null : "Ensure com.tront.eos-sdk is properly installed with Android platform support."
            });
        }

        private void CheckInternetPermission()
        {
            // Unity adds INTERNET permission by default, but check it's not disabled
            _checks.Add(new Check
            {
                Name = "Internet Permission",
                Status = CheckStatus.Pass,
                Detail = "Unity adds android.permission.INTERNET automatically. " +
                         "RECORD_AUDIO and ACCESS_WIFI_STATE are injected by the build processor."
            });
        }

        #endregion

        #region Gradle Template Generator

        /// <summary>
        /// Write a gradle template file, adapting desugaring/Java versions for current Unity version.
        /// </summary>
        private static void WriteTemplate(string path, string content)
        {
            content = content
                .Replace("desugar_jdk_libs:2.1.4", $"desugar_jdk_libs:{DesugarVersion}")
                .Replace("JavaVersion.VERSION_17", $"JavaVersion.{JavaVersionString}");
            File.WriteAllText(path, content);
        }

        /// <summary>
        /// Generate all 4 gradle template files with EOS desugaring pre-configured.
        /// Templates use Unity's **VARIABLE** placeholders for portability.
        /// </summary>
        public static void GenerateAllGradleTemplates(bool silent = false)
        {
            string androidDir = Path.Combine(Application.dataPath, "Plugins", "Android");

            if (!Directory.Exists(androidDir))
                Directory.CreateDirectory(androidDir);

            // Delete ALL old gradle template files first to ensure clean state
            string[] templateFileNames = new[]
            {
                "mainTemplate.gradle",
                "launcherTemplate.gradle",
                "gradleTemplate.properties",
                "settingsTemplate.gradle",
                "baseProjectTemplate.gradle" // legacy Unity template, clean it up too
            };
            int deleted = 0;
            foreach (var fileName in templateFileNames)
            {
                string filePath = Path.Combine(androidDir, fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    // Also delete .meta file
                    string metaPath = filePath + ".meta";
                    if (File.Exists(metaPath))
                        File.Delete(metaPath);
                    deleted++;
                }
            }
            if (deleted > 0)
                Debug.Log($"[EOS Android Validator] Deleted {deleted} old gradle template(s)");

            GenerateMainTemplate(androidDir);
            GenerateLauncherTemplate(androidDir);
            GenerateGradleProperties(androidDir);
            GenerateSettingsTemplate(androidDir);

            AssetDatabase.Refresh();
            Debug.Log("[EOS Android Validator] Generated 4 gradle templates in Assets/Plugins/Android/");
        }

        private static bool IsUnity6_1OrNewer()
        {
#if UNITY_6000_1_OR_NEWER
            return true;
#else
            return false;
#endif
        }

        private static void GenerateMainTemplate(string androidDir)
        {
            string path = Path.Combine(androidDir, "mainTemplate.gradle");
            if (IsUnity6_1OrNewer())
            {
                WriteTemplate(path, @"apply plugin: 'com.android.library'
**APPLY_PLUGINS**

dependencies {
    implementation fileTree(dir: 'libs', include: ['*.jar'])
    coreLibraryDesugaring 'com.android.tools:desugar_jdk_libs:2.1.4'
**DEPS**
}

android {
    ndkPath ""**NDKPATH**""
    ndkVersion ""**NDKVERSION**""

    compileSdk **APIVERSION**
    buildToolsVersion = ""**BUILDTOOLS**""

    compileOptions {
        coreLibraryDesugaringEnabled true
        sourceCompatibility JavaVersion.VERSION_17
        targetCompatibility JavaVersion.VERSION_17
    }

    defaultConfig {
        minSdk **MINSDK**
        targetSdk **TARGETSDK**
        ndk {
            abiFilters **ABIFILTERS**
            debugSymbolLevel **DEBUGSYMBOLLEVEL**
        }
        versionCode **VERSIONCODE**
        versionName '**VERSIONNAME**'
        consumerProguardFiles 'proguard-unity.txt'**USER_PROGUARD**
**DEFAULT_CONFIG_SETUP**
    }

    lint {
        abortOnError false
    }

    androidResources {
        noCompress = **BUILTIN_NOCOMPRESS** + unityStreamingAssets.tokenize(', ')
        ignoreAssetsPattern = ""!.svn:!.git:!.ds_store:!*.scc:!CVS:!thumbs.db:!picasa.ini:!*~""
    }**PACKAGING**
}
**IL_CPP_BUILD_SETUP**
**SOURCE_BUILD_SETUP**
**EXTERNAL_SOURCES**
");
            }
            else
            {
                WriteTemplate(path, @"apply plugin: 'com.android.library'
**APPLY_PLUGINS**

dependencies {
    implementation fileTree(dir: 'libs', include: ['*.jar'])
    coreLibraryDesugaring 'com.android.tools:desugar_jdk_libs:2.1.4'
**DEPS**
}

android {
    ndkPath ""**NDKPATH**""

    compileSdkVersion **APIVERSION**
    buildToolsVersion '**BUILDTOOLS**'

    compileOptions {
        coreLibraryDesugaringEnabled true
        sourceCompatibility JavaVersion.VERSION_17
        targetCompatibility JavaVersion.VERSION_17
    }

    defaultConfig {
        minSdkVersion **MINSDKVERSION**
        targetSdkVersion **TARGETSDKVERSION**
        ndk {
            abiFilters **ABIFILTERS**
        }
        versionCode **VERSIONCODE**
        versionName '**VERSIONNAME**'
        consumerProguardFiles 'proguard-unity.txt'**USER_PROGUARD**
    }

    lintOptions {
        abortOnError false
    }

    aaptOptions {
        noCompress = **BUILTIN_NOCOMPRESS** + unityStreamingAssets.tokenize(', ')
        ignoreAssetsPattern = ""!.svn:!.git:!.ds_store:!*.scc:!CVS:!thumbs.db:!picasa.ini:!*~""
    }**PACKAGING_OPTIONS**
**SPLITS**
**LIBRARY_TARGETS**
**REPOSITORIES**
}
**IL_CPP_BUILD_SETUP**
**SOURCE_BUILD_SETUP**
**EXTERNAL_SOURCES**
");
            }
            Debug.Log("[EOS Android Validator] Generated mainTemplate.gradle");
        }

        private static void GenerateLauncherTemplate(string androidDir)
        {
            string path = Path.Combine(androidDir, "launcherTemplate.gradle");
            if (IsUnity6_1OrNewer())
            {
                WriteTemplate(path, @"apply plugin: 'com.android.application'
**APPLY_PLUGINS**

dependencies {
    implementation project(':unityLibrary')
    coreLibraryDesugaring 'com.android.tools:desugar_jdk_libs:2.1.4'
    implementation 'androidx.appcompat:appcompat:1.5.1'
    implementation 'androidx.constraintlayout:constraintlayout:2.1.4'
    implementation 'androidx.security:security-crypto:1.0.0'
    implementation 'androidx.browser:browser:1.4.0'
}

android {
    ndkPath ""**NDKPATH**""
    ndkVersion ""**NDKVERSION**""

    compileSdk **APIVERSION**
    buildToolsVersion = ""**BUILDTOOLS**""

    compileOptions {
        coreLibraryDesugaringEnabled true
        sourceCompatibility JavaVersion.VERSION_17
        targetCompatibility JavaVersion.VERSION_17
    }

    defaultConfig {
        applicationId '**APPLICATIONID**'
        versionName '**VERSIONNAME**'
        minSdk **MINSDK**
        targetSdk **TARGETSDK**
        versionCode **VERSIONCODE**
        ndk {
            abiFilters **ABIFILTERS**
            debugSymbolLevel **DEBUGSYMBOLLEVEL**
        }
    }

    lint {
        abortOnError false
    }

    androidResources {
        noCompress = **BUILTIN_NOCOMPRESS** + unityStreamingAssets.tokenize(', ')
        ignoreAssetsPattern = ""!.svn:!.git:!.ds_store:!*.scc:!CVS:!thumbs.db:!picasa.ini:!*~""
    }
**SIGN**
    buildTypes {
        debug {
            minifyEnabled **MINIFY_DEBUG**
            proguardFiles getDefaultProguardFile('proguard-android.txt')**SIGNCONFIG**
        }
        release {
            minifyEnabled **MINIFY_RELEASE**
            proguardFiles getDefaultProguardFile('proguard-android.txt')**SIGNCONFIG**
        }
    }

    packaging {
        jniLibs {
            useLegacyPackaging true
        }
    }**PACKAGING**
**SPLITS**
**LAUNCHER_SOURCE_BUILD_SETUP**
}
");
            }
            else
            {
                WriteTemplate(path, @"apply plugin: 'com.android.application'
**APPLY_PLUGINS**

dependencies {
    implementation project(':unityLibrary')
    coreLibraryDesugaring 'com.android.tools:desugar_jdk_libs:2.1.4'
    implementation 'androidx.appcompat:appcompat:1.5.1'
    implementation 'androidx.constraintlayout:constraintlayout:2.1.4'
    implementation 'androidx.security:security-crypto:1.0.0'
    implementation 'androidx.browser:browser:1.4.0'
}

android {
    ndkPath ""**NDKPATH**""

    compileSdkVersion **APIVERSION**
    buildToolsVersion '**BUILDTOOLS**'

    compileOptions {
        coreLibraryDesugaringEnabled true
        sourceCompatibility JavaVersion.VERSION_17
        targetCompatibility JavaVersion.VERSION_17
    }

    defaultConfig {
        minSdkVersion **MINSDKVERSION**
        targetSdkVersion **TARGETSDKVERSION**
        applicationId '**APPLICATIONID**'
        ndk {
            abiFilters **ABIFILTERS**
        }
        versionCode **VERSIONCODE**
        versionName '**VERSIONNAME**'
    }

    aaptOptions {
        noCompress = **BUILTIN_NOCOMPRESS** + unityStreamingAssets.tokenize(', ')
        ignoreAssetsPattern = ""!.svn:!.git:!.ds_store:!*.scc:!CVS:!thumbs.db:!picasa.ini:!*~""
    }

    lintOptions {
        abortOnError false
    }
**SIGN**
    buildTypes {
        debug {
            minifyEnabled **MINIFY_DEBUG**
            proguardFiles getDefaultProguardFile('proguard-android.txt')**SIGNCONFIG**
        }
        release {
            minifyEnabled **MINIFY_RELEASE**
            proguardFiles getDefaultProguardFile('proguard-android.txt')**SIGNCONFIG**
        }
    }

    packagingOptions {
        jniLibs {
            useLegacyPackaging = true
        }
    }**PACKAGING_OPTIONS**
**SPLITS**
**LAUNCHER_TARGETS**
**REPOSITORIES**
}
");
            }
            Debug.Log("[EOS Android Validator] Generated launcherTemplate.gradle");
        }

        private static void GenerateGradleProperties(string androidDir)
        {
            string path = Path.Combine(androidDir, "gradleTemplate.properties");
            File.WriteAllText(path, @"org.gradle.jvmargs=-Xmx**JVM_HEAP_SIZE**M
org.gradle.parallel=true
android.useAndroidX=true
android.enableJetifier=true
unityStreamingAssets=**STREAMING_ASSETS**
**ADDITIONAL_PROPERTIES**
");
            Debug.Log("[EOS Android Validator] Generated gradleTemplate.properties");
        }

        private static void GenerateSettingsTemplate(string androidDir)
        {
            string path = Path.Combine(androidDir, "settingsTemplate.gradle");
            File.WriteAllText(path, @"pluginManagement {
    repositories {
        **ARTIFACTORYREPOSITORY**
        gradlePluginPortal()
        google()
        mavenCentral()
    }
}

include ':launcher', ':unityLibrary'
**INCLUDES**

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.PREFER_SETTINGS)
    repositories {
        **ARTIFACTORYREPOSITORY**
        google()
        mavenCentral()
        flatDir {
            dirs ""${project(':unityLibrary').projectDir}/libs""
        }
    }
}
");
            Debug.Log("[EOS Android Validator] Generated settingsTemplate.gradle");
        }

        #endregion

        #region Auto-Fix

        private void AutoFixAll()
        {
            foreach (var check in _checks)
            {
                if (check.AutoFix != null && (check.Status == CheckStatus.Fail || check.Status == CheckStatus.Warning))
                {
                    check.AutoFix();
                }
            }
            RunValidation();
        }

        #endregion

        #region Headless API (MCP-friendly)

        /// <summary>
        /// Run all validation checks headlessly and return JSON results.
        /// Called via reflection from TrontMCP validate_android_build tool.
        /// </summary>
        public static string RunValidationJson()
        {
            var window = CreateInstance<EOSAndroidBuildValidator>();
            window._pluginsAndroidPath = Path.Combine(Application.dataPath, "Plugins", "Android");
            window._projectSettingsPath = Path.Combine(Application.dataPath, "..", "ProjectSettings");
            window.RunValidation();
            string json = SerializeChecks(window._checks);
            DestroyImmediate(window);
            return json;
        }

        /// <summary>
        /// Auto-fix all fixable issues, re-validate, and return JSON results.
        /// Called via reflection from TrontMCP validate_android_build tool.
        /// </summary>
        public static string AutoFixAllJson()
        {
            var window = CreateInstance<EOSAndroidBuildValidator>();
            window._pluginsAndroidPath = Path.Combine(Application.dataPath, "Plugins", "Android");
            window._projectSettingsPath = Path.Combine(Application.dataPath, "..", "ProjectSettings");
            window.RunValidation();

            // Collect what we fixed
            var fixed_ = new List<string>();
            foreach (var check in window._checks)
            {
                if (check.AutoFix != null && (check.Status == CheckStatus.Fail || check.Status == CheckStatus.Warning))
                {
                    check.AutoFix();
                    fixed_.Add(check.Name);
                }
            }

            // Re-validate after fixes
            window.RunValidation();
            string json = SerializeChecks(window._checks, fixed_);
            DestroyImmediate(window);
            return json;
        }

        /// <summary>
        /// Generate all gradle templates headlessly and return JSON result.
        /// Called via reflection from TrontMCP validate_android_build tool.
        /// </summary>
        public static string GenerateGradleTemplatesJson()
        {
            GenerateAllGradleTemplates(silent: true);
            return "{\"success\":true,\"message\":\"Generated 4 gradle templates in Assets/Plugins/Android/\"}";
        }

        private static string SerializeChecks(List<Check> checks, List<string> fixed_ = null)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"checks\":[");
            for (int i = 0; i < checks.Count; i++)
            {
                var c = checks[i];
                if (i > 0) sb.Append(",");
                sb.Append("{\"name\":\"").Append(EscapeJson(c.Name)).Append("\",");
                sb.Append("\"status\":\"").Append(c.Status.ToString().ToLower()).Append("\",");
                sb.Append("\"detail\":\"").Append(EscapeJson(c.Detail)).Append("\"");
                if (c.Fix != null)
                    sb.Append(",\"fix\":\"").Append(EscapeJson(c.Fix)).Append("\"");
                sb.Append(",\"canAutoFix\":").Append(c.AutoFix != null ? "true" : "false");
                sb.Append("}");
            }
            sb.Append("],");

            int pass = checks.Count(c => c.Status == CheckStatus.Pass);
            int warn = checks.Count(c => c.Status == CheckStatus.Warning);
            int fail = checks.Count(c => c.Status == CheckStatus.Fail);
            sb.Append("\"summary\":{");
            sb.Append("\"pass\":").Append(pass).Append(",");
            sb.Append("\"warn\":").Append(warn).Append(",");
            sb.Append("\"fail\":").Append(fail).Append(",");
            sb.Append("\"total\":").Append(checks.Count).Append("}");

            if (fixed_ != null && fixed_.Count > 0)
            {
                sb.Append(",\"fixed\":[");
                for (int i = 0; i < fixed_.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append("\"").Append(EscapeJson(fixed_[i])).Append("\"");
                }
                sb.Append("]");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        #endregion
    }
}
#endif
