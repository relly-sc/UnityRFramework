using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 扫描并清理当前选择范围内的 Missing MonoBehaviour 引用。
    /// </summary>
    internal sealed class MissingScriptCleaner : EditorWindow
    {
        private const string ProjectMenuPath =
            "Assets/UnityRFramework/Missing Script 清理";

        private const string HierarchyMenuPath =
            "GameObject/UnityRFramework/Missing Script 清理";

        private readonly List<CleanupTarget> targets =
            new List<CleanupTarget>();

        private Vector2 scrollPosition;
        private string resultText = string.Empty;

        /// <summary>
        /// 从 Project 右键菜单打开 Missing Script 清理窗口。
        /// </summary>
        [MenuItem(ProjectMenuPath, false, -997)]
        private static void OpenFromProject()
        {
            Open();
        }

        /// <summary>
        /// 从 Hierarchy 右键菜单打开 Missing Script 清理窗口。
        /// </summary>
        [MenuItem(HierarchyMenuPath, false, -995)]
        private static void OpenFromHierarchy()
        {
            Open();
        }

        private static void Open()
        {
            MissingScriptCleaner window =
                GetWindow<MissingScriptCleaner>("Missing Script 清理");
            window.minSize = new Vector2(720f, 480f);
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
                "仅扫描当前选择。场景对象支持 Undo；Prefab 资产保存后不进入场景 Undo，"
                + "执行前请确认扫描列表。",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重新扫描", GUILayout.Height(28f)))
                {
                    ScanSelection();
                }

                using (new EditorGUI.DisabledScope(
                    GetTotalMissingCount() == 0))
                {
                    if (GUILayout.Button(
                        "清理扫描结果",
                        GUILayout.Height(28f)))
                    {
                        Clean();
                    }
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                $"扫描结果：{targets.Count} 个目标，"
                + $"{GetTotalMissingCount()} 个 Missing Script",
                EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < targets.Count; i++)
            {
                CleanupTarget target = targets[i];
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
                        target.MissingCount.ToString(),
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

                int count = CountMissingRecursively(root);
                if (count > 0)
                {
                    targets.Add(CleanupTarget.ForScene(
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
                        prefabPaths.Add(
                            AssetDatabase.GUIDToAssetPath(
                                guids[guidIndex]));
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
                if (prefab == null)
                {
                    continue;
                }

                int count = CountMissingRecursively(prefab);
                if (count > 0)
                {
                    targets.Add(CleanupTarget.ForPrefab(
                        prefab,
                        path,
                        count));
                }
            }
        }

        private void Clean()
        {
            int missingCount = GetTotalMissingCount();
            int prefabCount = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].IsPrefab)
                {
                    prefabCount++;
                }
            }

            if (missingCount == 0
                || !EditorUtility.DisplayDialog(
                    "清理 Missing Script",
                    $"将移除 {missingCount} 个 Missing Script，"
                    + $"其中包含 {prefabCount} 个 Prefab 资产。\n"
                    + "Prefab 修改不进入场景 Undo。是否继续？",
                    "清理",
                    "取消"))
            {
                return;
            }

            int removedCount = 0;
            List<string> errors = new List<string>();
            for (int i = 0; i < targets.Count; i++)
            {
                CleanupTarget target = targets[i];
                try
                {
                    removedCount += target.IsPrefab
                        ? CleanPrefab(target.AssetPath)
                        : CleanScene(target.SceneRoot);
                }
                catch (Exception ex)
                {
                    errors.Add(target.DisplayPath + ": " + ex.Message);
                    Debug.LogException(ex);
                }
            }

            AssetDatabase.SaveAssets();
            string summary = errors.Count == 0
                ? $"完成：已移除 {removedCount} 个 Missing Script。"
                : $"已移除 {removedCount} 个，失败 {errors.Count} 个：\n"
                    + string.Join("\n", errors);
            ScanSelection();
            resultText = summary;
        }

        private static int CleanScene(GameObject root)
        {
            if (root == null)
            {
                return 0;
            }

            Undo.RegisterFullObjectHierarchyUndo(
                root,
                "Clean Missing Scripts");
            int count = RemoveMissingRecursively(root);
            EditorUtility.SetDirty(root);
            return count;
        }

        private static int CleanPrefab(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = RemoveMissingRecursively(root);
                if (count > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }

                return count;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int CountMissingRecursively(GameObject root)
        {
            int count =
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            Transform transform = root.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                count += CountMissingRecursively(
                    transform.GetChild(i).gameObject);
            }

            return count;
        }

        private static int RemoveMissingRecursively(GameObject root)
        {
            int count =
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            if (count > 0)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            }

            Transform transform = root.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                count += RemoveMissingRecursively(
                    transform.GetChild(i).gameObject);
            }

            return count;
        }

        private int GetTotalMissingCount()
        {
            int count = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                count += targets[i].MissingCount;
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

        private sealed class CleanupTarget
        {
            private CleanupTarget()
            {
            }

            internal UnityEngine.Object DisplayObject { get; set; }

            internal string DisplayPath { get; set; }

            internal string AssetPath { get; set; }

            internal GameObject SceneRoot { get; set; }

            internal int MissingCount { get; set; }

            internal bool IsPrefab => !string.IsNullOrEmpty(AssetPath);

            internal static CleanupTarget ForScene(
                GameObject root,
                string path,
                int missingCount)
            {
                return new CleanupTarget
                {
                    DisplayObject = root,
                    DisplayPath = path,
                    SceneRoot = root,
                    MissingCount = missingCount
                };
            }

            internal static CleanupTarget ForPrefab(
                GameObject prefab,
                string path,
                int missingCount)
            {
                return new CleanupTarget
                {
                    DisplayObject = prefab,
                    DisplayPath = path,
                    AssetPath = path,
                    MissingCount = missingCount
                };
            }
        }
    }
}
