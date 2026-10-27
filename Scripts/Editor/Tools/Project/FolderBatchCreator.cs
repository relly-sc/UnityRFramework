using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 在 Assets 内的选中目录下按逐行相对路径批量创建文件夹。
    /// </summary>
    internal sealed class FolderBatchCreator : EditorWindow
    {
        [SerializeField]
        private string relativePaths =
            "Editor\nPrefabs\nScripts\nScenes\nResources";

        [SerializeField]
        private string resultText = string.Empty;

        private string baseFolder = "Assets";

        /// <summary>
        /// 从 Project 右键菜单打开批量创建文件夹窗口。
        /// </summary>
        [MenuItem("Assets/UnityRFramework/批量创建文件夹", false, -999)]
        private static void OpenFromAssetsMenu()
        {
            Open();
        }

        /// <summary>
        /// 判断当前 Project 选择是否可以作为目录。
        /// </summary>
        /// <returns>选择位于 Assets 内时返回 true。</returns>
        [MenuItem("Assets/UnityRFramework/批量创建文件夹", true)]
        private static bool ValidateOpenFromAssetsMenu()
        {
            return true;
        }

        private static void Open()
        {
            FolderBatchCreator window =
                GetWindow<FolderBatchCreator>("批量创建文件夹");
            window.minSize = new Vector2(520f, 360f);
            window.baseFolder = GetSelectedBaseFolder();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("目标目录", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(
                baseFolder,
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(
                "每行一个相对路径，可使用 / 创建多级目录",
                EditorStyles.wordWrappedLabel);
            relativePaths = EditorGUILayout.TextArea(
                relativePaths,
                GUILayout.MinHeight(150f),
                GUILayout.ExpandHeight(true));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新目标", GUILayout.Height(28f)))
                {
                    baseFolder = GetSelectedBaseFolder();
                }

                if (GUILayout.Button("创建", GUILayout.Height(28f)))
                {
                    CreateFolders();
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("结果", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                string.IsNullOrEmpty(resultText)
                    ? "尚未执行。"
                    : resultText,
                MessageType.Info);
        }

        private void CreateFolders()
        {
            try
            {
                IReadOnlyList<string> paths =
                    ParseRelativePaths(relativePaths);
                if (paths.Count == 0)
                {
                    throw new InvalidOperationException(
                        "没有可创建的文件夹路径。");
                }

                string lastFolder = baseFolder;
                int createdCount = 0;
                for (int i = 0; i < paths.Count; i++)
                {
                    lastFolder = CreatePath(
                        baseFolder,
                        paths[i],
                        ref createdCount);
                }

                AssetDatabase.Refresh();
                UnityEngine.Object folder =
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        lastFolder);
                if (folder != null)
                {
                    Selection.activeObject = folder;
                    EditorGUIUtility.PingObject(folder);
                }

                resultText =
                    $"完成：新建 {createdCount} 个文件夹，"
                    + $"处理 {paths.Count} 条路径。";
            }
            catch (Exception ex)
            {
                resultText = ex.Message;
                Debug.LogException(ex);
            }
        }

        private static IReadOnlyList<string> ParseRelativePaths(string value)
        {
            string[] lines = (value ?? string.Empty).Replace("\r", string.Empty)
                .Split('\n');
            List<string> result = new List<string>();
            HashSet<string> unique =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim().Replace('\\', '/').Trim('/');
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                ValidateRelativePath(line);
                if (unique.Add(line))
                {
                    result.Add(line);
                }
            }

            return result;
        }

        private static void ValidateRelativePath(string path)
        {
            string[] segments = path.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i].Trim();
                if (string.IsNullOrEmpty(segment)
                    || segment == "."
                    || segment == ".."
                    || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new InvalidOperationException(
                        $"文件夹路径无效：'{path}'。");
                }
            }
        }

        private static string CreatePath(
            string root,
            string relativePath,
            ref int createdCount)
        {
            string current = root.Replace('\\', '/').TrimEnd('/');
            string[] segments = relativePath.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i].Trim();
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(
                        current,
                        segments[i].Trim());
                    if (string.IsNullOrEmpty(guid))
                    {
                        throw new InvalidOperationException(
                            $"创建文件夹失败：'{next}'。");
                    }

                    createdCount++;
                }

                current = next;
            }

            return current;
        }

        private static string GetSelectedBaseFolder()
        {
            UnityEngine.Object active = Selection.activeObject;
            if (active == null)
            {
                return "Assets";
            }

            string path = AssetDatabase.GetAssetPath(active);
            if (!AssetDatabase.IsValidFolder(path))
            {
                path = Path.GetDirectoryName(path)?.Replace('\\', '/');
            }

            return !string.IsNullOrEmpty(path)
                && AssetDatabase.IsValidFolder(path)
                && (path == "Assets"
                    || path.StartsWith(
                        "Assets/",
                        StringComparison.Ordinal))
                ? path
                : "Assets";
        }
    }
}
