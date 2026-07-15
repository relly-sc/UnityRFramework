using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityRFramework.Runtime;
using UnityRFramework.Tests;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// ConfigPipeline v1 验收场景和专用 Player 构建入口。
    /// </summary>
    public static class ConfigPipelineAcceptanceBuilder
    {
        public const string ScenePath =
            "Assets/UnityRFramework/Tests/Runtime/ConfigPipelineAcceptance/"
            + "ConfigPipelineAcceptance.unity";
        public const string PlayerPath =
            "Temp/ConfigPipelineAcceptance/ConfigPipelineAcceptance.exe";
        private const string FrameworkPrefabPath =
            "Assets/UnityRFramework/Prefabs/UnityRFramework.prefab";

        [MenuItem("UnityRFramework/Tests/Export ConfigPipeline Acceptance Data")]
        public static void ExportData()
        {
            ConfigPipelineReport report = ConfigPipelineService.ExportAll(
                CreateOptions());
            Debug.Log(string.Join("\n", report.Messages));
        }

        [MenuItem("UnityRFramework/Tests/Analyze ConfigPipeline Acceptance Data")]
        public static void AnalyzeData()
        {
            ConfigPipelineReport report = ConfigPipelineService.Analyze(CreateOptions());
            Debug.Log(string.Join("\n", report.Messages));
        }

        [MenuItem("UnityRFramework/Tests/Rebuild ConfigPipeline Acceptance Scene")]
        public static void RebuildScene()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                GameObject frameworkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    FrameworkPrefabPath);
                if (frameworkPrefab == null)
                {
                    throw new RFramework.RFrameworkException(
                        $"Framework prefab was not found at '{FrameworkPrefabPath}'.");
                }

                GameObject framework = (GameObject)PrefabUtility.InstantiatePrefab(
                    frameworkPrefab, scene);
                framework.name = "UnityRFramework";
                ConfigComponent config =
                    framework.GetComponentInChildren<ConfigComponent>(true);
                LocalizationComponent localization =
                    framework.GetComponentInChildren<LocalizationComponent>(true);
                if (config == null || localization == null)
                {
                    throw new RFramework.RFrameworkException(
                        "Framework prefab is missing ConfigComponent or LocalizationComponent.");
                }

                SetString(config, "configHelperTypeName",
                    "UnityRFramework.Runtime.BinaryConfigHelper");
                SetString(localization, "localizationHelperTypeName",
                    "UnityRFramework.Runtime.BinaryLocalizationHelper");
                SetString(localization, "defaultLanguage", "zh-CN");
                SetString(localization, "languageAssetRoot",
                    "ConfigPipelineAcceptance/Localization/Binary");
                SetString(localization, "languageFileExtension", ".bytes");

                GameObject acceptance = new GameObject("ConfigPipeline Acceptance");
                acceptance.AddComponent<ConfigPipelineAcceptance>();

                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<Camera>();

                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    throw new RFramework.RFrameworkException(
                        "Failed to save ConfigPipeline acceptance scene.");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("ConfigPipeline acceptance scene rebuilt: " + ScenePath);
        }

        [MenuItem("UnityRFramework/Tests/Run ConfigPipeline Acceptance")]
        public static void RunInEditor()
        {
            EnsureScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("UnityRFramework/Tests/Build ConfigPipeline Acceptance Player")]
        public static void BuildPlayer()
        {
            EnsureScene();
            string fullPlayerPath = Path.GetFullPath(PlayerPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPlayerPath));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = fullPlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.IncludeTestAssemblies
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new RFramework.RFrameworkException(
                    $"ConfigPipeline acceptance build failed: {report.summary.result}, "
                    + $"errors={report.summary.totalErrors}, "
                    + $"warnings={report.summary.totalWarnings}.");
            }

            Debug.Log(
                $"CONFIG_PIPELINE_ACCEPTANCE_BUILD_PASS size={report.summary.totalSize} "
                + $"warnings={report.summary.totalWarnings}");
        }

        private static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                RebuildScene();
            }
        }

        private static ConfigPipelineOptions CreateOptions()
        {
            return new ConfigPipelineOptions
            {
                ConfigSourceDirectory =
                    "Assets/UnityRFramework/Tests/Fixtures/ConfigPipeline/"
                    + "ConfigSource/Config",
                LocalizationSourceDirectory =
                    "Assets/UnityRFramework/Tests/Fixtures/ConfigPipeline/"
                    + "ConfigSource/Localization",
                GeneratedCodeDirectory =
                    "Assets/UnityRFramework/Tests/Runtime/ConfigPipelineAcceptance/Generated",
                ConfigOutputDirectory =
                    "Assets/UnityRFramework/Tests/Runtime/ConfigPipelineAcceptance/"
                    + "Resources/ConfigPipelineAcceptance/Config",
                LocalizationOutputDirectory =
                    "Assets/UnityRFramework/Tests/Runtime/ConfigPipelineAcceptance/"
                    + "Resources/ConfigPipelineAcceptance/Localization",
                ExportConfigBundle = true,
                ConfigBundleName = "AcceptanceBundle",
                ExportLocalizationBundle = true,
                LocalizationBundleName = "AcceptanceLanguages",
                GeneratedNamespace = "UnityRFramework.Tests.Config"
            };
        }

        private static void SetString(Object target, string propertyName, string value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new RFramework.RFrameworkException(
                    $"Serialized property '{propertyName}' was not found on '{target.GetType().Name}'.");
            }

            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorUtility.SetDirty(target);
        }
    }
}
