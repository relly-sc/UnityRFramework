using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityRFramework.Expansion;
using UnityRFramework.Runtime;
using YooAsset.Editor;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 生成 ExpansionDemo 的 HybridCLR 可选覆盖层和热更新资源包。
    /// </summary>
    public static class ExpansionHybridCLRDemoBuilder
    {
        private const string PackageName = "ExpansionHybridCLRDemoPackage";
        private const string GroupName = "ExpansionHybridCLRDemo";
        private const string ServerDirectoryName =
            "ExpansionHybridCLRDemoServer";
        private const string PackageNote =
            "Expansion HybridCLR Demo Host update package";
        private const string HotUpdateTag = "hotupdate";
        private const string EntryTypeName =
            "UnityRFramework.Sample.HotUpdateEntry";
        private const string HotUpdateAssemblyName =
            "UnityRFramework.HotUpdate";

        private static readonly string[] RequiredAotAssemblies =
        {
            "mscorlib",
            "System",
            "System.Core"
        };

        private static string sampleRoot;

        private static string Root => sampleRoot ?? (sampleRoot = FindSampleRoot());

        private static string HotUpdateRoot => Root + "/GameAssets/HotUpdate";

        private static string GeneratedRoot => Root + "/Generated";

        private static string BootScene =>
            GeneratedRoot + "/Scenes/ExpansionHybridCLRDemoBoot.unity";

        private static string FrameworkPrefab =>
            GeneratedRoot + "/Prefabs/UnityRFramework.prefab";

        private static string HotUpdateAsmdefPath =>
            Root + "/Scripts/Runtime/HotUpdate/UnityRFramework.HotUpdate.asmdef";

        private static string ProbePrefab => HotUpdateRoot + "/ProbePanel.prefab";

        /// <summary>
        /// 生成 Player 构建所需代码、热更新 UI、收集规则和启动场景。
        /// </summary>
        [MenuItem("UnityRFramework/Expansion/HybridCLR Demo/重建当前平台覆盖层")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "ExpansionHybridCLRDemoBuilder: 不能在 Play Mode 中重建。");
            }

            EnsureFolder(HotUpdateRoot);
            CreateProbePrefab();
            HybridCLRArtifactBuilder.ConfigureProject(
                HotUpdateAsmdefPath,
                RequiredAotAssemblies);
            HybridCLRArtifactBuilder.GenerateCurrentTarget();
            string targetName = EditorUserBuildSettings.activeBuildTarget.ToString();

            ExpansionDemoBuilder.Rebuild(
                typeof(ExpansionHybridCLRDemoGameEntry),
                ConfigureHotUpdateCollector,
                GeneratedRoot,
                "ExpansionHybridCLRDemoBoot.unity",
                PackageName,
                GroupName);
            MigrateGeneratedServerUrl();
            ConfigureBootScene(targetName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[ExpansionHybridCLRDemo] Player overlay rebuilt for "
                + $"'{targetName}'. Build the IL2CPP Player before publishing "
                + "the Host Package.");
        }

        /// <summary>
        /// 使用最近一次 Player 的 AOT 裁剪产物发布代码热更新 Host Package。
        /// </summary>
        [MenuItem("UnityRFramework/Expansion/HybridCLR Demo/构建 Host Package")]
        public static void BuildHostPackage()
        {
            string codeVersion = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
            string targetName = HybridCLRArtifactBuilder.CompileAndStage(
                HotUpdateRoot,
                EntryTypeName,
                codeVersion,
                new[] { HotUpdateAssemblyName },
                RequiredAotAssemblies,
                false);
            ConfigureBootScene(targetName);
            ExpansionDemoBuilder.BuildHostPackage(
                ConfigureHotUpdateCollector,
                PackageName,
                GroupName,
                ServerDirectoryName,
                PackageNote);
            Debug.Log(
                $"[ExpansionHybridCLRDemo] Host Package built. Code version: "
                + $"{codeVersion}, Player baseline: {targetName}.");
        }

        private static void ConfigureHotUpdateCollector()
        {
            BundleCollectorSetting setting = BundleCollectorSettingData.Setting;
            BundleCollectorPackage package = setting.Packages.FirstOrDefault(
                item => string.Equals(
                    item.PackageName,
                    PackageName,
                    StringComparison.Ordinal));
            if (package == null)
            {
                throw new InvalidOperationException(
                    $"ExpansionHybridCLRDemoBuilder: {PackageName} 不存在。");
            }

            BundleCollectorGroup group = package.Groups.FirstOrDefault(
                item => string.Equals(
                    item.GroupName,
                    GroupName,
                    StringComparison.Ordinal));
            if (group == null)
            {
                throw new InvalidOperationException(
                    $"ExpansionHybridCLRDemoBuilder: {GroupName} 分组不存在。");
            }

            group.Collectors.RemoveAll(item => string.Equals(
                item.CollectPath,
                HotUpdateRoot,
                StringComparison.Ordinal));
            group.Collectors.Add(new BundleCollector
            {
                CollectPath = HotUpdateRoot,
                CollectorGUID = AssetDatabase.AssetPathToGUID(HotUpdateRoot),
                CollectorType = ECollectorType.MainAssetCollector,
                AddressRuleName = nameof(HybridCLRHotUpdateAddressRule),
                PackRuleName = nameof(ExpansionDemoShortPackRule),
                FilterRuleName = nameof(CollectAll),
                AssetTags = HotUpdateTag
            });

            BundleCollectorSettingData.ModifyPackage(package);
            BundleCollectorSettingData.ModifyGroup(package, group);
            BundleCollectorSettingData.FixFile();
            BundleCollectorSettingData.SaveFile();
        }

        private static void MigrateGeneratedServerUrl()
        {
            string defaultHostServer =
                ReadExpansionDemoServerUrl("defaultHostServer");
            string fallbackHostServer =
                ReadExpansionDemoServerUrl("fallbackHostServer");
            GameObject root = PrefabUtility.LoadPrefabContents(FrameworkPrefab);
            try
            {
                ResourceComponent resource =
                    root.GetComponentInChildren<ResourceComponent>(true);
                if (resource == null)
                {
                    throw new InvalidOperationException(
                        "ExpansionHybridCLRDemoBuilder: 生成的框架预制体缺少 ResourceComponent。");
                }

                SerializedObject serializedResource = new SerializedObject(resource);
                MigrateServerUrl(
                    serializedResource.FindProperty("defaultHostServer"),
                    defaultHostServer);
                MigrateServerUrl(
                    serializedResource.FindProperty("fallbackHostServer"),
                    fallbackHostServer);
                serializedResource.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, FrameworkPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string ReadExpansionDemoServerUrl(string propertyName)
        {
            string[] guids = AssetDatabase.FindAssets(
                "ExpansionDemoBuilder t:MonoScript");
            const string suffix = "/Scripts/Editor/ExpansionDemoBuilder.cs";
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(suffix, StringComparison.Ordinal))
                {
                    continue;
                }

                string prefabPath = path.Substring(0, path.Length - suffix.Length)
                                    + "/Generated/Prefabs/UnityRFramework.prefab";
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                ResourceComponent resource = prefab == null
                    ? null
                    : prefab.GetComponentInChildren<ResourceComponent>(true);
                if (resource == null)
                {
                    return string.Empty;
                }

                SerializedProperty property =
                    new SerializedObject(resource).FindProperty(propertyName);
                return property?.stringValue ?? string.Empty;
            }

            return string.Empty;
        }

        private static void MigrateServerUrl(
            SerializedProperty property,
            string fallbackValue)
        {
            if (property == null)
            {
                return;
            }

            string value = string.IsNullOrWhiteSpace(property.stringValue)
                ? fallbackValue
                : property.stringValue;
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            const string oldSuffix = "/ExpansionDemoServer";
            value = value.TrimEnd('/');
            if (!value.EndsWith(oldSuffix, StringComparison.OrdinalIgnoreCase))
            {
                property.stringValue = value;
                return;
            }

            property.stringValue = value.Substring(0, value.Length - oldSuffix.Length)
                                   + "/" + ServerDirectoryName;
        }

        private static void ConfigureBootScene(string targetName)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScene) == null)
            {
                throw new InvalidOperationException(
                    "ExpansionHybridCLRDemoBuilder: 独立启动场景不存在，请先执行重建当前平台覆盖层。");
            }

            Scene scene = SceneManager.GetSceneByPath(BootScene);
            bool openedForConfiguration = !scene.IsValid() || !scene.isLoaded;
            if (openedForConfiguration)
            {
                scene = EditorSceneManager.OpenScene(BootScene, OpenSceneMode.Additive);
            }

            try
            {
                ExpansionHybridCLRDemoGameEntry entry = null;
                foreach (GameObject rootObject in scene.GetRootGameObjects())
                {
                    entry = rootObject.GetComponentInChildren<
                        ExpansionHybridCLRDemoGameEntry>(true);
                    if (entry != null)
                    {
                        break;
                    }
                }

                if (entry == null)
                {
                    throw new InvalidOperationException(
                        "ExpansionHybridCLRDemoBuilder: 独立启动场景中未找到 HybridCLR 入口。");
                }

                SerializedObject serializedEntry = new SerializedObject(entry);
                SerializedProperty manifestLocation =
                    serializedEntry.FindProperty("manifestLocation");
                manifestLocation.stringValue = $"HotUpdate/{targetName}/Manifest";
                serializedEntry.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        "ExpansionHybridCLRDemoBuilder: 保存独立启动场景失败。");
                }
            }
            finally
            {
                if (openedForConfiguration && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void CreateProbePrefab()
        {
            Type panelType = FindType(
                "UnityRFramework.Sample.HotUpdateProbePanel");
            if (panelType == null || !typeof(MonoBehaviour).IsAssignableFrom(panelType))
            {
                throw new InvalidOperationException(
                    "ExpansionHybridCLRDemoBuilder: 热更新 UI 程序集尚未完成编译。");
            }

            GameObject root = new GameObject(
                "HybridCLRProbePanel",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1100;

                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                GameObject backdrop = CreateImage(
                    root.transform,
                    "Backdrop",
                    new Color(0.035f, 0.05f, 0.07f, 0.96f));
                Stretch(backdrop.GetComponent<RectTransform>());

                GameObject content = CreateImage(
                    backdrop.transform,
                    "Content",
                    new Color(0.11f, 0.14f, 0.18f, 1f));
                SetRect(
                    content.GetComponent<RectTransform>(),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(720f, 360f),
                    Vector2.zero);

                Text title = CreateText(
                    content.transform,
                    "Title",
                    "HybridCLR 代码热更新",
                    36,
                    TextAnchor.MiddleCenter,
                    Color.white);
                SetRect(
                    title.rectTransform,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(640f, 64f),
                    new Vector2(0f, -55f));

                Text version = CreateText(
                    content.transform,
                    "Version",
                    "热更新代码版本：未加载",
                    26,
                    TextAnchor.MiddleCenter,
                    new Color(0.35f, 0.82f, 0.98f, 1f));
                SetRect(
                    version.rectTransform,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(640f, 52f),
                    new Vector2(0f, -125f));

                Text status = CreateText(
                    content.transform,
                    "Status",
                    "等待热更新入口初始化...",
                    22,
                    TextAnchor.MiddleCenter,
                    new Color(0.78f, 0.83f, 0.88f, 1f));
                SetRect(
                    status.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(640f, 80f),
                    new Vector2(0f, 10f));

                Button continueButton = CreateButton(
                    content.transform,
                    "ContinueButton",
                    "进入 Demo",
                    out Text continueText);
                SetRect(
                    continueButton.GetComponent<RectTransform>(),
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(260f, 64f),
                    new Vector2(0f, 58f));

                Component panel = root.AddComponent(panelType);
                SerializedObject serializedPanel = new SerializedObject(panel);
                serializedPanel.FindProperty("versionText").objectReferenceValue = version;
                serializedPanel.FindProperty("statusText").objectReferenceValue = status;
                serializedPanel.FindProperty("continueButton").objectReferenceValue =
                    continueButton;
                serializedPanel.FindProperty("continueButtonText").objectReferenceValue =
                    continueText;
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ProbePrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (System.Reflection.Assembly assembly in
                     AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static string FindSampleRoot()
        {
            string[] guids = AssetDatabase.FindAssets(
                "ExpansionHybridCLRDemoBuilder t:MonoScript");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                const string suffix =
                    "/Scripts/Editor/ExpansionHybridCLRDemoBuilder.cs";
                if (path.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return path.Substring(0, path.Length - suffix.Length);
                }
            }

            throw new InvalidOperationException(
                "ExpansionHybridCLRDemoBuilder: 无法定位 Sample 根目录。");
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static GameObject CreateImage(
            Transform parent,
            string name,
            Color color)
        {
            GameObject result = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            result.transform.SetParent(parent, false);
            result.GetComponent<Image>().color = color;
            return result;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            GameObject result = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            result.transform.SetParent(parent, false);
            Text text = result.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            out Text text)
        {
            GameObject result = CreateImage(
                parent,
                name,
                new Color(0.12f, 0.50f, 0.30f, 1f));
            Button button = result.AddComponent<Button>();
            button.targetGraphic = result.GetComponent<Image>();
            text = CreateText(
                result.transform,
                "Text",
                label,
                24,
                TextAnchor.MiddleCenter,
                Color.white);
            Stretch(text.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 anchoredPosition)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }
    }

    /// <summary>
    /// 将热更新资源映射为不含 Unity 导入后缀的稳定地址。
    /// </summary>
    public sealed class HybridCLRHotUpdateAddressRule : IAddressRule
    {
        /// <inheritdoc />
        string IAddressRule.GetAssetAddress(AddressRuleData data)
        {
            const string marker = "/GameAssets/";
            string path = data.AssetPath.Replace('\\', '/');
            int index = path.LastIndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"HybridCLR hot update asset is outside GameAssets: '{path}'.");
            }

            string address = path.Substring(index + marker.Length);
            if (address.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
            {
                address = address.Substring(0, address.Length - ".bytes".Length);
            }
            else if (address.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                address = address.Substring(0, address.Length - ".prefab".Length);
            }

            return address;
        }
    }
}
