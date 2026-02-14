#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace EOSNative.Editor
{
    /// <summary>
    /// Checks GitHub for the latest versions of EOS packages.
    /// Tools > EOS SDK > Check for Updates
    /// </summary>
    public class EOSUpdateChecker : EditorWindow
    {
        private struct PackageInfo
        {
            public string Name;
            public string DisplayName;
            public string Repo;
            public string InstalledVersion;
            public string LatestVersion;
            public bool Fetching;
            public string Error;
        }

        private PackageInfo[] _packages;
        private Vector2 _scrollPos;
        private bool _initialized;

        private static readonly Color Green = new(0.2f, 0.9f, 0.2f);
        private static readonly Color Yellow = new(1f, 0.85f, 0.1f);
        private static readonly Color Cyan = new(0.3f, 0.85f, 1f);
        private static readonly Color Gray = new(0.6f, 0.6f, 0.6f);

        [MenuItem("Tools/EOS SDK/Check for Updates", priority = 53)]
        public static void ShowWindow()
        {
            var window = GetWindow<EOSUpdateChecker>("EOS Update Checker");
            window.minSize = new Vector2(450, 300);
            window.Show();
            window.CheckForUpdates();
        }

        private void OnEnable()
        {
            InitPackages();
        }

        private void InitPackages()
        {
            _packages = new[]
            {
                new PackageInfo
                {
                    Name = "com.tront.eos-sdk",
                    DisplayName = "EOS SDK",
                    Repo = "eos-sdk",
                },
                new PackageInfo
                {
                    Name = "com.tront.fishnet-eos-transport",
                    DisplayName = "FishNet EOS Transport",
                    Repo = "fishnet-eos-transport",
                },
                new PackageInfo
                {
                    Name = "com.tront.eos-mcp",
                    DisplayName = "TrontMCP Unity Plugin",
                    Repo = "trontmcp",
                },
            };

            // Read installed versions
            for (int i = 0; i < _packages.Length; i++)
            {
                _packages[i].InstalledVersion = FindInstalledVersion(_packages[i].Name);
            }
            _initialized = true;
        }

        private void CheckForUpdates()
        {
            if (!_initialized) InitPackages();

            for (int i = 0; i < _packages.Length; i++)
            {
                _packages[i].LatestVersion = null;
                _packages[i].Error = null;
                _packages[i].Fetching = true;
                FetchLatestTag(i);
            }
        }

        private void FetchLatestTag(int index)
        {
            string url = $"https://api.github.com/repos/TrentSterling/{_packages[index].Repo}/tags?per_page=5";
            var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("User-Agent", "EOSUpdateChecker/1.0");
            request.timeout = 10;

            var op = request.SendWebRequest();
            int capturedIndex = index;
            op.completed += _ =>
            {
                if (capturedIndex >= _packages.Length) return;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    _packages[capturedIndex].Error = request.error;
                    _packages[capturedIndex].Fetching = false;
                    Repaint();
                    request.Dispose();
                    return;
                }

                string json = request.downloadHandler.text;
                request.Dispose();

                // Parse version tags using regex — avoids JSON library dependency
                // GitHub returns: [{"name":"v1.4.5",...}, {"name":"v1.4.4",...}]
                var matches = Regex.Matches(json, @"""name""\s*:\s*""v?([^""]+)""");
                string latest = null;
                foreach (Match m in matches)
                {
                    string tag = m.Groups[1].Value;
                    // Take the first valid semver-like tag
                    if (Regex.IsMatch(tag, @"^\d+\.\d+\.\d+"))
                    {
                        latest = tag;
                        break;
                    }
                }

                _packages[capturedIndex].LatestVersion = latest ?? "unknown";
                _packages[capturedIndex].Fetching = false;
                Repaint();
            };
        }

        private static string FindInstalledVersion(string packageName)
        {
            // Check manifest.json for the package reference
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (!File.Exists(manifestPath))
                return "not found";

            string manifest = File.ReadAllText(manifestPath);

            // Check if it's a file: reference
            var fileRefMatch = Regex.Match(manifest, $@"""{Regex.Escape(packageName)}""\s*:\s*""file:([^""]+)""");
            if (fileRefMatch.Success)
            {
                // Read version from the referenced package.json
                string refPath = fileRefMatch.Groups[1].Value;
                string packagesDir = Path.Combine(Application.dataPath, "..", "Packages");
                string packageJsonPath = Path.Combine(packagesDir, refPath, "package.json");
                // Normalize the path
                packageJsonPath = Path.GetFullPath(packageJsonPath);
                if (File.Exists(packageJsonPath))
                {
                    string pkgJson = File.ReadAllText(packageJsonPath);
                    var versionMatch = Regex.Match(pkgJson, @"""version""\s*:\s*""([^""]+)""");
                    if (versionMatch.Success)
                        return versionMatch.Groups[1].Value;
                }
                return "file ref (version unknown)";
            }

            // Check for version reference (e.g. "1.4.5" or git URL)
            var versionRefMatch = Regex.Match(manifest, $@"""{Regex.Escape(packageName)}""\s*:\s*""([^""]+)""");
            if (versionRefMatch.Success)
            {
                string value = versionRefMatch.Groups[1].Value;
                if (Regex.IsMatch(value, @"^\d+\.\d+"))
                    return value;
                return value.Length > 30 ? value.Substring(0, 30) + "..." : value;
            }

            // Check package cache
            string cachePath = Path.Combine(Application.dataPath, "..", "Library", "PackageCache");
            if (Directory.Exists(cachePath))
            {
                foreach (string dir in Directory.GetDirectories(cachePath))
                {
                    string dirName = Path.GetFileName(dir);
                    if (dirName.StartsWith(packageName + "@"))
                    {
                        string version = dirName.Substring(packageName.Length + 1);
                        return version;
                    }
                }
            }

            return "not installed";
        }

        private void OnGUI()
        {
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
            headerStyle.normal.textColor = Cyan;

            EditorGUILayout.LabelField("EOS Package Update Checker", headerStyle);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Height(28), GUILayout.Width(100)))
            {
                CheckForUpdates();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            if (!_initialized || _packages == null)
            {
                EditorGUILayout.HelpBox("Initializing...", MessageType.Info);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = 0; i < _packages.Length; i++)
            {
                DrawPackage(ref _packages[i]);
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Versions are checked against GitHub releases.", EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
        }

        private static void DrawPackage(ref PackageInfo pkg)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(pkg.DisplayName, EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(pkg.LatestVersion) && pkg.LatestVersion != "unknown")
            {
                if (GUILayout.Button("View Releases", GUILayout.Width(100), GUILayout.Height(18)))
                {
                    Application.OpenURL($"https://github.com/TrentSterling/{pkg.Repo}/releases");
                }
            }
            EditorGUILayout.EndHorizontal();

            // Installed version
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Installed:", GUILayout.Width(70));
            string installed = pkg.InstalledVersion ?? "not installed";
            EditorGUILayout.LabelField(installed);
            EditorGUILayout.EndHorizontal();

            // Latest version
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Latest:", GUILayout.Width(70));

            if (pkg.Fetching)
            {
                EditorGUILayout.LabelField("checking...");
            }
            else if (!string.IsNullOrEmpty(pkg.Error))
            {
                Color old = GUI.color;
                GUI.color = Yellow;
                EditorGUILayout.LabelField($"error: {pkg.Error}");
                GUI.color = old;
            }
            else if (!string.IsNullOrEmpty(pkg.LatestVersion))
            {
                // Compare versions
                bool isUpToDate = pkg.InstalledVersion == pkg.LatestVersion;
                bool isNotInstalled = pkg.InstalledVersion == "not installed" ||
                                      pkg.InstalledVersion == "not found";

                Color old = GUI.color;
                if (isNotInstalled)
                    GUI.color = Gray;
                else if (isUpToDate)
                    GUI.color = Green;
                else
                    GUI.color = Yellow;

                string label = pkg.LatestVersion;
                if (!isNotInstalled)
                    label += isUpToDate ? "  (up to date)" : "  (update available)";

                EditorGUILayout.LabelField(label);
                GUI.color = old;
            }
            else
            {
                EditorGUILayout.LabelField("—");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
    }
}
#endif
