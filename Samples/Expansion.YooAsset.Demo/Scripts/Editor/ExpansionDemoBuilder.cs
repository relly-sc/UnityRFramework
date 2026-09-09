using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityRFramework.Expansion;
using UnityRFramework.Runtime;
using UnityRFramework.Sample;
using YooAsset;
using YooAsset.Editor;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 为官方 Demo 生成使用 Expansion 第三方 Helper 的覆盖层。
    /// 业务脚本、配置、UI 和场景继续复用 Demo Sample，不在本 Sample 中复制。
    /// </summary>
    public static class ExpansionDemoBuilder
    {
        private const string PackageName = "ExpansionDemoPackage";
        private const string GroupName = "ExpansionDemo";
        private const string ServerDirectoryName = "ExpansionDemoServer";
        private const string PackageNote = "ExpansionDemo Host update package";
        private const string PreloadTag = "preload";
        private const string OnDemandTag = "ondemand";
        private static string sampleRoot;
        private static string demoRoot;

        private static string Root => sampleRoot ?? (sampleRoot = FindOwnSampleRoot());

        private static string DemoRoot => demoRoot ?? (demoRoot = FindDemoSampleRoot());

        private static string GeneratedRoot => Root + "/Generated";

        private static string DemoResources => DemoRoot + "/GameAssets/Resources";

        private static string DemoScenes => DemoRoot + "/GameAssets/Scenes";

        private static string DemoFont =>
            DemoRoot + "/GameAssets/Fonts/Noto_Sans_SC/NotoSansSC-Regular.ttf";

        private static string OnDemandModel =>
            Root + "/GameAssets/OnDemand/Elastigirl/Elastigirl.fbx";

        private static string SourceBootScene => DemoScenes + "/DemoBoot.unity";

        private static string HallScene => DemoScenes + "/DemoHall.unity";

        private static string ExpeditionScene => DemoScenes + "/DemoExpedition.unity";

        /// <summary>
        /// 重建第三方 Helper 覆盖层、YooAsset 收集规则和 Build Settings。
        /// </summary>
        [MenuItem("UnityRFramework/ExpansionDemo/Rebuild Demo Overlay")]
        public static void Rebuild()
        {
            Rebuild(
                typeof(ExpansionDemoGameEntry),
                null,
                GeneratedRoot,
                "ExpansionDemoBoot.unity",
                PackageName,
                GroupName);
        }

        /// <summary>
        /// 重建覆盖层，并允许可选扩展替换启动入口和追加收集规则。
        /// </summary>
        internal static void Rebuild(
            Type gameEntryType,
            Action configureAdditionalCollectors,
            string generatedRoot,
            string bootSceneFileName,
            string packageName,
            string groupName)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(generatedRoot))
            {
                throw new ArgumentException(
                    "ExpansionDemoBuilder: 生成目录不能为空。",
                    nameof(generatedRoot));
            }

            if (string.IsNullOrWhiteSpace(bootSceneFileName)
                || !bootSceneFileName.EndsWith(
                    ".unity",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "ExpansionDemoBuilder: 启动场景文件名必须以 .unity 结尾。",
                    nameof(bootSceneFileName));
            }

            ValidatePackageSettings(packageName, groupName, null);

            string frameworkPrefab =
                generatedRoot + "/Prefabs/UnityRFramework.prefab";
            string bootScene =
                generatedRoot + "/Scenes/" + bootSceneFileName;

            ValidateDependencies();
            EnsureFolder(generatedRoot);
            EnsureFolder(generatedRoot + "/Prefabs");
            EnsureFolder(generatedRoot + "/Scenes");
            CreateFrameworkPrefab(frameworkPrefab, packageName);
            ConfigureYooAssetCollectors(packageName, groupName);
            configureAdditionalCollectors?.Invoke();
            CreateBootScene(gameEntryType, frameworkPrefab, bootScene);
            ConfigureBuildSettings(bootScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ExpansionDemo] Demo overlay rebuilt.");
        }

        /// <summary>
        /// 为当前平台构建新的 Host Package，并发布到独立 HFS 服务目录。
        /// 不复制任何文件到 StreamingAssets。
        /// </summary>
        [MenuItem("UnityRFramework/ExpansionDemo/Build Host Package")]
        public static void BuildHostPackage()
        {
            BuildHostPackage(
                null,
                PackageName,
                GroupName,
                ServerDirectoryName,
                PackageNote);
        }

        /// <summary>
        /// 构建 Host Package，并允许可选覆盖层在基础收集规则之后追加资源。
        /// </summary>
        /// <param name="configureAdditionalCollectors">追加收集规则的回调。</param>
        internal static void BuildHostPackage(
            Action configureAdditionalCollectors,
            string packageName,
            string groupName,
            string serverDirectoryName,
            string packageNote)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "ExpansionDemoBuilder: 不能在 Play Mode 中构建资源包。");
            }

            ValidatePackageSettings(packageName, groupName, serverDirectoryName);
            ValidateDependencies();
            ConfigureYooAssetCollectors(packageName, groupName);
            configureAdditionalCollectors?.Invoke();
            AssetDatabase.SaveAssets();

            string version = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
            string pipelineName = EBuildPipeline.ScriptableBuildPipeline.ToString();
            string shaderBundleName = DefaultBundlePackRule
                .CreateShadersPackRuleResult()
                .GetBundleName(
                    packageName,
                    BundleCollectorSettingData.Setting.UniqueBundleName);
            ScriptableBuildParameters parameters = new ScriptableBuildParameters
            {
                BuildOutputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot(),
                BundledFileRoot = BundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = pipelineName,
                BuildBundleType = (int)EBundleType.AssetBundle,
                BuildTarget = EditorUserBuildSettings.activeBuildTarget,
                PackageName = packageName,
                PackageVersion = version,
                PackageNote = packageNote,
                EnableSharePackRule = true,
                VerifyBuildingResult = true,
                FileNameStyle = BundleBuilderSetting.GetPackageFileNameStyle(
                    packageName,
                    pipelineName),
                BundledCopyOption = BundleBuilderSetting.GetPackageBundledCopyOption(
                    packageName,
                    pipelineName),
                BundledCopyParams = BundleBuilderSetting.GetPackageBundledCopyParams(
                    packageName,
                    pipelineName),
                CompressOption = BundleBuilderSetting.GetPackageCompressOption(
                    packageName,
                    pipelineName),
                ClearBuildCacheFiles = BundleBuilderSetting.GetPackageClearBuildCache(
                    packageName,
                    pipelineName),
                UseAssetDependencyDB = BundleBuilderSetting.GetPackageUseAssetDependencyDB(
                    packageName,
                    pipelineName),
                WriteLinkXML = true,
                BuiltinShadersBundleName = shaderBundleName,
                BundleEncryptor = CreateBuilderSettingInstance<IBundleEncryptor>(
                    BundleBuilderSetting.GetPackageBundleEncryptorClassName(
                        packageName,
                        pipelineName),
                    "Bundle encryptor"),
                ManifestEncryptor = CreateBuilderSettingInstance<IManifestEncryptor>(
                    BundleBuilderSetting.GetPackageManifestEncryptorClassName(
                        packageName,
                        pipelineName),
                    "Manifest encryptor"),
                ManifestDecryptor = CreateBuilderSettingInstance<IManifestDecryptor>(
                    BundleBuilderSetting.GetPackageManifestDecryptorClassName(
                        packageName,
                        pipelineName),
                    "Manifest decryptor")
            };

            ScriptableBuildPipeline pipeline = new ScriptableBuildPipeline();
            BuildResult result = pipeline.Run(parameters, true);
            if (!result.Success)
            {
                throw new InvalidOperationException(
                    "ExpansionDemoBuilder: YooAsset Package 构建失败。"
                    + $" Task: {result.FailedTask}, Error: {result.ErrorInfo}");
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException(
                    "ExpansionDemoBuilder: 无法定位 Unity 工程根目录。");
            }

            string serverRoot = Path.Combine(
                projectRoot,
                "Bundles",
                serverDirectoryName);
            PublishPackage(result.OutputPackageDirectory, serverRoot);
            Debug.Log(
                $"[ExpansionDemo] Host package '{version}' published to '{serverRoot}'.");
            EditorUtility.RevealInFinder(serverRoot);
        }

        private static T CreateBuilderSettingInstance<T>(
            string className,
            string settingName)
            where T : class
        {
            Type classType = EditorAssemblyUtility
                .GetAssignableTypes(typeof(T))
                .FirstOrDefault(type => string.Equals(
                    type.FullName,
                    className,
                    StringComparison.Ordinal));
            if (classType == null)
            {
                throw new InvalidOperationException(
                    $"ExpansionDemoBuilder: {settingName} type not found: "
                    + $"'{className}'. Please correct it in YooAsset Bundle Builder.");
            }

            return Activator.CreateInstance(classType) as T
                   ?? throw new InvalidOperationException(
                       $"ExpansionDemoBuilder: failed to create {settingName} "
                       + $"'{className}'.");
        }

        private static void PublishPackage(string sourceRoot, string targetRoot)
        {
            Directory.CreateDirectory(targetRoot);
            foreach (string sourceFile in Directory.GetFiles(
                         sourceRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                string relativePath = sourceFile.Substring(sourceRoot.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string targetFile = Path.Combine(targetRoot, relativePath);
                string targetDirectory = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                File.Copy(sourceFile, targetFile, true);
            }
        }

        private static void ValidateDependencies()
        {
            if (AssetDatabase.LoadAssetAtPath<MonoScript>(
                    DemoRoot + "/Scripts/Runtime/DemoGameEntry.cs") == null)
            {
                throw new InvalidOperationException(
                    "ExpansionDemoBuilder: Demo Sample 未完整导入。");
            }

            if (!AssetDatabase.IsValidFolder(DemoResources)
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceBootScene) == null
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(HallScene) == null
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(ExpeditionScene) == null
                || AssetDatabase.LoadAssetAtPath<GameObject>(OnDemandModel) == null
                || AssetDatabase.LoadAssetAtPath<Font>(DemoFont) == null)
            {
                throw new InvalidOperationException(
                    "ExpansionDemoBuilder: Demo 资源、场景或按需验证模型不完整。");
            }

            if (FindType("UnityRFramework.Expansion.YooAssetResourceHelper") == null
                || FindType("UnityRFramework.Expansion.UniTaskWebRequestHelper") == null)
            {
                throw new InvalidOperationException(
                    "ExpansionDemoBuilder: 请先导入 Expansion.YooAsset 与 Expansion.UniTask Sample 并完成第三方依赖安装。");
            }
        }

        private static void ValidatePackageSettings(
            string packageName,
            string groupName,
            string serverDirectoryName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException(
                    "ExpansionDemoBuilder: Package 名称不能为空。",
                    nameof(packageName));
            }

            if (string.IsNullOrWhiteSpace(groupName))
            {
                throw new ArgumentException(
                    "ExpansionDemoBuilder: Group 名称不能为空。",
                    nameof(groupName));
            }

            if (serverDirectoryName != null
                && string.IsNullOrWhiteSpace(serverDirectoryName))
            {
                throw new ArgumentException(
                    "ExpansionDemoBuilder: Host 发布目录名称不能为空。",
                    nameof(serverDirectoryName));
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static string FindOwnSampleRoot()
        {
            return FindSampleRoot(
                "ExpansionDemoBuilder t:MonoScript",
                "/Scripts/Editor/ExpansionDemoBuilder.cs",
                "ExpansionDemoBuilder: 无法定位 ExpansionDemo Sample 根目录。");
        }

        private static string FindDemoSampleRoot()
        {
            return FindSampleRoot(
                "DemoGameEntry t:MonoScript",
                "/Scripts/Runtime/DemoGameEntry.cs",
                "ExpansionDemoBuilder: 请先导入 Sample.Demo。");
        }

        private static string FindSampleRoot(
            string filter,
            string scriptSuffix,
            string errorMessage)
        {
            string[] guids = AssetDatabase.FindAssets(filter);
            foreach (string guid in guids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!scriptPath.EndsWith(scriptSuffix, StringComparison.Ordinal))
                {
                    continue;
                }

                string directory = Path.GetDirectoryName(scriptPath);
                int depth = scriptSuffix.Count(character => character == '/');
                for (int i = 1; i < depth; i++)
                {
                    directory = Path.GetDirectoryName(directory);
                }

                if (!string.IsNullOrEmpty(directory))
                {
                    return directory.Replace('\\', '/');
                }
            }

            throw new InvalidOperationException(errorMessage);
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

        private static void CreateFrameworkPrefab(
            string frameworkPrefab,
            string packageName)
        {
            GeneratedResourceSettings resourceSettings =
                ReadGeneratedResourceSettings(frameworkPrefab);
            string sourcePath = FindFrameworkPrefab();
            GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                ResourceComponent resource =
                    root.GetComponentInChildren<ResourceComponent>(true);
                WebRequestComponent webRequest =
                    root.GetComponentInChildren<WebRequestComponent>(true);
                LocalizationComponent localization =
                    root.GetComponentInChildren<LocalizationComponent>(true);

                SetSerializedValue(
                    resource,
                    "resourceHelperTypeName",
                    "UnityRFramework.Expansion.YooAssetResourceHelper");
                SetSerializedValue(
                    resource,
                    "playMode",
                    resourceSettings.PlayMode);
                SetSerializedValue(resource, "packageName", packageName);
                SetSerializedValue(
                    resource,
                    "defaultHostServer",
                    resourceSettings.DefaultHostServer);
                SetSerializedValue(
                    resource,
                    "fallbackHostServer",
                    resourceSettings.FallbackHostServer);
                SetSerializedValue(
                    webRequest,
                    "webRequestHelperTypeName",
                    "UnityRFramework.Expansion.UniTaskWebRequestHelper");
                SetSerializedValue(localization, "loadDefaultLanguageOnStart", false);
                CreateOnDemandProbe(root.transform);

                PrefabUtility.SaveAsPrefabAsset(root, frameworkPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GeneratedResourceSettings ReadGeneratedResourceSettings(
            string frameworkPrefab)
        {
            GameObject existing =
                AssetDatabase.LoadAssetAtPath<GameObject>(frameworkPrefab);
            if (existing == null)
            {
                return new GeneratedResourceSettings(0, string.Empty, string.Empty);
            }

            ResourceComponent resource =
                existing.GetComponentInChildren<ResourceComponent>(true);
            if (resource == null)
            {
                return new GeneratedResourceSettings(0, string.Empty, string.Empty);
            }

            SerializedObject serializedResource = new SerializedObject(resource);
            return new GeneratedResourceSettings(
                serializedResource.FindProperty("playMode").intValue,
                serializedResource.FindProperty("defaultHostServer").stringValue,
                serializedResource.FindProperty("fallbackHostServer").stringValue);
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
            else if (value is UnityEngine.Object objectValue)
            {
                property.objectReferenceValue = objectValue;
            }
            else
            {
                throw new ArgumentException(
                    $"Unsupported serialized value type '{value?.GetType().FullName}'.",
                    nameof(value));
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void ConfigureYooAssetCollectors(
            string packageName,
            string groupName)
        {
            BundleCollectorSetting setting = BundleCollectorSettingData.Setting;
            BundleCollectorPackage package = setting.Packages.FirstOrDefault(
                item => string.Equals(item.PackageName, packageName, StringComparison.Ordinal));
            if (package == null)
            {
                package = BundleCollectorSettingData.CreatePackage(packageName);
            }

            package.EnableAddressable = true;
            package.AutoCollectShaders = true;
            package.IgnoreRuleName = nameof(NormalIgnoreRule);

            BundleCollectorGroup group = package.Groups.FirstOrDefault(
                item => string.Equals(item.GroupName, groupName, StringComparison.Ordinal));
            if (group == null)
            {
                group = BundleCollectorSettingData.CreateGroup(package, groupName);
            }

            group.Collectors.Clear();
            group.Collectors.Add(CreateCollector(
                DemoResources,
                nameof(ExpansionDemoResourcesAddressRule),
                nameof(ExpansionDemoShortPackRule),
                nameof(CollectAll),
                PreloadTag));
            group.Collectors.Add(CreateCollector(
                DemoScenes,
                nameof(AddressByFileName),
                nameof(ExpansionDemoShortPackRule),
                nameof(CollectScene),
                PreloadTag));
            group.Collectors.Add(CreateCollector(
                OnDemandModel,
                nameof(ExpansionDemoOnDemandAddressRule),
                nameof(ExpansionDemoShortPackRule),
                nameof(CollectAll),
                OnDemandTag));

            BundleCollectorSettingData.ModifyPackage(package);
            BundleCollectorSettingData.ModifyGroup(package, group);
            BundleCollectorSettingData.FixFile();
            BundleCollectorSettingData.SaveFile();
        }

        private static BundleCollector CreateCollector(
            string collectPath,
            string addressRuleName,
            string packRuleName,
            string filterRuleName,
            string assetTags)
        {
            return new BundleCollector
            {
                CollectPath = collectPath,
                CollectorGUID = AssetDatabase.AssetPathToGUID(collectPath),
                CollectorType = ECollectorType.MainAssetCollector,
                AddressRuleName = addressRuleName,
                PackRuleName = packRuleName,
                FilterRuleName = filterRuleName,
                AssetTags = assetTags
            };
        }

        private static void CreateOnDemandProbe(Transform frameworkRoot)
        {
            Transform existing = frameworkRoot.Find("ExpansionDemoOnDemandProbe");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            GameObject probeRoot = new GameObject("ExpansionDemoOnDemandProbe");
            probeRoot.transform.SetParent(frameworkRoot, false);
            ExpansionDemoOnDemandProbe probe =
                probeRoot.AddComponent<ExpansionDemoOnDemandProbe>();

            GameObject canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(probeRoot.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panel = CreateImage(
                canvasObject.transform,
                "Panel",
                new Color(0.07f, 0.09f, 0.12f, 0.96f));
            SetRect(
                panel.GetComponent<RectTransform>(),
                Vector2.one,
                Vector2.one,
                new Vector2(380f, 500f),
                new Vector2(-1493f, -725f));

            Text title = CreateText(
                panel.transform,
                "Title",
                "YooAsset 按需加载验证",
                24,
                TextAnchor.MiddleCenter,
                Color.white);
            SetRect(
                title.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(340f, 46f),
                new Vector2(0f, -30f));

            GameObject previewBackground = CreateImage(
                panel.transform,
                "PreviewBackground",
                new Color(0.025f, 0.035f, 0.05f, 1f));
            SetRect(
                previewBackground.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(330f, 290f),
                new Vector2(0f, -200f));

            GameObject previewObject = new GameObject(
                "Preview",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            previewObject.transform.SetParent(previewBackground.transform, false);
            RawImage previewImage = previewObject.GetComponent<RawImage>();
            previewImage.color = Color.white;
            Stretch(previewObject.GetComponent<RectTransform>());

            Text status = CreateText(
                panel.transform,
                "Status",
                "点击按钮验证运行时按需下载。",
                18,
                TextAnchor.MiddleCenter,
                new Color(0.80f, 0.85f, 0.90f, 1f));
            SetRect(
                status.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(340f, 72f),
                new Vector2(0f, 94f));

            Button loadButton = CreateButton(
                panel.transform,
                "LoadButton",
                "加载远程模型",
                new Color(0.12f, 0.50f, 0.30f, 1f),
                out Text loadButtonText);
            SetRect(
                loadButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(240f, 52f),
                new Vector2(0f, 40f));

            GameObject previewRoot = new GameObject("PreviewRoot");
            previewRoot.transform.SetParent(probeRoot.transform, false);
            previewRoot.transform.position = new Vector3(10000f, 10000f, 10000f);

            GameObject cameraObject = new GameObject("PreviewCamera", typeof(Camera));
            cameraObject.transform.SetParent(probeRoot.transform, false);
            Camera previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.025f, 0.035f, 0.05f, 1f);
            previewCamera.cullingMask = 1 << 31;
            previewCamera.orthographic = true;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = false;

            SetSerializedValue(probe, "panel", panel);
            SetSerializedValue(probe, "statusText", status);
            SetSerializedValue(probe, "loadButton", loadButton);
            SetSerializedValue(probe, "loadButtonText", loadButtonText);
            SetSerializedValue(probe, "previewImage", previewImage);
            SetSerializedValue(probe, "previewCamera", previewCamera);
            SetSerializedValue(probe, "previewRoot", previewRoot.transform);
        }

        private static void CreateBootScene(
            Type gameEntryType,
            string frameworkPrefab,
            string bootScene)
        {
            if (File.Exists(bootScene))
            {
                FileUtil.ReplaceFile(SourceBootScene, bootScene);
            }
            else
            {
                FileUtil.CopyFileOrDirectory(SourceBootScene, bootScene);
            }

            AssetDatabase.ImportAsset(
                bootScene,
                ImportAssetOptions.ForceSynchronousImport
                | ImportAssetOptions.ForceUpdate);
            Scene scene = EditorSceneManager.OpenScene(bootScene, OpenSceneMode.Single);
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                DemoGameEntry demoEntry =
                    rootObject.GetComponentInChildren<DemoGameEntry>(true);
                if (demoEntry != null)
                {
                    UnityEngine.Object.DestroyImmediate(demoEntry.gameObject);
                    continue;
                }

                if (string.Equals(
                        rootObject.name,
                        "UnityRFramework",
                        StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(rootObject);
                }
            }

            GameObject framework =
                AssetDatabase.LoadAssetAtPath<GameObject>(frameworkPrefab);
            if (framework == null)
            {
                throw new InvalidOperationException(
                    "ExpansionDemoBuilder: 生成的框架预制体不存在。");
            }

            PrefabUtility.InstantiatePrefab(framework, scene);
            CreateUpdateBootstrap(scene, gameEntryType);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "ExpansionDemoBuilder: 保存第三方启动场景失败。");
            }
        }

        private static void CreateUpdateBootstrap(Scene scene, Type gameEntryType)
        {
            GameObject bootstrap = new GameObject("ExpansionDemoBootstrap");
            SceneManager.MoveGameObjectToScene(bootstrap, scene);
            if (gameEntryType == null
                || !typeof(ExpansionDemoGameEntry).IsAssignableFrom(gameEntryType))
            {
                throw new InvalidOperationException(
                    "ExpansionDemoBuilder: 启动入口必须继承 ExpansionDemoGameEntry。");
            }

            ExpansionDemoGameEntry entry =
                (ExpansionDemoGameEntry)bootstrap.AddComponent(gameEntryType);

            GameObject canvasObject = new GameObject(
                "ResourceUpdateCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panel = CreateImage(
                canvasObject.transform,
                "UpdatePanel",
                new Color(0.055f, 0.075f, 0.10f, 1f));
            Stretch(panel.GetComponent<RectTransform>());

            GameObject content = CreateImage(
                panel.transform,
                "Content",
                new Color(0.11f, 0.14f, 0.18f, 1f));
            SetRect(
                content.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(760f, 460f),
                Vector2.zero);

            Text status = CreateText(
                content.transform,
                "Status",
                "正在检查资源更新",
                36,
                TextAnchor.MiddleCenter,
                Color.white);
            SetRect(
                status.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(680f, 70f),
                new Vector2(0f, -65f));

            Text detail = CreateText(
                content.transform,
                "Detail",
                "正在连接资源服务器...",
                24,
                TextAnchor.MiddleCenter,
                new Color(0.78f, 0.83f, 0.88f, 1f));
            SetRect(
                detail.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(680f, 120f),
                new Vector2(0f, -160f));

            GameObject progressBackground = CreateImage(
                content.transform,
                "ProgressBackground",
                new Color(0.035f, 0.045f, 0.06f, 1f));
            SetRect(
                progressBackground.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(620f, 34f),
                new Vector2(0f, -30f));

            GameObject fillObject = CreateImage(
                progressBackground.transform,
                "Fill",
                new Color(0.13f, 0.63f, 0.39f, 1f));
            Stretch(fillObject.GetComponent<RectTransform>());
            Image fill = fillObject.GetComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;

            Text progress = CreateText(
                progressBackground.transform,
                "Progress",
                "检查中",
                20,
                TextAnchor.MiddleCenter,
                Color.white);
            Stretch(progress.rectTransform);

            Button primary = CreateButton(
                content.transform,
                "PrimaryButton",
                "检查中...",
                new Color(0.12f, 0.50f, 0.30f, 1f),
                out Text primaryText);
            SetRect(
                primary.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(250f, 58f),
                new Vector2(-145f, 65f));

            Button quit = CreateButton(
                content.transform,
                "QuitButton",
                "退出",
                new Color(0.33f, 0.37f, 0.42f, 1f),
                out _);
            SetRect(
                quit.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(160f, 58f),
                new Vector2(145f, 65f));

            SetSerializedValue(entry, "updatePanel", panel);
            SetSerializedValue(entry, "statusText", status);
            SetSerializedValue(entry, "detailText", detail);
            SetSerializedValue(entry, "progressFill", fill);
            SetSerializedValue(entry, "progressText", progress);
            SetSerializedValue(entry, "primaryButton", primary);
            SetSerializedValue(entry, "primaryButtonText", primaryText);
            SetSerializedValue(entry, "quitButton", quit);

            GameObject eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }

        private static GameObject CreateImage(
            Transform parent,
            string name,
            Color color)
        {
            GameObject gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            gameObject.GetComponent<Image>().color = color;
            return gameObject;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            GameObject gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>(DemoFont);
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Color color,
            out Text labelText)
        {
            GameObject gameObject = CreateImage(parent, name, color);
            Button button = gameObject.AddComponent<Button>();
            button.targetGraphic = gameObject.GetComponent<Image>();
            labelText = CreateText(
                gameObject.transform,
                "Text",
                label,
                24,
                TextAnchor.MiddleCenter,
                Color.white);
            Stretch(labelText.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void SetRect(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size,
            Vector2 position)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = position;
        }

        private static void ConfigureBuildSettings(string bootScene)
        {
            HashSet<string> managedScenes = new HashSet<string>(StringComparer.Ordinal)
            {
                bootScene,
                SourceBootScene,
                HallScene,
                ExpeditionScene
            };
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(item => !managedScenes.Contains(item.path)
                    && !IsGeneratedExpansionBootScene(item.path))
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(bootScene, true));
            scenes.Insert(1, new EditorBuildSettingsScene(HallScene, true));
            scenes.Insert(2, new EditorBuildSettingsScene(ExpeditionScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static bool IsGeneratedExpansionBootScene(string scenePath)
        {
            string fileName = Path.GetFileName(scenePath);
            return string.Equals(
                    fileName,
                    "ExpansionDemoBoot.unity",
                    StringComparison.Ordinal)
                || string.Equals(
                    fileName,
                    "ExpansionHybridCLRDemoBoot.unity",
                    StringComparison.Ordinal);
        }

        private readonly struct GeneratedResourceSettings
        {
            /// <summary>
            /// 创建一份需要跨覆盖层重建保留的资源配置快照。
            /// </summary>
            public GeneratedResourceSettings(
                int playMode,
                string defaultHostServer,
                string fallbackHostServer)
            {
                PlayMode = playMode;
                DefaultHostServer = defaultHostServer ?? string.Empty;
                FallbackHostServer = fallbackHostServer ?? string.Empty;
            }

            /// <summary>获取资源运行模式枚举值。</summary>
            public int PlayMode { get; }

            /// <summary>获取主资源服务器地址。</summary>
            public string DefaultHostServer { get; }

            /// <summary>获取备用资源服务器地址。</summary>
            public string FallbackHostServer { get; }
        }
    }

    /// <summary>
    /// 将 Demo Resources 目录中的资源映射为原 Demo 使用的相对路径。
    /// 文本和音频保留扩展名，Prefab 等 Unity 对象移除扩展名。
    /// </summary>
    public sealed class ExpansionDemoResourcesAddressRule : IAddressRule
    {
        /// <inheritdoc />
        string IAddressRule.GetAssetAddress(AddressRuleData data)
        {
            const string marker = "/Resources/";
            string path = data.AssetPath.Replace('\\', '/');
            int markerIndex = path.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                throw new InvalidOperationException(
                    $"ExpansionDemo Resources address requires a Resources path: '{path}'.");
            }

            string relativePath = path.Substring(markerIndex + marker.Length);
            string extension = Path.GetExtension(relativePath);
            if (string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".mat", StringComparison.OrdinalIgnoreCase))
            {
                return relativePath.Substring(0, relativePath.Length - extension.Length);
            }

            return relativePath;
        }
    }

    /// <summary>
    /// 为按需下载验证模型提供稳定且不依赖源文件名的 YooAsset 地址。
    /// </summary>
    public sealed class ExpansionDemoOnDemandAddressRule : IAddressRule
    {
        /// <inheritdoc />
        string IAddressRule.GetAssetAddress(AddressRuleData data)
        {
            return "ExpansionDemo/OnDemandModel";
        }
    }

    /// <summary>
    /// 使用 Unity Asset GUID 生成与 Sample 安装目录无关的短资源包名称。
    /// </summary>
    public sealed class ExpansionDemoShortPackRule : IBundlePackRule
    {
        /// <inheritdoc />
        BundlePackRuleResult IBundlePackRule.GetPackRuleResult(
            BundlePackRuleData data)
        {
            string guid = AssetDatabase.AssetPathToGUID(data.AssetPath);
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException(
                    $"ExpansionDemo asset has no valid GUID: '{data.AssetPath}'.");
            }

            return new BundlePackRuleResult(
                "asset_" + guid,
                DefaultBundlePackRule.AssetBundleFileExtension);
        }
    }
}
