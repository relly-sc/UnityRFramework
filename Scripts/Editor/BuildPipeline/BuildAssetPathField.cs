using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建步骤配置资产中路径字段的选择辅助：弹出目录选择框，并把所选
    /// 绝对路径转换为工程相对路径（正斜杠）写回字段。
    /// 约定框架内路径一律使用 Assets 相对或工程相对的正斜杠路径；
    /// 选择工程外目录时拒绝，防止误写绝对路径导致配置失效。
    /// </summary>
    public static class BuildAssetPathField
    {
        private const float SelectButtonWidth = 52f;

        /// <summary>
        /// 绘制工程内目录字段及选择按钮。
        /// </summary>
        public static void DrawProjectDirectory(
            SerializedProperty property,
            GUIContent label)
        {
            DrawDirectory(property, label, true);
        }

        /// <summary>
        /// 绘制允许工程外目录的字段及选择按钮。
        /// 工程内路径保存为相对路径，工程外路径保存为绝对路径。
        /// </summary>
        public static void DrawDirectory(
            SerializedProperty property,
            GUIContent label,
            bool requireProjectDirectory)
        {
            if (property == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(property, label);
                HandlePathDrop(
                    GUILayoutUtility.GetLastRect(),
                    property,
                    true,
                    string.Empty,
                    requireProjectDirectory);
                if (GUILayout.Button("选择", GUILayout.Width(SelectButtonWidth)))
                {
                    string selected = PickDirectory(
                        property.stringValue,
                        requireProjectDirectory);
                    if (selected != null)
                    {
                        property.stringValue = selected;
                    }
                }
            }
        }

        /// <summary>
        /// 绘制工程内文件字段及选择按钮。
        /// </summary>
        public static void DrawProjectFile(
            SerializedProperty property,
            GUIContent label,
            string extension)
        {
            DrawFile(property, label, extension, true);
        }

        /// <summary>
        /// 绘制文件字段及选择按钮。
        /// </summary>
        public static void DrawFile(
            SerializedProperty property,
            GUIContent label,
            string extension,
            bool requireProjectFile)
        {
            if (property == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(property, label);
                HandlePathDrop(
                    GUILayoutUtility.GetLastRect(),
                    property,
                    false,
                    extension,
                    requireProjectFile);
                if (GUILayout.Button("选择", GUILayout.Width(SelectButtonWidth)))
                {
                    string selected = PickFile(
                        property.stringValue,
                        extension,
                        requireProjectFile);
                    if (selected != null)
                    {
                        property.stringValue = selected;
                    }
                }
            }
        }

        /// <summary>
        /// 弹出目录选择框并返回可写回字段的工程相对路径。
        /// </summary>
        /// <param name="currentAssetPath">字段当前值（工程相对路径，可为空）。</param>
        /// <returns>工程相对路径（正斜杠）；用户取消或路径无效时返回 null。</returns>
        public static string PickProjectDirectory(string currentAssetPath)
        {
            return PickDirectory(currentAssetPath, true);
        }

        /// <summary>
        /// 弹出目录选择框。
        /// </summary>
        /// <param name="currentPath">字段当前路径。</param>
        /// <param name="requireProjectDirectory">是否要求目录位于工程内。</param>
        /// <returns>选择后的路径；取消或无效时返回 null。</returns>
        public static string PickDirectory(
            string currentPath,
            bool requireProjectDirectory)
        {
            string projectRoot = GetProjectRoot();
            string initial = ResolveAbsolute(currentPath, projectRoot);
            string selected = EditorUtility.OpenFolderPanel(
                "选择目录",
                initial,
                string.Empty);
            if (string.IsNullOrEmpty(selected))
            {
                return null;
            }

            string relative = ToProjectRelative(selected, projectRoot);
            if (relative == null)
            {
                if (!requireProjectDirectory)
                {
                    return NormalizePath(selected);
                }

                EditorUtility.DisplayDialog(
                    "路径无效",
                    "所选目录不在工程目录内，请重新选择。",
                    "确定");
                return null;
            }

            if (!relative.StartsWith("Assets/", StringComparison.Ordinal))
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "路径提示",
                    $"所选目录不在 Assets/ 下：{relative}\n\n"
                    + "框架内路径按 Assets 相对路径解析，确定要使用吗？",
                    "仍使用",
                    "取消");
                if (!confirm)
                {
                    return null;
                }
            }

            return relative;
        }

        /// <summary>
        /// 弹出文件选择框并返回工程相对路径。
        /// </summary>
        /// <param name="currentPath">字段当前路径。</param>
        /// <param name="extension">扩展名过滤，不含点；空表示不过滤。</param>
        /// <returns>工程相对路径；取消或选择工程外文件时返回 null。</returns>
        public static string PickProjectFile(
            string currentPath,
            string extension)
        {
            return PickFile(currentPath, extension, true);
        }

        /// <summary>
        /// 弹出文件选择框；工程内文件保存相对路径，允许工程外时保存绝对路径。
        /// </summary>
        public static string PickFile(
            string currentPath,
            string extension,
            bool requireProjectFile)
        {
            string projectRoot = GetProjectRoot();
            string initial = ResolveAbsolute(currentPath, projectRoot);
            string initialDirectory = Directory.Exists(initial)
                ? initial
                : Path.GetDirectoryName(initial);
            string selected = EditorUtility.OpenFilePanel(
                "选择文件",
                string.IsNullOrEmpty(initialDirectory)
                    ? projectRoot
                    : initialDirectory,
                extension ?? string.Empty);
            if (string.IsNullOrEmpty(selected))
            {
                return null;
            }

            string relative = ToProjectRelative(selected, projectRoot);
            if (relative != null)
            {
                return relative;
            }

            if (!requireProjectFile)
            {
                return NormalizePath(selected);
            }

            EditorUtility.DisplayDialog(
                "路径无效",
                "所选文件不在工程目录内，请重新选择。",
                "确定");
            return null;
        }

        /// <summary>
        /// 获取 Unity 工程根目录的绝对路径（含末尾分隔符归一化）。
        /// </summary>
        /// <returns>工程根目录绝对路径。</returns>
        public static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        /// <summary>
        /// 把工程相对路径解析为绝对路径，作为目录选择框的初始位置。
        /// 空值回退到工程根；路径已为绝对时原样返回。
        /// </summary>
        /// <param name="assetPath">工程相对路径，可为空。</param>
        /// <param name="projectRoot">工程根目录绝对路径。</param>
        /// <returns>绝对路径。</returns>
        private static string ResolveAbsolute(string assetPath, string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return projectRoot;
            }

            string normalized = assetPath.Replace('/', Path.DirectorySeparatorChar);
            string combined = Path.IsPathRooted(normalized)
                ? normalized
                : Path.Combine(projectRoot, normalized);
            return Path.GetFullPath(combined);
        }

        /// <summary>
        /// 把绝对路径转换为工程相对路径（正斜杠）；工程外返回 null。
        /// </summary>
        /// <param name="absolute">绝对路径。</param>
        /// <param name="projectRoot">工程根目录绝对路径。</param>
        /// <returns>工程相对路径；工程外时返回 null。</returns>
        private static string ToProjectRelative(string absolute, string projectRoot)
        {
            string full = Path.GetFullPath(absolute);
            string root = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string relative = full.Substring(root.Length);
            return relative.Replace('\\', '/');
        }

        /// <summary>归一化绝对路径分隔符。</summary>
        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }

        /// <summary>
        /// 允许把 Project 视图或文件管理器中的单个文件、目录拖到路径文本框。
        /// 输出目录等尚不存在的路径仍可继续手填或使用原选择按钮。
        /// </summary>
        private static void HandlePathDrop(
            Rect fieldRect,
            SerializedProperty property,
            bool requireDirectory,
            string extension,
            bool requireProjectPath)
        {
            Event current = Event.current;
            if (!fieldRect.Contains(current.mousePosition)
                || (current.type != EventType.DragUpdated
                    && current.type != EventType.DragPerform))
            {
                return;
            }

            string path;
            bool accepted = TryGetDraggedPath(
                requireDirectory,
                extension,
                requireProjectPath,
                out path);
            DragAndDrop.visualMode = accepted
                ? DragAndDropVisualMode.Copy
                : DragAndDropVisualMode.Rejected;

            if (current.type == EventType.DragPerform && accepted)
            {
                DragAndDrop.AcceptDrag();
                property.stringValue = path;
                GUI.changed = true;
            }

            current.Use();
        }

        private static bool TryGetDraggedPath(
            bool requireDirectory,
            string extension,
            bool requireProjectPath,
            out string path)
        {
            path = null;
            UnityEngine.Object[] references = DragAndDrop.objectReferences;
            string draggedPath = null;
            if (references != null && references.Length > 0)
            {
                if (references.Length != 1 || references[0] == null)
                {
                    return false;
                }

                draggedPath = AssetDatabase.GetAssetPath(references[0]);
            }

            if (string.IsNullOrWhiteSpace(draggedPath))
            {
                string[] draggedPaths = DragAndDrop.paths;
                if (draggedPaths == null || draggedPaths.Length != 1)
                {
                    return false;
                }

                draggedPath = draggedPaths[0];
            }

            string projectRoot = GetProjectRoot();
            string absolutePath;
            try
            {
                absolutePath = ResolveAbsolute(draggedPath, projectRoot);
            }
            catch (Exception)
            {
                return false;
            }

            bool isDirectory = Directory.Exists(absolutePath);
            bool isFile = File.Exists(absolutePath);
            if (!isDirectory && !isFile)
            {
                return false;
            }

            if (requireDirectory != isDirectory)
            {
                return false;
            }

            if (!requireDirectory
                && !string.IsNullOrWhiteSpace(extension)
                && !string.Equals(
                    Path.GetExtension(absolutePath).TrimStart('.'),
                    extension.TrimStart('.'),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string relativePath = ToProjectRelative(absolutePath, projectRoot);
            if (requireProjectPath && relativePath == null)
            {
                return false;
            }

            path = relativePath ?? NormalizePath(absolutePath);
            return true;
        }
    }
}
