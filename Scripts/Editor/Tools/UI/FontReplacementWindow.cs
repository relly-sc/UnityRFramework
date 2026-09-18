using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 在当前选择的场景对象和 Prefab 中批量替换 UGUI Text 字体。
    /// </summary>
    internal sealed class FontReplacementWindow : EditorWindow
    {
        private const string ProjectMenuPath =
            "Assets/UnityRFramework/UGUI 字体替换";

        private const string HierarchyMenuPath =
            "GameObject/UnityRFramework/UGUI 字体替换";

        [SerializeField]
        private Font sourceFont;

        [SerializeField]
        private Font replacementFont;

        private readonly List<FontTarget> targets =
            new List<FontTarget>();

        private Vector2 scrollPosition;
        private string resultText = string.Empty;

        /// <summary>
        /// 从 Project 右键菜单打开 UGUI 字体替换窗口。
        /// </summary>
        [MenuItem(ProjectMenuPath, false, -996)]
        private static void OpenFromProject()
        {
            Open();
        }

        /// <summary>
        /// 从 Hierarchy 右键菜单打开 UGUI 字体替换窗口。
        /// </summary>
        [MenuItem(HierarchyMenuPath, false, -994)]
        private static void OpenFromHierarchy()
        {
            Open();
        }

        private static void Open()
        {
            FontReplacementWindow window =
                GetWindow<FontReplacementWindow>("UGUI 字体替换");
            window.minSize = new Vector2(700f, 500f);
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
            EditorGUILayout.HelpBox(
                "默认只处理 UGUI Text。原字体留空表示匹配全部字体；"
                + "不会修改字号、布局或文本内容。",
                MessageType.Info);
            EditorGUI.BeginChangeCheck();
            Font nextSourceFont = (Font)EditorGUILayout.ObjectField(
                "原字体",
                sourceFont,
                typeof(Font),
                false);
            Font nextReplacementFont = (Font)EditorGUILayout.ObjectField(
                "目标字体",
                replacementFont,
                typeof(Font),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                sourceFont = nextSourceFont;
                replacementFont = nextReplacementFont;
                ScanSelection();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重新扫描", GUILayout.Height(28f)))
                {
                    ScanSelection();
                }

                using (new EditorGUI.DisabledScope(
                    replacementFont == null
                    || GetTotalTextCount() == 0))
                {
                    if (GUILayout.Button(
                        $"替换 {GetTotalTextCount()} 个 Text",
                        GUILayout.Height(28f)))
                    {
                        ReplaceFonts();
                    }
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                $"匹配结果：{targets.Count} 个目标，"
                + $"{GetTotalTextCount()} 个 Text",
                EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < targets.Count; i++)
            {
                FontTarget target = targets[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(
                        target.DisplayObject,
                        typeof(UnityEngine.Object),
                        true,
                        GUILayout.Width(220f));
                    EditorGUILayout.LabelField(
                        target.DisplayPath,
                        GUILayout.MinWidth(330f));
                    EditorGUILayout.LabelField(
                        target.TextCount.ToString(),
                        GUILayout.Width(50f));
                }
            }

            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(resultText))
            {
                EditorGUILayout.HelpBox(resultText, MessageType.Info);
            }
        }

        private void ScanSelection()
        {
            targets.Clear();
            resultText = string.Empty;
            CollectSceneTargets();
            CollectPrefabTargets();
            targets.Sort((left, right) =>
                string.Compare(
                    left.DisplayPath,
                    right.DisplayPath,
                    StringComparison.OrdinalIgnoreCase));
        }

        private void CollectSceneTargets()
        {
            GameObject[] selected = Selection.gameObjects;
            HashSet<int> selectedIds = new HashSet<int>();
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] != null
                    && selected[i].scene.IsValid()
                    && !EditorUtility.IsPersistent(selected[i]))
                {
                    selectedIds.Add(selected[i].GetInstanceID());
                }
            }

            for (int i = 0; i < selected.Length; i++)
            {
                GameObject root = selected[i];
                if (root == null
                    || !selectedIds.Contains(root.GetInstanceID())
                    || HasSelectedAncestor(root.transform, selectedIds))
                {
                    continue;
                }

                int count = CountMatchingTexts(root);
                if (count > 0)
                {
                    targets.Add(FontTarget.ForScene(
                        root,
                        GetHierarchyPath(root.transform),
                        count));
                }
            }
        }

        private void CollectPrefabTargets()
        {
            HashSet<string> prefabPaths =
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
                        "t:Prefab",
                        new[] { path });
                    for (int guidIndex = 0;
                         guidIndex < guids.Length;
                         guidIndex++)
                    {
                        string prefabPath =
                            AssetDatabase.GUIDToAssetPath(
                                guids[guidIndex]);
                        if (prefabPath.EndsWith(
                            ".prefab",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            prefabPaths.Add(prefabPath);
                        }
                    }
                }
                else if (path.EndsWith(
                    ".prefab",
                    StringComparison.OrdinalIgnoreCase))
                {
                    prefabPaths.Add(path);
                }
            }

            foreach (string path in prefabPaths)
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);
                int count = prefab == null
                    ? 0
                    : CountMatchingTexts(prefab);
                if (count > 0)
                {
                    targets.Add(FontTarget.ForPrefab(
                        prefab,
                        path,
                        count));
                }
            }
        }

        private void ReplaceFonts()
        {
            int textCount = GetTotalTextCount();
            if (replacementFont == null
                || textCount == 0
                || !EditorUtility.DisplayDialog(
                    "UGUI 字体替换",
                    $"确认替换 {textCount} 个 UGUI Text 的字体？"
                    + "\nPrefab 修改不进入场景 Undo。",
                    "替换",
                    "取消"))
            {
                return;
            }

            int replacedCount = 0;
            List<string> errors = new List<string>();
            for (int i = 0; i < targets.Count; i++)
            {
                FontTarget target = targets[i];
                try
                {
                    replacedCount += target.IsPrefab
                        ? ReplacePrefab(target.AssetPath)
                        : ReplaceScene(target.SceneRoot);
                }
                catch (Exception ex)
                {
                    errors.Add(target.DisplayPath + ": " + ex.Message);
                    Debug.LogException(ex);
                }
            }

            AssetDatabase.SaveAssets();
            string summary = errors.Count == 0
                ? $"完成：已替换 {replacedCount} 个 UGUI Text。"
                : $"已替换 {replacedCount} 个，失败 {errors.Count} 个：\n"
                    + string.Join("\n", errors);
            ScanSelection();
            resultText = summary;
        }

        private int ReplaceScene(GameObject root)
        {
            Text[] texts = GetMatchingTexts(root);
            if (texts.Length == 0)
            {
                return 0;
            }

            Undo.RecordObjects(texts, "Replace UGUI Fonts");
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = replacementFont;
                EditorUtility.SetDirty(texts[i]);
            }

            return texts.Length;
        }

        private int ReplacePrefab(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Text[] texts = GetMatchingTexts(root);
                for (int i = 0; i < texts.Length; i++)
                {
                    texts[i].font = replacementFont;
                }

                if (texts.Length > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }

                return texts.Length;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private int CountMatchingTexts(GameObject root)
        {
            return GetMatchingTexts(root).Length;
        }

        private Text[] GetMatchingTexts(GameObject root)
        {
            Text[] all = root.GetComponentsInChildren<Text>(true);
            List<Text> matching = new List<Text>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                Text text = all[i];
                if ((sourceFont == null || text.font == sourceFont)
                    && text.font != replacementFont)
                {
                    matching.Add(text);
                }
            }

            return matching.ToArray();
        }

        private int GetTotalTextCount()
        {
            int count = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                count += targets[i].TextCount;
            }

            return count;
        }

        private static bool HasSelectedAncestor(
            Transform transform,
            HashSet<int> selectedIds)
        {
            Transform current = transform.parent;
            while (current != null)
            {
                if (selectedIds.Contains(current.gameObject.GetInstanceID()))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return transform.gameObject.scene.name + "/" + path;
        }

        private sealed class FontTarget
        {
            private FontTarget()
            {
            }

            internal UnityEngine.Object DisplayObject { get; set; }

            internal string DisplayPath { get; set; }

            internal string AssetPath { get; set; }

            internal GameObject SceneRoot { get; set; }

            internal int TextCount { get; set; }

            internal bool IsPrefab => !string.IsNullOrEmpty(AssetPath);

            internal static FontTarget ForScene(
                GameObject root,
                string path,
                int textCount)
            {
                return new FontTarget
                {
                    DisplayObject = root,
                    DisplayPath = path,
                    SceneRoot = root,
                    TextCount = textCount
                };
            }

            internal static FontTarget ForPrefab(
                GameObject prefab,
                string path,
                int textCount)
            {
                return new FontTarget
                {
                    DisplayObject = prefab,
                    DisplayPath = path,
                    AssetPath = path,
                    TextCount = textCount
                };
            }
        }
    }
}
