using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 对 Project 当前选择范围内的纹理批量应用 TextureImporter 设置。
    /// </summary>
    internal sealed class TextureImportBatchWindow : EditorWindow
    {
        [SerializeField]
        private TextureImporterType textureType =
            TextureImporterType.Sprite;

        [SerializeField]
        private SpriteImportMode spriteMode =
            SpriteImportMode.Single;

        [SerializeField]
        private bool isReadable;

        [SerializeField]
        private bool mipmapEnabled;

        [SerializeField]
        private int defaultMaxSize = 2048;

        [SerializeField]
        private bool overrideStandalone;

        [SerializeField]
        private int standaloneMaxSize = 2048;

        [SerializeField]
        private bool overrideAndroid;

        [SerializeField]
        private int androidMaxSize = 2048;

        [SerializeField]
        private bool overrideWebGL;

        [SerializeField]
        private int webGLMaxSize = 2048;

        private readonly List<string> texturePaths =
            new List<string>();

        private Vector2 scrollPosition;
        private string resultText = string.Empty;

        /// <summary>
        /// 从 Project 右键菜单打开纹理批量设置窗口。
        /// </summary>
        [MenuItem("Assets/UnityRFramework/纹理批量设置", false, -998)]
        private static void OpenFromAssetsMenu()
        {
            Open();
        }

        private static void Open()
        {
            TextureImportBatchWindow window =
                GetWindow<TextureImportBatchWindow>("纹理批量设置");
            window.minSize = new Vector2(620f, 600f);
            window.ScanSelection();
            window.Show();
        }

        private void OnSelectionChange()
        {
            ScanSelection();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("通用设置", EditorStyles.boldLabel);
            textureType = (TextureImporterType)EditorGUILayout.EnumPopup(
                "Texture Type", textureType);
            using (new EditorGUI.DisabledScope(
                textureType != TextureImporterType.Sprite))
            {
                spriteMode = (SpriteImportMode)EditorGUILayout.EnumPopup(
                    "Sprite Mode", spriteMode);
            }

            isReadable = EditorGUILayout.Toggle(
                "Read/Write Enabled", isReadable);
            mipmapEnabled = EditorGUILayout.Toggle(
                "Generate Mip Maps", mipmapEnabled);
            defaultMaxSize = DrawMaxSize(
                "Default Max Size", defaultMaxSize);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("平台设置", EditorStyles.boldLabel);
            DrawPlatform(
                "Standalone",
                ref overrideStandalone,
                ref standaloneMaxSize);
            DrawPlatform(
                "Android",
                ref overrideAndroid,
                ref androidMaxSize);
            DrawPlatform(
                "WebGL",
                ref overrideWebGL,
                ref webGLMaxSize);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重新扫描", GUILayout.Height(28f)))
                {
                    ScanSelection();
                }

                using (new EditorGUI.DisabledScope(
                    texturePaths.Count == 0))
                {
                    if (GUILayout.Button(
                        $"应用到 {texturePaths.Count} 个纹理",
                        GUILayout.Height(28f)))
                    {
                        Apply();
                    }
                }
            }

            EditorGUILayout.LabelField("纹理列表", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < texturePaths.Count; i++)
            {
                EditorGUILayout.LabelField(texturePaths[i]);
            }

            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(resultText))
            {
                EditorGUILayout.HelpBox(resultText, MessageType.Info);
            }
        }

        private void ScanSelection()
        {
            texturePaths.Clear();
            HashSet<string> paths =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            UnityEngine.Object[] selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(selected[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    string[] guids = AssetDatabase.FindAssets(
                        "t:Texture2D",
                        new[] { path });
                    for (int guidIndex = 0;
                         guidIndex < guids.Length;
                         guidIndex++)
                    {
                        AddTexturePath(
                            AssetDatabase.GUIDToAssetPath(
                                guids[guidIndex]),
                            paths);
                    }
                }
                else
                {
                    AddTexturePath(path, paths);
                }
            }

            texturePaths.AddRange(paths);
            texturePaths.Sort(StringComparer.OrdinalIgnoreCase);
            resultText = string.Empty;
        }

        private void Apply()
        {
            if (texturePaths.Count == 0
                || !EditorUtility.DisplayDialog(
                    "纹理批量设置",
                    $"确认把当前设置应用到 {texturePaths.Count} 个纹理？"
                    + "\n发生变化的纹理会重新导入。",
                    "应用",
                    "取消"))
            {
                return;
            }

            List<string> changedPaths = new List<string>();
            for (int i = 0; i < texturePaths.Count; i++)
            {
                string path = texturePaths[i];
                TextureImporter importer =
                    AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                Undo.RecordObject(importer, "Batch Texture Import Settings");
                if (!ApplySettings(importer))
                {
                    continue;
                }

                EditorUtility.SetDirty(importer);
                AssetDatabase.WriteImportSettingsIfDirty(path);
                changedPaths.Add(path);
            }

            for (int i = 0; i < changedPaths.Count; i++)
            {
                AssetDatabase.ImportAsset(
                    changedPaths[i],
                    ImportAssetOptions.ForceUpdate);
            }

            resultText =
                $"完成：{changedPaths.Count}/{texturePaths.Count} 个纹理设置发生变化。";
        }

        private bool ApplySettings(TextureImporter importer)
        {
            bool changed = false;
            changed |= SetValue(
                importer.textureType,
                textureType,
                value => importer.textureType = value);
            if (textureType == TextureImporterType.Sprite)
            {
                changed |= SetValue(
                    importer.spriteImportMode,
                    spriteMode,
                    value => importer.spriteImportMode = value);
            }

            changed |= SetValue(
                importer.isReadable,
                isReadable,
                value => importer.isReadable = value);
            changed |= SetValue(
                importer.mipmapEnabled,
                mipmapEnabled,
                value => importer.mipmapEnabled = value);
            changed |= SetValue(
                importer.maxTextureSize,
                defaultMaxSize,
                value => importer.maxTextureSize = value);
            changed |= ApplyPlatform(
                importer,
                "Standalone",
                overrideStandalone,
                standaloneMaxSize);
            changed |= ApplyPlatform(
                importer,
                "Android",
                overrideAndroid,
                androidMaxSize);
            changed |= ApplyPlatform(
                importer,
                "WebGL",
                overrideWebGL,
                webGLMaxSize);
            return changed;
        }

        private static bool ApplyPlatform(
            TextureImporter importer,
            string platform,
            bool overridden,
            int maxSize)
        {
            TextureImporterPlatformSettings settings =
                importer.GetPlatformTextureSettings(platform);
            if (settings.overridden == overridden
                && (!overridden || settings.maxTextureSize == maxSize))
            {
                return false;
            }

            settings.name = platform;
            settings.overridden = overridden;
            if (overridden)
            {
                settings.maxTextureSize = maxSize;
            }

            importer.SetPlatformTextureSettings(settings);
            return true;
        }

        private static bool SetValue<T>(
            T current,
            T next,
            Action<T> setter)
        {
            if (EqualityComparer<T>.Default.Equals(current, next))
            {
                return false;
            }

            setter(next);
            return true;
        }

        private static void AddTexturePath(
            string path,
            HashSet<string> paths)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter)
            {
                paths.Add(path);
            }
        }

        private static int DrawMaxSize(string label, int value)
        {
            int[] sizes =
            {
                32, 64, 128, 256, 512, 1024, 2048, 4096, 8192
            };
            string[] labels =
            {
                "32", "64", "128", "256", "512",
                "1024", "2048", "4096", "8192"
            };
            return EditorGUILayout.IntPopup(label, value, labels, sizes);
        }

        private static void DrawPlatform(
            string platform,
            ref bool overridden,
            ref int maxSize)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                overridden = EditorGUILayout.ToggleLeft(
                    "Override " + platform,
                    overridden,
                    GUILayout.Width(190f));
                using (new EditorGUI.DisabledScope(!overridden))
                {
                    maxSize = DrawMaxSize("Max Size", maxSize);
                }
            }
        }
    }
}
