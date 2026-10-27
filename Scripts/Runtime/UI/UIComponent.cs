using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// UI 组件。作为 UIModule 的运行时包装层，绑定 Unity 生命周期，
    /// 转发所有 UI 操作到 UIModule。
    /// </summary>
    [AddComponentMenu("UnityRFramework/UI")]
    [DisallowMultipleComponent]
    public sealed class UIComponent : UnityRFrameworkComponent
    {
        [Serializable]
        private struct LayerRoot
        {
            public LayerRoot(int windowLayer, RectTransform root)
            {
                this.windowLayer = windowLayer;
                this.root = root;
            }

            [SerializeField]
            [Tooltip("逻辑窗口层级，需与打开 UI 时传入的 Window Layer 一致。")]
            private int windowLayer;

            [SerializeField]
            [Tooltip("该逻辑层级对应的 UGUI RectTransform 容器。")]
            private RectTransform root;

            public int WindowLayer => windowLayer;

            public RectTransform Root => root;
        }

        /// <summary>
        /// UI 模块引用。
        /// </summary>
        private IUIModule uiModule;

        private static readonly int[] DefaultLayers =
        {
            UILayer.Bottom,
            UILayer.HUD,
            UILayer.Panel,
            UILayer.Popup,
            UILayer.System,
            UILayer.Top
        };

        private static readonly string[] DefaultLayerNames =
        {
            "Bottom",
            "HUD",
            "Panel",
            "Popup",
            "System",
            "Top"
        };

        /// <summary>
        /// UI 辅助器类型名称。
        /// </summary>
        [SerializeField] private string uiHelperTypeName = "UnityRFramework.Runtime.DefaultUIHelper";

        /// <summary>
        /// 未匹配到专用层级时使用的 UI 根节点。留空保持原有场景根节点实例化行为。
        /// </summary>
        [SerializeField]
        [Tooltip("未匹配到专用层级时使用的 UI 根节点。留空时不自动设置父节点。")]
        private RectTransform uiRoot;

        /// <summary>
        /// 带根 Canvas 的 UI 使用的持久化根节点。未配置专用层级时回退到普通层级容器。
        /// </summary>
        [SerializeField]
        [Tooltip("带根 Canvas 的 UI 使用的持久化根节点。未配置专用层级时回退到普通层级容器。")]
        private RectTransform canvasRoot;

        /// <summary>
        /// 逻辑窗口层级与 UGUI 容器的对应关系。
        /// </summary>
        [SerializeField]
        [Tooltip("逻辑窗口层级与 UGUI RectTransform 容器的对应关系。")]
        private LayerRoot[] layerRoots = Array.Empty<LayerRoot>();

        /// <summary>
        /// 带根 Canvas 的 UI 使用的逻辑层级容器。
        /// </summary>
        [SerializeField]
        [Tooltip("带根 Canvas 的 UI 使用的逻辑层级容器。")]
        private LayerRoot[] canvasLayerRoots = Array.Empty<LayerRoot>();

        /// <summary>
        /// 获取当前打开的 UI 数量。
        /// </summary>
        public int UIFormCount
        {
            get { return uiModule != null ? uiModule.UIFormCount : 0; }
        }

        protected override void Awake()
        {
            base.Awake();

            // 旧 Prefab 可能只保留 UIComponent，根节点补齐不依赖模块注册顺序。
            EnsureLayerRoots();

            uiModule = RFrameworkModuleHost.Get<IUIModule>();
            if (uiModule == null)
            {
                Log.Error("Can not find module '{0}'.", nameof(IUIModule));
                return;
            }

            // 注入依赖模块
            IResourceModule resourceModule = RFrameworkModuleHost.Get<IResourceModule>();
            IEventModule eventModule = RFrameworkModuleHost.Get<IEventModule>();
            uiModule.SetDependencies(resourceModule, eventModule);

            // 创建并注入 UI 辅助器
            UIHelperBase uiHelper = ComponentFactory.Create<UIHelperBase>(uiHelperTypeName, null);
            if (uiHelper != null)
            {
                SetHelper(uiHelper);
                uiHelper.transform.SetParent(transform);
            }
        }

        /// <summary>
        /// 运行时替换 UI 辅助器。
        /// </summary>
        public void SetHelper(IUIHelper helper)
        {
            if (helper is UIHelperBase unityHelper)
            {
                EnsureLayerRoots();
                ConfigureLayerRoots(unityHelper);
            }

            // 允许在 Awake 完成模块注入前预配置辅助器，避免旧场景或编辑器测试触发空引用。
            if (uiModule != null)
            {
                uiModule.SetHelper(helper);
            }
        }

        private void EnsureLayerRoots()
        {
            if (uiRoot == null)
            {
                uiRoot = FindExistingRect("Canvas/UIRoot");
                if (uiRoot == null)
                {
                    RectTransform canvasTransform = EnsureDefaultCanvas();
                    uiRoot = CreateRectRoot("UIRoot", canvasTransform);
                }
                else
                {
                    EnsureCanvasComponents(uiRoot.parent.gameObject, false);
                }
            }

            if (canvasRoot == null)
            {
                canvasRoot = FindExistingRect("CanvasRoot")
                    ?? CreateRectRoot("CanvasRoot", transform);
            }

            layerRoots = EnsureDefaultLayerRoots(uiRoot, layerRoots);
            canvasLayerRoots = EnsureDefaultLayerRoots(canvasRoot, canvasLayerRoots);
        }

        private RectTransform EnsureDefaultCanvas()
        {
            Transform existing = transform.Find("Canvas");
            GameObject canvasObject = existing != null
                ? existing.gameObject
                : new GameObject("Canvas", typeof(RectTransform));
            if (canvasObject.transform.parent != transform)
            {
                canvasObject.transform.SetParent(transform, false);
            }

            EnsureCanvasComponents(canvasObject, existing == null);
            return canvasObject.GetComponent<RectTransform>();
        }

        private static void EnsureCanvasComponents(GameObject canvasObject, bool isNewCanvasObject)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0 && isNewCanvasObject)
            {
                canvasObject.layer = uiLayer;
            }

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            bool canvasAdded = canvas == null;
            if (canvas == null)
            {
                canvas = canvasObject.AddComponent<Canvas>();
            }

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            bool scalerAdded = scaler == null;
            if (scaler == null)
            {
                scaler = canvasObject.AddComponent<CanvasScaler>();
            }

            GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            bool raycasterAdded = raycaster == null;
            if (raycaster == null)
            {
                raycaster = canvasObject.AddComponent<GraphicRaycaster>();
            }

            if (isNewCanvasObject || canvasAdded)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.pixelPerfect = false;
                canvas.sortingOrder = 0;
                canvas.targetDisplay = 0;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
            }

            if (isNewCanvasObject || scalerAdded)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
                scaler.referencePixelsPerUnit = 100f;
            }

            if (isNewCanvasObject || raycasterAdded)
            {
                raycaster.ignoreReversedGraphics = true;
                raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
                raycaster.blockingMask = -1;
            }
        }

        private RectTransform FindExistingRect(string path)
        {
            Transform existing = transform.Find(path);
            return existing != null ? existing as RectTransform : null;
        }

        private static LayerRoot[] EnsureDefaultLayerRoots(
            RectTransform parent,
            LayerRoot[] configured)
        {
            List<LayerRoot> result = configured != null
                ? new List<LayerRoot>(configured)
                : new List<LayerRoot>();
            HashSet<int> existingLayers = new HashSet<int>();
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i].Root != null)
                {
                    existingLayers.Add(result[i].WindowLayer);
                }
            }

            for (int i = 0; i < DefaultLayers.Length; i++)
            {
                if (existingLayers.Contains(DefaultLayers[i]))
                {
                    continue;
                }

                RectTransform root = FindDirectRect(parent, DefaultLayerNames[i])
                    ?? CreateRectRoot(DefaultLayerNames[i], parent);
                result.Add(new LayerRoot(DefaultLayers[i], root));
            }

            return result.ToArray();
        }

        private static RectTransform FindDirectRect(RectTransform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform child = parent.GetChild(i) as RectTransform;
                if (child != null && child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static RectTransform CreateRectRoot(string name, Transform parent)
        {
            GameObject rootObject = new GameObject(name, typeof(RectTransform));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.localScale = Vector3.one;
            rootObject.layer = parent.gameObject.layer;
            return root;
        }

        private void ConfigureLayerRoots(UIHelperBase helper)
        {
            helper.SetUIRoot(uiRoot);
            helper.SetCanvasRoot(canvasRoot);

            HashSet<int> configuredLayers = new HashSet<int>();
            List<LayerRoot> orderedRoots = new List<LayerRoot>();
            HashSet<RectTransform> configuredRoots = new HashSet<RectTransform>();
            for (int i = 0; layerRoots != null && i < layerRoots.Length; i++)
            {
                LayerRoot layerRoot = layerRoots[i];
                if (layerRoot.Root == null)
                {
                    continue;
                }

                if (!configuredLayers.Add(layerRoot.WindowLayer))
                {
                    Log.Warning("Duplicate UI layer root '{0}' was ignored.", layerRoot.WindowLayer);
                    continue;
                }

                if (!configuredRoots.Add(layerRoot.Root))
                {
                    Log.Warning("UI layer root '{0}' is assigned to multiple layers and was ignored.",
                        layerRoot.Root.name);
                    continue;
                }

                if (uiRoot != null && layerRoot.Root.parent != uiRoot)
                {
                    Log.Warning(
                        "UI layer root '{0}' is not a direct child of UI Root and was ignored.",
                        layerRoot.WindowLayer);
                    continue;
                }

                helper.SetLayerRoot(layerRoot.WindowLayer, layerRoot.Root);
                if (uiRoot != null)
                {
                    orderedRoots.Add(layerRoot);
                }
            }

            HashSet<int> configuredCanvasLayers = new HashSet<int>();
            List<LayerRoot> orderedCanvasRoots = new List<LayerRoot>();
            HashSet<RectTransform> configuredCanvasRoots = new HashSet<RectTransform>();
            for (int i = 0; canvasLayerRoots != null && i < canvasLayerRoots.Length; i++)
            {
                LayerRoot layerRoot = canvasLayerRoots[i];
                if (layerRoot.Root == null)
                {
                    continue;
                }

                if (!configuredCanvasLayers.Add(layerRoot.WindowLayer))
                {
                    Log.Warning("Duplicate independent Canvas layer root '{0}' was ignored.",
                        layerRoot.WindowLayer);
                    continue;
                }

                if (!configuredCanvasRoots.Add(layerRoot.Root))
                {
                    Log.Warning("Independent Canvas root '{0}' is assigned to multiple layers and was ignored.",
                        layerRoot.Root.name);
                    continue;
                }

                if (canvasRoot != null && layerRoot.Root.parent != canvasRoot)
                {
                    Log.Warning(
                        "Independent Canvas layer root '{0}' is not a direct child of Canvas Root and was ignored.",
                        layerRoot.WindowLayer);
                    continue;
                }

                helper.SetCanvasLayerRoot(layerRoot.WindowLayer, layerRoot.Root);
                if (canvasRoot != null)
                {
                    orderedCanvasRoots.Add(layerRoot);
                }
            }

            orderedRoots.Sort((left, right) => left.WindowLayer.CompareTo(right.WindowLayer));
            for (int i = 0; i < orderedRoots.Count; i++)
            {
                orderedRoots[i].Root.SetAsLastSibling();
            }

            orderedCanvasRoots.Sort((left, right) => left.WindowLayer.CompareTo(right.WindowLayer));
            for (int i = 0; i < orderedCanvasRoots.Count; i++)
            {
                orderedCanvasRoots[i].Root.SetAsLastSibling();
            }
        }

        /// <inheritdoc cref="IUIModule.OpenUIFormAsync"/>
        public Task<IUIForm> OpenUIFormAsync(string assetName, int windowLayer = 0,
            bool fullScreen = false, uint priority = 0, object userData = null,
            CancellationToken ct = default)
        {
            return uiModule.OpenUIFormAsync(assetName, windowLayer, fullScreen, priority, userData, ct);
        }

        /// <summary>
        /// 将场景中已有的 UI 对象登记到 UI 模块。
        /// 场景 UI 参与窗口栈和生命周期管理，但模块不会销毁对象或卸载资源。
        /// </summary>
        /// <param name="uiInstance">场景中的 UI 对象。</param>
        /// <param name="formName">UI 表单唯一名称。</param>
        /// <param name="windowLayer">窗口层级。</param>
        /// <param name="fullScreen">是否为全屏窗口。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>登记后的 UI 表单。</returns>
        public IUIForm RegisterSceneUIForm(GameObject uiInstance, string formName, int windowLayer = 0,
            bool fullScreen = false, object userData = null)
        {
            if (uiInstance == null)
            {
                throw new RFrameworkException("Scene UI instance is invalid.");
            }

            UIForm uiForm = uiInstance.GetOrAddComponent<UIForm>();
            uiForm.Init(formName, windowLayer, fullScreen);
            uiModule.RegisterUIForm(formName, uiForm, userData);
            return uiForm;
        }

        /// <summary>
        /// 从 UI 模块注销场景 UI，不销毁对象或卸载资源。
        /// </summary>
        /// <param name="formName">UI 表单唯一名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void UnregisterSceneUIForm(string formName, object userData = null)
        {
            uiModule.UnregisterUIForm(formName, userData);
        }

        /// <inheritdoc cref="IUIModule.CloseUIForm"/>
        public void CloseUIForm(string assetName, object userData = null)
        {
            uiModule.CloseUIForm(assetName, userData);
        }

        /// <inheritdoc cref="IUIModule.CloseAllUIForms"/>
        public void CloseAllUIForms(object userData = null)
        {
            uiModule.CloseAllUIForms(userData);
        }

        /// <inheritdoc cref="IUIModule.HasUIForm"/>
        public bool HasUIForm(string assetName)
        {
            return uiModule.HasUIForm(assetName);
        }

        /// <inheritdoc cref="IUIModule.GetUIForm"/>
        public IUIForm GetUIForm(string assetName)
        {
            return uiModule.GetUIForm(assetName);
        }

        /// <inheritdoc cref="IUIModule.GetTopUIForm"/>
        public IUIForm GetTopUIForm()
        {
            return uiModule.GetTopUIForm();
        }

        /// <inheritdoc cref="IUIModule.CloseTopUIForm"/>
        public bool CloseTopUIForm(object userData = null)
        {
            return uiModule.CloseTopUIForm(userData);
        }
    }
}
