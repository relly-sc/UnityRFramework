using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 在 Assets 内创建使用 UTF-8 无 BOM 和 LF 换行的 C# 脚本。
    /// </summary>
    internal sealed class Utf8ScriptCreator : EditorWindow
    {
        private const string AssetsMenuPath = "Assets/UnityRFramework/创建 UTF-8 C# 脚本";

        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        [SerializeField]
        private string targetFolder = "Assets";

        [SerializeField]
        private string scriptName = "NewScript";

        [SerializeField]
        private ScriptKind scriptKind = ScriptKind.Class;

        [SerializeField]
        private string namespaceName = string.Empty;

        [SerializeField]
        private string baseTypeName = string.Empty;

        [SerializeField]
        private bool inheritMonoBehaviour;

        [SerializeField]
        private string resultText = string.Empty;

        /// <summary>
        /// 从 Project 右键菜单打开脚本创建窗口。
        /// </summary>
        [MenuItem(AssetsMenuPath, false, -996)]
        private static void OpenFromAssetsMenu()
        {
            Open(GetSelectedFolder());
        }

        private static void Open(string initialFolder)
        {
            Utf8ScriptCreator window = GetWindow<Utf8ScriptCreator>("创建 UTF-8 C# 脚本");
            window.minSize = new Vector2(520f, 330f);
            window.targetFolder = initialFolder;
            window.Show();
        }

        private void OnSelectionChange()
        {
            string selectedFolder = GetSelectedFolder();
            if (string.Equals(targetFolder, selectedFolder, StringComparison.Ordinal))
            {
                return;
            }

            targetFolder = selectedFolder;
            resultText = string.Empty;
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("脚本设置", EditorStyles.boldLabel);
            DrawTargetFolderField();
            scriptName = EditorGUILayout.TextField(
                new GUIContent("脚本名称", "C# 类型名和文件名，例如 PlayerController。"),
                scriptName);
            scriptKind = (ScriptKind)EditorGUILayout.EnumPopup(
                new GUIContent("类型", "要创建的 C# 类型。"),
                scriptKind);
            namespaceName = EditorGUILayout.TextField(
                new GUIContent("命名空间", "可留空；使用 . 分隔，例如 Company.Product。"),
                namespaceName);

            using (new EditorGUI.DisabledScope(scriptKind != ScriptKind.Class))
            {
                inheritMonoBehaviour = EditorGUILayout.Toggle(
                    new GUIContent(
                        "继承 MonoBehaviour",
                        "启用后自动生成 using UnityEngine 和 : MonoBehaviour。"),
                    inheritMonoBehaviour);

                using (new EditorGUI.DisabledScope(inheritMonoBehaviour))
                {
                    baseTypeName = EditorGUILayout.TextField(
                        new GUIContent("自定义父类", "仅 class 可用。可留空，例如 BaseController。"),
                        baseTypeName);
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "生成文件固定使用 UTF-8（无 BOM）与 LF 换行；不会修改 Unity 安装目录中的脚本模板。",
                MessageType.Info);

            string validationMessage;
            bool canCreate = CanCreate(out validationMessage);
            using (new EditorGUI.DisabledScope(!canCreate))
            {
                if (GUILayout.Button("创建脚本", GUILayout.Height(30f)))
                {
                    CreateScript();
                    GUIUtility.ExitGUI();
                }
            }

            if (!string.IsNullOrEmpty(validationMessage))
            {
                EditorGUILayout.HelpBox(validationMessage, MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(resultText))
            {
                EditorGUILayout.HelpBox(resultText, MessageType.Info);
            }
        }

        private void DrawTargetFolderField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(
                    targetFolder,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("选择目录", GUILayout.Width(82f)))
                {
                    string selected = EditorUtility.OpenFolderPanel(
                        "选择 Assets 内的脚本目录",
                        Application.dataPath,
                        string.Empty);
                    if (!string.IsNullOrEmpty(selected))
                    {
                        targetFolder = ToAssetPath(selected);
                    }
                }
            }
        }

        private void CreateScript()
        {
            if (!CanCreate(out string validationMessage))
            {
                resultText = validationMessage;
                return;
            }

            try
            {
                string assetPath = NormalizeFolder(targetFolder) + "/" + scriptName.Trim() + ".cs";
                string absolutePath = GetAbsoluteAssetPath(assetPath);
                File.WriteAllText(
                    absolutePath,
                    Utf8ScriptTemplateBuilder.Build(
                        scriptName.Trim(),
                        scriptKind,
                        namespaceName.Trim(),
                        baseTypeName.Trim(),
                        inheritMonoBehaviour),
                    Utf8WithoutBom);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                UnityEngine.Object createdAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (createdAsset != null)
                {
                    Selection.activeObject = createdAsset;
                    EditorGUIUtility.PingObject(createdAsset);
                }

                resultText = "已创建：" + assetPath;
            }
            catch (Exception exception)
            {
                resultText = "创建失败：" + exception.Message;
                Debug.LogException(exception);
            }
        }

        private bool CanCreate(out string message)
        {
            string normalizedFolder = NormalizeFolder(targetFolder);
            if (!IsAssetsFolder(normalizedFolder) || !AssetDatabase.IsValidFolder(normalizedFolder))
            {
                message = "请选择 Assets 内已存在的目录。";
                return false;
            }

            if (!Utf8ScriptTemplateBuilder.IsIdentifier(scriptName.Trim()))
            {
                message = "脚本名称必须是合法且非 C# 关键字的标识符。";
                return false;
            }

            if (!Utf8ScriptTemplateBuilder.IsQualifiedIdentifier(namespaceName.Trim()))
            {
                message = "命名空间必须由合法标识符以 . 分隔组成，或保持为空。";
                return false;
            }

            if (scriptKind == ScriptKind.Class
                && !inheritMonoBehaviour
                && !Utf8ScriptTemplateBuilder.IsQualifiedIdentifier(baseTypeName.Trim()))
            {
                message = "父类必须由合法标识符以 . 分隔组成，或保持为空。";
                return false;
            }

            string assetPath = normalizedFolder + "/" + scriptName.Trim() + ".cs";
            if (File.Exists(GetAbsoluteAssetPath(assetPath)))
            {
                message = "目标文件已存在，不会覆盖：" + assetPath;
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static string GetSelectedFolder()
        {
            UnityEngine.Object selected = Selection.activeObject;
            if (selected == null)
            {
                return "Assets";
            }

            string selectedPath = AssetDatabase.GetAssetPath(selected);
            if (AssetDatabase.IsValidFolder(selectedPath))
            {
                return IsAssetsFolder(selectedPath) ? selectedPath : "Assets";
            }

            string directory = Path.GetDirectoryName(selectedPath)?.Replace('\\', '/');
            return IsAssetsFolder(directory) && AssetDatabase.IsValidFolder(directory)
                ? directory
                : "Assets";
        }

        private static string ToAssetPath(string fullPath)
        {
            string assetsPath = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar);
            string selectedPath = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar);
            if (string.Equals(assetsPath, selectedPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets";
            }

            string prefix = assetsPath + Path.DirectorySeparatorChar;
            if (!selectedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets";
            }

            return "Assets/" + selectedPath.Substring(prefix.Length).Replace('\\', '/');
        }

        private static bool IsAssetsFolder(string path)
        {
            return string.Equals(path, "Assets", StringComparison.Ordinal)
                || (!string.IsNullOrEmpty(path)
                    && path.StartsWith("Assets/", StringComparison.Ordinal));
        }

        private static string NormalizeFolder(string folder)
        {
            return (folder ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            string projectPath = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectPath))
            {
                throw new InvalidOperationException("无法确定 Unity 工程根目录。");
            }

            return Path.GetFullPath(Path.Combine(projectPath, assetPath));
        }
    }

    /// <summary>
    /// UTF-8 C# 脚本创建工具支持的类型。
    /// </summary>
    internal enum ScriptKind
    {
        /// <summary>类。</summary>
        Class,

        /// <summary>结构体。</summary>
        Struct,

        /// <summary>接口。</summary>
        Interface,

        /// <summary>枚举。</summary>
        Enum
    }

    /// <summary>
    /// 构建 C# 脚本模板并校验 C# 标识符。
    /// </summary>
    internal static class Utf8ScriptTemplateBuilder
    {
        private static readonly Regex IdentifierPattern = new Regex(
            @"^[_\p{L}][\p{L}\p{Nd}_]*$",
            RegexOptions.CultureInvariant);

        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while", "add", "alias", "ascending", "async", "await", "by", "descending",
            "dynamic", "equals", "from", "get", "global", "group", "into", "join", "let", "nameof",
            "not", "notnull", "on", "or", "orderby", "partial", "remove", "select", "set", "unmanaged",
            "value", "var", "when", "where", "with", "yield"
        };

        /// <summary>
        /// 构建指定类型的最小 C# 源码模板。
        /// </summary>
        /// <param name="typeName">类型名称。</param>
        /// <param name="kind">类型类别。</param>
        /// <param name="namespaceName">可选命名空间。</param>
        /// <param name="baseTypeName">可选父类名称。</param>
        /// <param name="inheritMonoBehaviour">Class 是否继承 MonoBehaviour。</param>
        /// <returns>使用 LF 换行的 C# 源码。</returns>
        public static string Build(
            string typeName,
            ScriptKind kind,
            string namespaceName,
            string baseTypeName,
            bool inheritMonoBehaviour = false)
        {
            string declaration = BuildDeclaration(
                typeName,
                kind,
                inheritMonoBehaviour ? "MonoBehaviour" : baseTypeName);
            StringBuilder builder = new StringBuilder();
            if (kind == ScriptKind.Class && inheritMonoBehaviour)
            {
                builder.Append("using UnityEngine;\n\n");
            }

            if (!string.IsNullOrEmpty(namespaceName))
            {
                builder.Append("namespace ").Append(namespaceName).Append("\n{");
                builder.Append("\n    /// <summary>");
                builder.Append("\n    /// TODO: 描述 ").Append(typeName).Append(" 的用途。");
                builder.Append("\n    /// </summary>");
                builder.Append("\n    ").Append(declaration);
                AppendBody(builder, kind, "    ");
                builder.Append("}\n");
            }
            else
            {
                builder.Append("/// <summary>");
                builder.Append("\n/// TODO: 描述 ").Append(typeName).Append(" 的用途。");
                builder.Append("\n/// </summary>");
                builder.Append("\n").Append(declaration);
                AppendBody(builder, kind, string.Empty);
            }

            return builder.ToString();
        }

        /// <summary>
        /// 判断值是否为合法且非关键字的 C# 标识符。
        /// </summary>
        /// <param name="value">待校验值。</param>
        /// <returns>合法时返回 true。</returns>
        public static bool IsIdentifier(string value)
        {
            return !string.IsNullOrEmpty(value)
                && IdentifierPattern.IsMatch(value)
                && !CSharpKeywords.Contains(value);
        }

        /// <summary>
        /// 判断值是否为由 . 分隔的合法标识符，空值视为合法的可选输入。
        /// </summary>
        /// <param name="value">待校验值。</param>
        /// <returns>合法时返回 true。</returns>
        public static bool IsQualifiedIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            string[] segments = value.Split('.');
            for (int index = 0; index < segments.Length; index++)
            {
                if (!IsIdentifier(segments[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildDeclaration(
            string typeName,
            ScriptKind kind,
            string baseTypeName)
        {
            switch (kind)
            {
                case ScriptKind.Class:
                    return string.IsNullOrEmpty(baseTypeName)
                        ? "public class " + typeName
                        : "public class " + typeName + " : " + baseTypeName;
                case ScriptKind.Struct:
                    return "public struct " + typeName;
                case ScriptKind.Interface:
                    return "public interface " + typeName;
                case ScriptKind.Enum:
                    return "public enum " + typeName;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static void AppendBody(StringBuilder builder, ScriptKind kind, string indent)
        {
            builder.Append("\n").Append(indent).Append("{");
            if (kind == ScriptKind.Enum)
            {
                builder.Append("\n").Append(indent).Append("    None = 0");
            }

            builder.Append("\n").Append(indent).Append("}\n");
        }
    }
}
