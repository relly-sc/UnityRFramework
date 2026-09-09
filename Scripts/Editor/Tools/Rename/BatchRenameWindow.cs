using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 为 Project 资源与 Hierarchy 对象提供统一的批量重命名预览和执行入口。
    /// </summary>
    internal sealed class BatchRenameWindow : EditorWindow
    {
        [SerializeField]
        private string nameTemplate = "{name}";

        [SerializeField]
        private string findText = string.Empty;

        [SerializeField]
        private string replaceText = string.Empty;

        [SerializeField]
        private int removeStartCharacterCount;

        [SerializeField]
        private int removeEndCharacterCount;

        [SerializeField]
        private string prefix = string.Empty;

        [SerializeField]
        private string suffix = string.Empty;

        [SerializeField]
        private bool autoAppendIndex;

        [SerializeField]
        private string indexSeparator = "_";

        [SerializeField]
        private int startIndex = 1;

        [SerializeField]
        private int indexDigits = 2;

        [SerializeField]
        private int settingsVersion;

        [SerializeField]
        private string resultText = string.Empty;

        private readonly List<RenameEntry> entries =
            new List<RenameEntry>();

        private Vector2 scrollPosition;

        /// <summary>
        /// 从 Project 右键菜单打开批量重命名窗口。
        /// </summary>
        [MenuItem("Assets/UnityRFramework/批量重命名", false, -1000)]
        private static void OpenFromAssetsMenu()
        {
            Open();
        }

        /// <summary>
        /// 从 Hierarchy 右键菜单打开批量重命名窗口。
        /// </summary>
        [MenuItem("GameObject/UnityRFramework/批量重命名", false, -1000)]
        private static void OpenFromGameObjectMenu()
        {
            Open();
        }

        private static void Open()
        {
            BatchRenameWindow window =
                GetWindow<BatchRenameWindow>("批量重命名");
            window.minSize = new Vector2(760f, 500f);
            window.RefreshSelection();
            window.Show();
        }

        private void OnEnable()
        {
            if (settingsVersion < 1)
            {
                autoAppendIndex = true;
                settingsVersion = 1;
            }
        }

        private void OnSelectionChange()
        {
            RefreshSelection();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("命名规则", EditorStyles.boldLabel);
            nameTemplate = EditorGUILayout.TextField(
                new GUIContent(
                    "名称模板",
                    "支持 {name} 和 {index}，例如 Enemy_{index}_{name}。"),
                nameTemplate);
            findText = EditorGUILayout.TextField("查找", findText);
            replaceText = EditorGUILayout.TextField("替换为", replaceText);
            removeStartCharacterCount = Mathf.Max(
                0,
                EditorGUILayout.IntField(
                    new GUIContent(
                        "删除开头字符数",
                        "从原名称开头删除指定数量的字符。"),
                    removeStartCharacterCount));
            removeEndCharacterCount = Mathf.Max(
                0,
                EditorGUILayout.IntField(
                    new GUIContent(
                        "删除末尾字符数",
                        "从原名称末尾删除指定数量的字符，适合清理批量复制后缀。"),
                    removeEndCharacterCount));
            prefix = EditorGUILayout.TextField("前缀", prefix);
            suffix = EditorGUILayout.TextField("后缀", suffix);
            autoAppendIndex = EditorGUILayout.Toggle(
                new GUIContent(
                    "自动追加序号",
                    "名称模板不含 {index} 时，仍在名称末尾追加序号。"),
                autoAppendIndex);
            using (new EditorGUI.DisabledScope(!autoAppendIndex))
            {
                indexSeparator = EditorGUILayout.TextField(
                    new GUIContent(
                        "序号分隔符",
                        "自动追加序号时，名称与序号之间使用的文本。"),
                    indexSeparator);
            }

            startIndex = EditorGUILayout.IntField("起始序号", startIndex);
            indexDigits = EditorGUILayout.IntSlider(
                "序号位数", indexDigits, 1, 8);

            RebuildPreviews();
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                $"预览 ({entries.Count})",
                EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < entries.Count; i++)
            {
                RenameEntry entry = entries[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(
                        entry.Target,
                        typeof(UnityEngine.Object),
                        true,
                        GUILayout.Width(210f));
                    EditorGUILayout.LabelField(
                        entry.OriginalName,
                        GUILayout.MinWidth(130f));
                    EditorGUILayout.LabelField(
                        "->",
                        GUILayout.Width(20f));
                    EditorGUILayout.LabelField(
                        entry.PreviewName,
                        entry.HasError
                            ? EditorStyles.boldLabel
                            : EditorStyles.label,
                        GUILayout.MinWidth(180f));
                    using (new EditorGUI.DisabledScope(entry.HasError))
                    {
                        if (GUILayout.Button(
                            "执行",
                            GUILayout.Width(52f)))
                        {
                            ApplySingleRename(i);
                            GUIUtility.ExitGUI();
                        }
                    }
                }

                if (entry.HasError)
                {
                    EditorGUILayout.HelpBox(
                        entry.Error,
                        MessageType.Error);
                }
            }

            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新选择", GUILayout.Height(28f)))
                {
                    RefreshSelection();
                }

                using (new EditorGUI.DisabledScope(
                    entries.Count == 0 || HasErrors()))
                {
                    if (GUILayout.Button("执行重命名", GUILayout.Height(28f)))
                    {
                        ApplyRename();
                    }
                }
            }

            if (!string.IsNullOrEmpty(resultText))
            {
                EditorGUILayout.HelpBox(resultText, MessageType.Info);
            }
        }

        private void RefreshSelection()
        {
            entries.Clear();
            HashSet<int> objects = new HashSet<int>();
            UnityEngine.Object[] selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                UnityEngine.Object target = selected[i];
                if (target == null || !objects.Add(target.GetInstanceID()))
                {
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(target);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    if (!AssetDatabase.IsMainAsset(target))
                    {
                        continue;
                    }

                    entries.Add(new RenameEntry(
                        target,
                        assetPath,
                        GetAssetSortKey(assetPath)));
                    continue;
                }

                GameObject gameObject = target as GameObject;
                if (gameObject != null && gameObject.scene.IsValid())
                {
                    entries.Add(new RenameEntry(
                        gameObject,
                        null,
                        GetHierarchyPath(gameObject.transform)));
                }
            }

            entries.Sort((left, right) =>
                string.Compare(
                    left.SortKey,
                    right.SortKey,
                    StringComparison.OrdinalIgnoreCase));
            resultText = string.Empty;
            RebuildPreviews();
        }

        private void RebuildPreviews()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                RenameEntry entry = entries[i];
                string baseName = RemoveEdgeCharacters(
                    entry.OriginalName,
                    removeStartCharacterCount,
                    removeEndCharacterCount);
                if (!string.IsNullOrEmpty(findText))
                {
                    baseName = baseName.Replace(
                        findText,
                        replaceText ?? string.Empty);
                }

                string index = (startIndex + i).ToString(
                    "D" + indexDigits);
                string template = string.IsNullOrEmpty(nameTemplate)
                    ? "{name}"
                    : nameTemplate;
                bool templateContainsIndex =
                    template.Contains("{index}");
                string resolvedName = template.Replace("{name}", baseName)
                    .Replace("{index}", index);
                if (autoAppendIndex && !templateContainsIndex)
                {
                    resolvedName +=
                        (indexSeparator ?? string.Empty) + index;
                }

                entry.PreviewName = (prefix ?? string.Empty)
                    + resolvedName
                    + (suffix ?? string.Empty);
                entry.Error = ValidateName(entry, entry.PreviewName);
            }

            ValidateDuplicatePreviews();
        }

        private static string RemoveEdgeCharacters(
            string value,
            int startCount,
            int endCount)
        {
            value = value ?? string.Empty;
            int safeStartCount = Mathf.Clamp(
                startCount,
                0,
                value.Length);
            int remainingLength = value.Length - safeStartCount;
            int safeEndCount = Mathf.Clamp(
                endCount,
                0,
                remainingLength);
            return value.Substring(
                safeStartCount,
                remainingLength - safeEndCount);
        }

        private void ValidateDuplicatePreviews()
        {
            Dictionary<string, RenameEntry> keys =
                new Dictionary<string, RenameEntry>(
                    StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                RenameEntry entry = entries[i];
                if (entry.HasError || !entry.IsAsset)
                {
                    continue;
                }

                string key = Path.GetDirectoryName(entry.AssetPath)
                    ?.Replace('\\', '/')
                    + "/"
                    + entry.PreviewName;
                if (keys.TryGetValue(key, out RenameEntry other))
                {
                    string error =
                        $"重命名后与 '{other.OriginalName}' 冲突。";
                    entry.Error = error;
                    other.Error = error;
                }
                else
                {
                    keys.Add(key, entry);
                }
            }
        }

        private void ApplySingleRename(int entryIndex)
        {
            RebuildPreviews();
            if (entryIndex < 0 || entryIndex >= entries.Count)
            {
                return;
            }

            RenameEntry entry = entries[entryIndex];
            if (entry.HasError)
            {
                resultText = entry.Error;
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rename Selected Item");
            bool succeeded = TryApplyRename(entry, out string error);
            Undo.CollapseUndoOperations(undoGroup);
            if (entry.IsAsset)
            {
                AssetDatabase.SaveAssets();
            }

            string summary = succeeded
                ? $"完成：'{entry.OriginalName}' 已重命名为 "
                    + $"'{entry.PreviewName}'。"
                : $"失败：{entry.OriginalName}: {error}";
            RefreshSelection();
            resultText = summary;
        }

        private void ApplyRename()
        {
            RebuildPreviews();
            if (entries.Count == 0 || HasErrors())
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "批量重命名",
                $"确认重命名 {entries.Count} 个对象？",
                "执行",
                "取消"))
            {
                return;
            }

            int successCount = 0;
            List<string> errors = new List<string>();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Batch Rename");
            for (int i = 0; i < entries.Count; i++)
            {
                RenameEntry entry = entries[i];
                if (TryApplyRename(entry, out string error))
                {
                    successCount++;
                }
                else
                {
                    errors.Add($"{entry.OriginalName}: {error}");
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            string summary = errors.Count == 0
                ? $"完成：已重命名 {successCount} 个对象。"
                : $"完成 {successCount} 个，失败 {errors.Count} 个：\n"
                    + string.Join("\n", errors);
            RefreshSelection();
            resultText = summary;
        }

        private static bool TryApplyRename(
            RenameEntry entry,
            out string error)
        {
            if (entry.IsAsset)
            {
                error = AssetDatabase.RenameAsset(
                    entry.AssetPath,
                    entry.PreviewName);
                return string.IsNullOrEmpty(error);
            }

            Undo.RecordObject(entry.Target, "Rename GameObject");
            entry.Target.name = entry.PreviewName;
            EditorUtility.SetDirty(entry.Target);
            error = null;
            return true;
        }

        private bool HasErrors()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].HasError)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ValidateName(
            RenameEntry entry,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "名称不能为空。";
            }

            if (value.IndexOf('/') >= 0
                || value.IndexOf('\\') >= 0
                || (entry.IsAsset
                    && value.IndexOfAny(
                        Path.GetInvalidFileNameChars()) >= 0))
            {
                return "名称包含非法字符。";
            }

            if (entry.IsAsset)
            {
                string directory =
                    Path.GetDirectoryName(entry.AssetPath)
                        ?.Replace('\\', '/');
                string extension = AssetDatabase.IsValidFolder(entry.AssetPath)
                    ? string.Empty
                    : Path.GetExtension(entry.AssetPath);
                string candidate =
                    directory + "/" + value + extension;
                if (!candidate.Equals(
                        entry.AssetPath,
                        StringComparison.OrdinalIgnoreCase)
                    && AssetDatabase.LoadMainAssetAtPath(candidate) != null)
                {
                    return $"目标资源已存在：'{candidate}'。";
                }
            }

            return null;
        }

        private static string GetAssetSortKey(string path)
        {
            return "A:" + path;
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

            return "H:" + transform.gameObject.scene.path + "/" + path;
        }

        private sealed class RenameEntry
        {
            internal RenameEntry(
                UnityEngine.Object target,
                string assetPath,
                string sortKey)
            {
                Target = target;
                AssetPath = assetPath;
                SortKey = sortKey;
                OriginalName = target.name;
                PreviewName = OriginalName;
            }

            internal UnityEngine.Object Target { get; }

            internal string AssetPath { get; }

            internal string SortKey { get; }

            internal string OriginalName { get; }

            internal string PreviewName { get; set; }

            internal string Error { get; set; }

            internal bool IsAsset => !string.IsNullOrEmpty(AssetPath);

            internal bool HasError => !string.IsNullOrEmpty(Error);
        }
    }
}
