using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Profile 参数绘制的共享实现：基础字段、平台分区（按目标平台过滤专有字段）、
    /// 输出设置与场景列表。构建工具窗口与 Profile 资产 Inspector 共用，
    /// 保证两处字段显示与过滤规则完全一致，避免重复维护。
    /// </summary>
    internal static class BuildProfileParametersGUI
    {
        /// <summary>编译参数前的平台通用字段名。</summary>
        private static readonly string[] PlatformIdentityFieldNames =
        {
            "CompanyName",
            "ProductName",
            "ApplicationIdentifier",
            "PublicVersion",
            "BuildNumber",
            "AutoIncrementBuildNumber"
        };

        /// <summary>编译参数后的平台通用字段名。</summary>
        private static readonly string[] PlatformRuntimeFieldNames =
        {
            "IncrementalGC",
            "DevelopmentBuild",
            "ScriptDebugging",
            "AutoconnectProfiler",
            "DeepProfiling",
            "DefineSymbols",
            "RemoveDefineSymbols"
        };

        private static readonly BuildTarget[] SupportedTargets =
        {
            BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneLinux64,
            BuildTarget.StandaloneOSX,
            BuildTarget.Android,
            BuildTarget.iOS,
            BuildTarget.WebGL
        };

        private static readonly string[] SupportedTargetLabels =
        {
            "Windows x64",
            "Linux x64",
            "macOS",
            "Android",
            "iOS",
            "WebGL"
        };

        private static readonly ScriptingImplementation[] StandardBackends =
        {
            ScriptingImplementation.Mono2x,
            ScriptingImplementation.IL2CPP
        };

        private static readonly ScriptingImplementation[] Il2CppOnlyBackend =
        {
            ScriptingImplementation.IL2CPP
        };

        private static readonly string[] StandardBackendLabels = { "Mono", "IL2CPP" };
        private static readonly string[] Il2CppOnlyBackendLabel = { "IL2CPP" };

        private static readonly ApiCompatibilityLevel[] ApiCompatibilityLevels =
        {
            ApiCompatibilityLevel.NET_Standard,
            ApiCompatibilityLevel.NET_Unity_4_8
        };

        private static readonly string[] ApiCompatibilityLabels =
        {
            ".NET Standard 2.1",
            ".NET Framework"
        };

        private static readonly ManagedStrippingLevel[] ManagedStrippingLevels =
        {
            ManagedStrippingLevel.Minimal,
            ManagedStrippingLevel.Low,
            ManagedStrippingLevel.Medium,
            ManagedStrippingLevel.High
        };

        private static readonly string[] ManagedStrippingLabels =
        {
            "Minimal",
            "Low",
            "Medium",
            "High"
        };

        private static readonly Il2CppCodeGeneration[] Il2CppCodeGenerations =
        {
            Il2CppCodeGeneration.OptimizeSpeed,
            Il2CppCodeGeneration.OptimizeSize
        };

        private static readonly string[] Il2CppCodeGenerationLabels =
        {
            "Faster runtime",
            "Faster (smaller) builds"
        };

        /// <summary>Android 平台专有字段名。</summary>
        private static readonly string[] AndroidOnlyFieldNames =
        {
            "AndroidBuildAppBundle",
            "AndroidArchitecture",
            "AndroidTargetSdk",
            "AndroidKeystoreName",
            "AndroidKeystoreAlias",
            "AndroidKeystorePassEnvVar",
            "AndroidKeyAliasPassEnvVar"
        };

        /// <summary>iOS 平台专有字段名。</summary>
        private static readonly string[] IosOnlyFieldNames =
        {
            "IosTargetSdk"
        };

        /// <summary>
        /// 绘制 Profile 基础字段与全部参数分区（平台 / 输出 / 场景）。
        /// 调用方负责在绘制前后执行 SerializedObject.Update 与 ApplyModifiedProperties。
        /// </summary>
        /// <param name="serializedObject">Profile 资产的序列化对象。</param>
        /// <param name="outputPreview">输出路径预览文本；非空时在输出设置下方显示只读预览。</param>
        public static void DrawProfileParameters(
            SerializedObject serializedObject,
            string outputPreview = null)
        {
            // 标签宽度由窗口 OnGUI 顶层统一设为 200px，此处不再单独管理。
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Description"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Enabled"));
            DrawFlavor(serializedObject);
            DrawRecipe(serializedObject.FindProperty("Recipe"));

            EditorGUILayout.Space(6f);
            DrawPlatformSection(serializedObject, serializedObject.FindProperty("Platform"));

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("输出设置（Player 包体路径）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Player 包体输出根目录，与 YooAsset Bundles 目录独立。",
                MessageType.Info);
            DrawOutputSettings(serializedObject.FindProperty("Output"));

            if (!string.IsNullOrEmpty(outputPreview))
            {
                EditorGUILayout.Space(2f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    // label 宽度与上方字段标签保持一致，避免左右列错位。
                    EditorGUILayout.LabelField(
                        "Player 输出预览",
                        GUILayout.Width(EditorGUIUtility.labelWidth));
                    EditorGUILayout.SelectableLabel(
                        outputPreview,
                        EditorStyles.label,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight),
                        GUILayout.ExpandWidth(true));
                }
            }

            EditorGUILayout.Space(6f);
            DrawScenesList(serializedObject.FindProperty("Scenes"));
        }

        /// <summary>
        /// 绘制 Recipe 选项及当前执行范围提示，避免把构建方案与 Flavor 混淆。
        /// </summary>
        /// <param name="recipeProperty">Recipe 序列化属性。</param>
        private static void DrawRecipe(SerializedProperty recipeProperty)
        {
            if (recipeProperty == null)
            {
                EditorGUILayout.HelpBox(
                    "Recipe 字段序列化失败，请检查 UnityRFrameworkBuildProfile 定义。",
                    MessageType.Error);
                return;
            }

            EditorGUILayout.PropertyField(recipeProperty);
            BuildRecipe recipe = (BuildRecipe)recipeProperty.enumValueIndex;
            EditorGUILayout.HelpBox(GetRecipeDescription(recipe), MessageType.Info);
        }

        /// <summary>
        /// 获取构建方案的执行范围说明。可选步骤仍以 Profile 中已启用条目为准。
        /// </summary>
        /// <param name="recipe">构建方案。</param>
        /// <returns>适合直接展示在 Profile 面板的说明。</returns>
        private static string GetRecipeDescription(BuildRecipe recipe)
        {
            switch (recipe)
            {
                case BuildRecipe.Player:
                    return "Player：只应用 Profile 参数并构建 Player；不导出 Config，"
                        + "不准备热更代码，也不构建 YooAsset 资源包。";
                case BuildRecipe.Assets:
                    return "Assets：导出 Config 并构建 YooAsset 资源包；"
                        + "不准备 HybridCLR/Obfuz 热更代码，也不构建 Player。";
                case BuildRecipe.HotUpdate:
                    return "HotUpdate：准备 HybridCLR/Obfuz 热更代码并构建 YooAsset 资源包；"
                        + "不导出 Config，也不构建 Player。";
                case BuildRecipe.Release:
                    return "Release：执行配置导出、热更代码准备、资源构建和 Player 构建的完整发布流程。";
                default:
                    return "未知 Recipe，无法确定构建范围。";
            }
        }

        /// <summary>
        /// 绘制构建分档与显式应用按钮。切换分档不会自动覆盖用户参数；
        /// 应用前展示所有将被修改的字段，并且只写入当前 Profile。
        /// </summary>
        /// <param name="serializedObject">Profile 资产的序列化对象。</param>
        private static void DrawFlavor(SerializedObject serializedObject)
        {
            SerializedProperty flavorProperty = serializedObject.FindProperty("Flavor");
            if (flavorProperty == null)
            {
                EditorGUILayout.HelpBox(
                    "Flavor 字段序列化失败，请检查 UnityRFrameworkBuildProfile 定义。",
                    MessageType.Error);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(flavorProperty);
                if (!GUILayout.Button(
                        new GUIContent(
                            "应用推荐参数",
                            "预览并应用该分档推荐的调试参数；不会修改脚本后端，也不会立即修改 PlayerSettings。"),
                        GUILayout.Width(112f)))
                {
                    return;
                }
            }

            UnityRFrameworkBuildProfile profile =
                serializedObject.targetObject as UnityRFrameworkBuildProfile;
            if (profile == null)
            {
                return;
            }

            BuildProfileFlavor flavor =
                (BuildProfileFlavor)flavorProperty.enumValueIndex;
            BuildFlavorRecommendation recommendation =
                BuildFlavorRecommendation.Get(flavor);
            IReadOnlyList<string> changes =
                recommendation.GetChanges(profile.Platform);
            if (changes.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "应用推荐参数",
                    "当前参数已符合该分档的推荐值，无需修改。",
                    "确定");
                return;
            }

            StringBuilder message = new StringBuilder();
            message.AppendLine("将修改当前 Profile 的以下字段：");
            message.AppendLine();
            for (int i = 0; i < changes.Count; i++)
            {
                message.AppendLine("- " + changes[i]);
            }
            message.AppendLine();
            message.Append("本操作不会立即修改 Unity PlayerSettings。是否继续？");

            if (!EditorUtility.DisplayDialog(
                    $"应用 {flavor} 推荐参数",
                    message.ToString(),
                    "应用",
                    "取消"))
            {
                return;
            }

            ApplyFlavorRecommendation(
                serializedObject.FindProperty("Platform"),
                recommendation);
        }

        /// <summary>
        /// 将推荐值写入序列化属性，交由调用方统一保存与刷新校验结果。
        /// </summary>
        private static void ApplyFlavorRecommendation(
            SerializedProperty platform,
            BuildFlavorRecommendation recommendation)
        {
            platform.FindPropertyRelative("DevelopmentBuild").boolValue =
                recommendation.DevelopmentBuild;
            platform.FindPropertyRelative("ScriptDebugging").boolValue =
                recommendation.ScriptDebugging;
            platform.FindPropertyRelative("AutoconnectProfiler").boolValue =
                recommendation.AutoconnectProfiler;
            platform.FindPropertyRelative("DeepProfiling").boolValue =
                recommendation.DeepProfiling;
        }

        /// <summary>
        /// 手动绘制场景列表：每个元素一行（序号 / 场景引用 / 启用开关 / 上移 / 下移 / 删除），
        /// 不使用 Unity 默认 ReorderableList，消除其元素自带缩进导致「场景」标签被向右挤入的空白。
        /// 排序通过上移/下移按钮完成，删除与添加即时作用于数组。
        /// </summary>
        /// <param name="scenesProp">Scenes 数组序列化属性。</param>
        private static void DrawScenesList(SerializedProperty scenesProp)
        {
            if (scenesProp == null)
            {
                return;
            }

            EditorGUILayout.LabelField("场景列表", EditorStyles.boldLabel);

            for (int i = 0; i < scenesProp.arraySize; i++)
            {
                SerializedProperty element = scenesProp.GetArrayElementAtIndex(i);
                SerializedProperty sceneProp = element.FindPropertyRelative("Scene");
                SerializedProperty enabledProp = element.FindPropertyRelative("Enabled");

                EditorGUILayout.BeginHorizontal();
                // 序号与「场景」标签合并到一行，宽度给足，避免窄标签把数字压缩成方块。
                EditorGUILayout.LabelField($"{i}. 场景", GUILayout.Width(64f));
                EditorGUILayout.PropertyField(sceneProp, GUIContent.none, GUILayout.ExpandWidth(true));

                // 启用开关：行内临时收紧 labelWidth，避免顶层 200px 标签把 checkbox 挤出可视区导致无法点击。
                float savedLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 40f;
                try
                {
                    EditorGUILayout.PropertyField(enabledProp, new GUIContent("启用"), GUILayout.Width(72f));
                }
                finally
                {
                    EditorGUIUtility.labelWidth = savedLabelWidth;
                }

                bool movedUp = false;
                bool movedDown = false;
                bool removed = false;

                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button("↑", GUILayout.Width(24f)))
                    {
                        movedUp = true;
                    }
                }
                using (new EditorGUI.DisabledScope(i == scenesProp.arraySize - 1))
                {
                    if (GUILayout.Button("↓", GUILayout.Width(24f)))
                    {
                        movedDown = true;
                    }
                }
                if (GUILayout.Button("✕", GUILayout.Width(24f)))
                {
                    removed = true;
                }
                EditorGUILayout.EndHorizontal();

                if (movedUp)
                {
                    scenesProp.MoveArrayElement(i, i - 1);
                    break;
                }
                if (movedDown)
                {
                    scenesProp.MoveArrayElement(i, i + 1);
                    break;
                }
                if (removed)
                {
                    scenesProp.DeleteArrayElementAtIndex(i);
                    break;
                }
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("＋ 添加场景", GUILayout.Width(110f)))
            {
                scenesProp.InsertArrayElementAtIndex(scenesProp.arraySize);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制平台设置分区：目标平台切换即时生效，专有字段按平台过滤显示。
        /// </summary>
        /// <param name="owner">Profile 资产的序列化对象，用于 Target 切换后立即应用。</param>
        /// <param name="platform">Platform 序列化属性。</param>
        public static void DrawPlatformSection(SerializedObject owner, SerializedProperty platform)
        {
            if (platform == null)
            {
                EditorGUILayout.HelpBox(
                    "Platform 字段序列化失败，请检查 BuildPlatformSettings 定义。",
                    MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("平台设置", EditorStyles.boldLabel);

            // 目标平台先绘制并立即应用，保证分区切换即时生效。
            SerializedProperty targetProperty = platform.FindPropertyRelative("Target");
            BuildTarget selectedTarget = DrawRestrictedEnum(
                targetProperty,
                new GUIContent("Target", "仅提供框架正式支持的常用构建平台。"),
                SupportedTargets,
                SupportedTargetLabels);

            SerializedProperty backendProperty =
                platform.FindPropertyRelative("ScriptingBackend");
            if (BuildPlatformSettings.RequiresIl2Cpp(selectedTarget))
            {
                backendProperty.intValue = (int)ScriptingImplementation.IL2CPP;
            }

            owner.ApplyModifiedProperties();

            BuildTarget currentTarget = GetCurrentTarget(owner);
            for (int i = 0; i < PlatformIdentityFieldNames.Length; i++)
            {
                DrawPlatformField(platform, PlatformIdentityFieldNames[i]);
            }

            bool requiresIl2Cpp = BuildPlatformSettings.RequiresIl2Cpp(currentTarget);
            ScriptingImplementation backend = DrawRestrictedEnum(
                backendProperty,
                new GUIContent(
                    "Scripting Backend",
                    requiresIl2Cpp
                        ? "当前平台只支持 IL2CPP。"
                        : "脚本后端由项目技术栈与插件要求决定。"),
                requiresIl2Cpp ? Il2CppOnlyBackend : StandardBackends,
                requiresIl2Cpp ? Il2CppOnlyBackendLabel : StandardBackendLabels);

            DrawRestrictedEnum(
                platform.FindPropertyRelative("ApiCompatibilityLevel"),
                new GUIContent("API Compatibility Level"),
                ApiCompatibilityLevels,
                ApiCompatibilityLabels);
            DrawRestrictedEnum(
                platform.FindPropertyRelative("ManagedStrippingLevel"),
                new GUIContent("Managed Stripping Level"),
                ManagedStrippingLevels,
                ManagedStrippingLabels);

            if (backend == ScriptingImplementation.IL2CPP)
            {
                DrawRestrictedEnum(
                    platform.FindPropertyRelative("Il2CppCodeGeneration"),
                    new GUIContent("IL2CPP Code Generation"),
                    Il2CppCodeGenerations,
                    Il2CppCodeGenerationLabels);
                DrawPlatformField(platform, "CppCompilerConfiguration");
            }

            for (int i = 0; i < PlatformRuntimeFieldNames.Length; i++)
            {
                DrawPlatformField(platform, PlatformRuntimeFieldNames[i]);
            }

            string[] exclusiveNames = GetExclusiveFieldNames(currentTarget);
            if (exclusiveNames.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "当前平台无专有参数。",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                GetPlatformSectionTitle(currentTarget),
                EditorStyles.boldLabel);
            for (int i = 0; i < exclusiveNames.Length; i++)
            {
                DrawPlatformField(platform, exclusiveNames[i]);
            }
        }

        /// <summary>
        /// 绘制受限枚举下拉，只展示框架支持且与 Unity Player Settings 对应的选项。
        /// 旧值不在允许列表中时规范为首个有效值。
        /// </summary>
        private static T DrawRestrictedEnum<T>(
            SerializedProperty property,
            GUIContent label,
            T[] values,
            string[] labels)
            where T : struct, Enum
        {
            if (property == null || values == null || values.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    $"字段 '{label?.text}' 无可用选项。",
                    MessageType.Error);
                return default;
            }

            int currentValue = property.intValue;
            int currentIndex = -1;
            for (int i = 0; i < values.Length; i++)
            {
                if (Convert.ToInt32(values[i]) == currentValue)
                {
                    currentIndex = i;
                    break;
                }
            }

            bool needsNormalization = currentIndex < 0;
            if (needsNormalization)
            {
                currentIndex = 0;
            }

            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup(label, currentIndex, labels);
            if (EditorGUI.EndChangeCheck() || needsNormalization)
            {
                property.intValue = Convert.ToInt32(values[selectedIndex]);
            }

            return values[selectedIndex];
        }

        /// <summary>
        /// 从序列化对象读取当前目标构建平台；读取失败时回退到不受支持的值。
        /// </summary>
        /// <param name="owner">Profile 资产的序列化对象。</param>
        /// <returns>当前目标平台。</returns>
        private static BuildTarget GetCurrentTarget(SerializedObject owner)
        {
            SerializedProperty target = owner.FindProperty("Platform")
                ?.FindPropertyRelative("Target");
            if (target == null)
            {
                return BuildTarget.NoTarget;
            }

            return (BuildTarget)target.intValue;
        }

        /// <summary>
        /// 绘制平台设置中的单个字段；字段序列化失败时给出提示。
        /// </summary>
        /// <param name="platform">Platform 序列化属性。</param>
        /// <param name="fieldName">字段名。</param>
        private static void DrawPlatformField(SerializedProperty platform, string fieldName)
        {
            SerializedProperty property = platform.FindPropertyRelative(fieldName);
            if (property == null)
            {
                EditorGUILayout.HelpBox(
                    $"平台字段 '{fieldName}' 序列化失败，请检查 BuildPlatformSettings 定义。",
                    MessageType.Warning);
                return;
            }

            if (string.Equals(
                    fieldName,
                    "AndroidKeystoreName",
                    StringComparison.Ordinal))
            {
                BuildAssetPathField.DrawFile(
                    property,
                    new GUIContent(
                        property.displayName,
                        "Android Keystore 文件；工程内保存相对路径，工程外保存绝对路径。"),
                    string.Empty,
                    false);
                return;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        /// <summary>
        /// 绘制 Player 输出设置；输出根目录提供文件系统目录选择。
        /// </summary>
        private static void DrawOutputSettings(SerializedProperty output)
        {
            if (output == null)
            {
                EditorGUILayout.HelpBox(
                    "Output 字段序列化失败，请检查 BuildOutputSettings 定义。",
                    MessageType.Error);
                return;
            }

            BuildAssetPathField.DrawDirectory(
                output.FindPropertyRelative("OutputRoot"),
                new GUIContent(
                    "Output Root",
                    "Player 输出根目录；工程内保存相对路径，工程外保存绝对路径。"),
                false);
            EditorGUILayout.PropertyField(
                output.FindPropertyRelative("DirectoryTemplate"),
                true);
            EditorGUILayout.PropertyField(
                output.FindPropertyRelative("FileNameTemplate"),
                true);
            EditorGUILayout.PropertyField(
                output.FindPropertyRelative("CleanBeforeBuild"),
                true);
        }

        /// <summary>
        /// 按目标平台返回应显示的专有字段名数组。
        /// </summary>
        /// <param name="buildTarget">目标构建平台。</param>
        /// <returns>专有字段名数组；当前平台无专有字段时返回空数组。</returns>
        private static string[] GetExclusiveFieldNames(BuildTarget buildTarget)
        {
            if (buildTarget == BuildTarget.Android)
            {
                return AndroidOnlyFieldNames;
            }

            if (buildTarget == BuildTarget.iOS)
            {
                return IosOnlyFieldNames;
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// 获取平台专有参数分区标题。
        /// </summary>
        /// <param name="buildTarget">目标构建平台。</param>
        /// <returns>分区标题。</returns>
        private static string GetPlatformSectionTitle(BuildTarget buildTarget)
        {
            if (buildTarget == BuildTarget.Android)
            {
                return "Android 专有参数";
            }

            if (buildTarget == BuildTarget.iOS)
            {
                return "iOS 专有参数";
            }

            return "平台专有参数";
        }
    }
}
