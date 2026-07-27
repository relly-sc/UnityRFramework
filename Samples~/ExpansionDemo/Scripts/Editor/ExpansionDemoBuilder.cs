using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityRFramework.Runtime;
using YooAsset.Editor;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 创建和更新 ExpansionDemo 的验收资产、YooAsset 收集规则与 Build Settings。
    /// 运行时场景只保留序列化布局，不依赖代码动态创建 UI。
    /// </summary>
    public static class ExpansionDemoBuilder
    {
        private const string PackageName = "ExpansionDemoPackage";
        private const string WebProbeDirectory = "Assets/StreamingAssets/ExpansionDemo";
        private const string WebProbeFile = WebProbeDirectory + "/WebProbe.txt";
        private static string sampleRoot;

        private static string Root => sampleRoot ?? (sampleRoot = FindSampleRoot());

        private static string DemoFrameworkPrefab =>
            Root + "/GameAssets/Prefabs/UnityRFramework.prefab";

        private static string RawDirectory => Root + "/GameAssets/YooAsset/Raw";

        private static string RemoteDirectory => Root + "/GameAssets/YooAsset/Remote";

        private static string SceneDirectory => Root + "/GameAssets/YooAsset/Scenes";

        private static string ProbeFile => RawDirectory + "/ExpansionProbe.bytes";

        private static string RemoteProbeFile => RemoteDirectory + "/RemoteProbe.json";

        private static string BootScene => Root + "/GameAssets/Scenes/ExpansionDemo.unity";

        private static string ContentScene => SceneDirectory + "/ExpansionContent.unity";

        /// <summary>
        /// 重建 ExpansionDemo 验收资产并将启动场景放到 Build Settings 第 0 项。
        /// </summary>
        [MenuItem("UnityRFramework/ExpansionDemo/Rebuild Acceptance Assets")]
        public static void Rebuild()
        {
            EnsureDirectories();
            WriteProbeFile();
            WriteRemoteProbeFile();
            WriteWebProbeFile();
            CreateContentScene();
            CreateFrameworkPrefab();
            ConfigureYooAssetCollectors();
            CreateBootScene();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ExpansionDemo] Acceptance assets rebuilt.");
        }

        private static void EnsureDirectories()
        {
            EnsureFolder(Root + "/Scripts");
            EnsureFolder(Root + "/GameAssets");
            EnsureFolder(Root + "/GameAssets/Prefabs");
            EnsureFolder(Root + "/GameAssets/Scenes");
            EnsureFolder(Root + "/GameAssets/YooAsset");
            EnsureFolder(RawDirectory);
            EnsureFolder(RemoteDirectory);
            EnsureFolder(SceneDirectory);
            EnsureFolder(WebProbeDirectory);
        }

        private static string FindSampleRoot()
        {
            string[] guids = AssetDatabase.FindAssets("ExpansionDemoBuilder t:MonoScript");
            foreach (string guid in guids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!scriptPath.EndsWith(
                        "/Scripts/Editor/ExpansionDemoBuilder.cs",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string editorDirectory = Path.GetDirectoryName(scriptPath);
                string scriptsDirectory = Path.GetDirectoryName(editorDirectory);
                string root = Path.GetDirectoryName(scriptsDirectory);
                if (!string.IsNullOrEmpty(root))
                {
                    return root.Replace('\\', '/');
                }
            }

            throw new InvalidOperationException(
                "ExpansionDemoBuilder: 无法从脚本位置定位 ExpansionDemo Sample 根目录。");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void WriteProbeFile()
        {
            string absolutePath = Path.GetFullPath(ProbeFile);
            File.WriteAllText(
                absolutePath,
                "UnityRFramework Expansion RawFile acceptance payload.\n",
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ProbeFile, ImportAssetOptions.ForceUpdate);
        }

        private static void WriteWebProbeFile()
        {
            string absolutePath = Path.GetFullPath(WebProbeFile);
            File.WriteAllText(
                absolutePath,
                "UnityRFramework Expansion WebRequest acceptance payload.\n",
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(WebProbeFile, ImportAssetOptions.ForceUpdate);
        }

        private static void WriteRemoteProbeFile()
        {
            string absolutePath = Path.GetFullPath(RemoteProbeFile);
            if (!File.Exists(absolutePath))
            {
                File.WriteAllText(
                    absolutePath,
                    "{\n"
                    + "  \"version\": 1,\n"
                    + "  \"message\": \"UnityRFramework remote package probe\"\n"
                    + "}\n",
                    new UTF8Encoding(false));
            }

            AssetDatabase.ImportAsset(RemoteProbeFile, ImportAssetOptions.ForceUpdate);
        }

        private static void CreateContentScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("Expansion Content Probe");
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "YooAsset Scene Marker";
            marker.transform.SetParent(root.transform);
            marker.transform.position = Vector3.zero;

            EditorSceneManager.SaveScene(scene, ContentScene);
        }

        private static void CreateFrameworkPrefab()
        {
            string sourceFrameworkPrefab = FindFrameworkPrefab();
            if (!AssetDatabase.CopyAsset(sourceFrameworkPrefab, DemoFrameworkPrefab)
                && AssetDatabase.LoadAssetAtPath<GameObject>(DemoFrameworkPrefab) == null)
            {
                throw new InvalidOperationException(
                    $"Can not copy framework prefab from '{sourceFrameworkPrefab}'.");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(DemoFrameworkPrefab);
            try
            {
                ResourceComponent resource = root.GetComponentInChildren<ResourceComponent>(true);
                WebRequestComponent webRequest = root.GetComponentInChildren<WebRequestComponent>(true);
                LocalizationComponent localization =
                    root.GetComponentInChildren<LocalizationComponent>(true);

                SetSerializedValue(
                    resource,
                    "resourceHelperTypeName",
                    "UnityRFramework.Expansion.YooAssetResourceHelper");
                SetSerializedValue(resource, "playMode", 0);
                SetSerializedValue(resource, "packageName", PackageName);
                SetSerializedValue(
                    webRequest,
                    "webRequestHelperTypeName",
                    "UnityRFramework.Expansion.UniTaskWebRequestHelper");
                SetSerializedValue(localization, "loadDefaultLanguageOnStart", false);

                PrefabUtility.SaveAsPrefabAsset(root, DemoFrameworkPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string FindFrameworkPrefab()
        {
            string[] candidates =
            {
                "Packages/com.relly-sc.unityrframework/Prefabs/UnityRFramework.prefab",
                "Assets/UnityRFramework/Prefabs/UnityRFramework.prefab"
            };

            foreach (string candidate in candidates)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(candidate) != null)
                {
                    return candidate;
                }
            }

            string[] guids = AssetDatabase.FindAssets("UnityRFramework t:Prefab");
            foreach (string guid in guids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                if (prefabPath.EndsWith(
                        "/Prefabs/UnityRFramework.prefab",
                        StringComparison.Ordinal)
                    && !prefabPath.StartsWith(Root + "/", StringComparison.Ordinal))
                {
                    return prefabPath;
                }
            }

            throw new InvalidOperationException(
                "ExpansionDemoBuilder: 无法定位框架 UnityRFramework.prefab。");
        }

        private static void SetSerializedValue(
            UnityEngine.Object target,
            string propertyName,
            object value)
        {
            if (target == null)
            {
                throw new InvalidOperationException(
                    $"ExpansionDemoBuilder: target for '{propertyName}' is missing.");
            }

            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"ExpansionDemoBuilder: serialized property '{propertyName}' is missing "
                    + $"on '{target.GetType().Name}'.");
            }

            if (value is string stringValue)
            {
                property.stringValue = stringValue;
            }
            else if (value is bool boolValue)
            {
                property.boolValue = boolValue;
            }
            else if (value is int intValue)
            {
                property.intValue = intValue;
            }
            else
            {
                throw new ArgumentException(
                    $"Unsupported serialized value type '{value?.GetType().FullName}'.",
                    nameof(value));
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureYooAssetCollectors()
        {
            BundleCollectorSetting setting = BundleCollectorSettingData.Setting;
            BundleCollectorPackage package = setting.Packages.FirstOrDefault(
                item => string.Equals(item.PackageName, PackageName, StringComparison.Ordinal));
            if (package == null)
            {
                package = BundleCollectorSettingData.CreatePackage(PackageName);
            }

            package.EnableAddressable = true;
            package.AutoCollectShaders = true;
            package.IgnoreRuleName = nameof(NormalIgnoreRule);

            BundleCollectorGroup group = package.Groups.FirstOrDefault(
                item => string.Equals(item.GroupName, "ExpansionDemo", StringComparison.Ordinal));
            if (group == null)
            {
                group = BundleCollectorSettingData.CreateGroup(package, "ExpansionDemo");
            }

            group.Collectors.Clear();
            group.Collectors.Add(CreateCollector(
                RawDirectory,
                nameof(PackSeparately),
                nameof(CollectAll),
                "builtin"));
            group.Collectors.Add(CreateCollector(
                RemoteDirectory,
                nameof(PackSeparately),
                nameof(CollectAll),
                string.Empty));
            group.Collectors.Add(CreateCollector(
                SceneDirectory,
                nameof(PackSeparately),
                nameof(CollectScene),
                "builtin"));

            BundleCollectorSettingData.ModifyPackage(package);
            BundleCollectorSettingData.ModifyGroup(package, group);
            BundleCollectorSettingData.FixFile();
            BundleCollectorSettingData.SaveFile();
        }

        private static BundleCollector CreateCollector(
            string collectPath,
            string packRuleName,
            string filterRuleName,
            string assetTags)
        {
            return new BundleCollector
            {
                CollectPath = collectPath,
                CollectorGUID = AssetDatabase.AssetPathToGUID(collectPath),
                CollectorType = ECollectorType.MainAssetCollector,
                AddressRuleName = nameof(AddressByFileName),
                PackRuleName = packRuleName,
                FilterRuleName = filterRuleName,
                AssetTags = assetTags
            };
        }

        private static void CreateBootScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject frameworkPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(DemoFrameworkPrefab);
            PrefabUtility.InstantiatePrefab(frameworkPrefab, scene);

            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));

            GameObject controllerObject = new GameObject("ExpansionDemoController");
            ExpansionDemoController controller =
                controllerObject.AddComponent<ExpansionDemoController>();

            Canvas canvas = CreateCanvas();
            Image background = CreateImage(
                "Background",
                canvas.transform,
                new Color32(31, 38, 50, 255));
            Stretch(background.rectTransform);

            Text title = CreateText(
                "Title",
                background.transform,
                "UnityRFramework Expansion Acceptance",
                30,
                TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, 176, -24, -32, 60, true);

            Text description = CreateText(
                "Description",
                background.transform,
                "YooAsset Resource Helper + UniTask WebRequest Helper",
                20,
                TextAnchor.MiddleLeft);
            SetRect(description.rectTransform, 32, -82, -32, 40, true);

            Image statusPanel = CreateImage(
                "StatusPanel",
                background.transform,
                new Color32(20, 25, 34, 255));
            SetRect(statusPanel.rectTransform, 32, -136, -32, 796, true);

            Text status = CreateText(
                "Status",
                statusPanel.transform,
                "等待验收...",
                20,
                TextAnchor.UpperLeft);
            Stretch(status.rectTransform, 20);
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            status.verticalOverflow = VerticalWrapMode.Overflow;

            Button run = CreateButton(
                "RunAcceptance",
                background.transform,
                "运行全部",
                new Color32(37, 126, 87, 255));
            Button cancel = CreateButton(
                "RunCancellation",
                background.transform,
                "测试取消",
                new Color32(44, 101, 174, 255));
            Button restart = CreateButton(
                "RestartFramework",
                background.transform,
                "重启框架",
                new Color32(174, 111, 24, 255));

            SetBottomButtonRect(run.GetComponent<RectTransform>(), 32, 0);
            SetBottomButtonRect(cancel.GetComponent<RectTransform>(), 0, 1);
            SetBottomButtonRect(restart.GetComponent<RectTransform>(), -32, 2);

            SerializedObject controllerData = new SerializedObject(controller);
            controllerData.FindProperty("statusText").objectReferenceValue = status;
            controllerData.FindProperty("runButton").objectReferenceValue = run;
            controllerData.FindProperty("cancelButton").objectReferenceValue = cancel;
            controllerData.FindProperty("restartButton").objectReferenceValue = restart;
            controllerData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, BootScene);
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            TextAnchor alignment)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.text = value;
            return text;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Color color)
        {
            GameObject gameObject =
                new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;

            Button button = gameObject.GetComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText("Text", gameObject.transform, label, 22, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rectTransform, float padding = 0)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(padding, padding);
            rectTransform.offsetMax = new Vector2(-padding, -padding);
        }

        private static void SetRect(
            RectTransform rectTransform,
            float left,
            float top,
            float right,
            float height,
            bool stretchHorizontal)
        {
            rectTransform.anchorMin = stretchHorizontal
                ? new Vector2(0, 1)
                : new Vector2(0.5f, 1);
            rectTransform.anchorMax = stretchHorizontal
                ? new Vector2(1, 1)
                : new Vector2(0.5f, 1);
            rectTransform.pivot = new Vector2(0.5f, 1);
            rectTransform.offsetMin = new Vector2(left, top - height);
            rectTransform.offsetMax = new Vector2(right, top);
        }

        private static void SetBottomButtonRect(
            RectTransform rectTransform,
            float edgeOffset,
            int index)
        {
            float width = 260;
            float spacing = 24;
            float totalWidth = width * 3 + spacing * 2;
            float start = -totalWidth * 0.5f;
            float x = start + index * (width + spacing) + width * 0.5f;
            rectTransform.anchorMin = new Vector2(0.5f, 0);
            rectTransform.anchorMax = new Vector2(0.5f, 0);
            rectTransform.pivot = new Vector2(0.5f, 0);
            rectTransform.anchoredPosition = new Vector2(x + edgeOffset, 32);
            rectTransform.sizeDelta = new Vector2(width, 64);
        }

        private static void ConfigureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes =
                EditorBuildSettings.scenes
                    .Where(item => !string.Equals(item.path, BootScene, StringComparison.Ordinal)
                        && !string.Equals(item.path, ContentScene, StringComparison.Ordinal))
                    .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(BootScene, true));
            scenes.Insert(1, new EditorBuildSettingsScene(ContentScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
