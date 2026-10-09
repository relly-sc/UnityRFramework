using System.Collections.Generic;
using RFramework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认 UI 辅助器实现。使用纯 Unity API（Instantiate / Destroy）完成 UI 实例化和销毁。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public class DefaultUIHelper : UIHelperBase
    {
        private readonly Dictionary<int, RectTransform> layerRoots =
            new Dictionary<int, RectTransform>();

        private readonly Dictionary<int, RectTransform> canvasLayerRoots =
            new Dictionary<int, RectTransform>();

        private RectTransform uiRoot;
        private RectTransform canvasRoot;

        /// <inheritdoc cref="UIHelperBase.SetUIRoot"/>
        public override void SetUIRoot(RectTransform root)
        {
            uiRoot = root;
            layerRoots.Clear();
            canvasLayerRoots.Clear();
        }

        /// <inheritdoc cref="UIHelperBase.SetCanvasRoot"/>
        public override void SetCanvasRoot(RectTransform root)
        {
            canvasRoot = root;
        }

        /// <inheritdoc cref="UIHelperBase.SetLayerRoot"/>
        public override void SetLayerRoot(int windowLayer, RectTransform layerRoot)
        {
            if (layerRoot == null)
            {
                layerRoots.Remove(windowLayer);
                return;
            }

            layerRoots[windowLayer] = layerRoot;
        }

        /// <inheritdoc cref="UIHelperBase.SetCanvasLayerRoot"/>
        public override void SetCanvasLayerRoot(int windowLayer, RectTransform layerRoot)
        {
            if (layerRoot == null)
            {
                canvasLayerRoots.Remove(windowLayer);
                return;
            }

            canvasLayerRoots[windowLayer] = layerRoot;
        }

        /// <inheritdoc cref="IUIHelper.InstantiateUI"/>
        public override object InstantiateUI(object uiAsset)
        {
            if (uiAsset == null)
            {
                Log.Error("UI asset is invalid.");
                return null;
            }

            GameObject prefab = uiAsset as GameObject;
            if (prefab == null)
            {
                Log.Error("UI asset '{0}' is not a GameObject.", uiAsset);
                return null;
            }

            return Object.Instantiate(prefab);
        }

        /// <inheritdoc cref="IUIHelper.CreateUIForm"/>
        public override IUIForm CreateUIForm(object uiInstance, string assetName, int windowLayer, bool fullScreen)
        {
            if (uiInstance == null)
            {
                Log.Error("UI instance is invalid.");
                return null;
            }

            GameObject go = uiInstance as GameObject;
            if (go == null)
            {
                Log.Error("UI instance '{0}' is not a GameObject.", uiInstance);
                return null;
            }

            UIForm uiForm = go.GetOrAddComponent<UIForm>();
            uiForm.Init(assetName, windowLayer, fullScreen);

            bool hasRootCanvas = go.GetComponent<Canvas>() != null;
            Dictionary<int, RectTransform> roots = hasRootCanvas ? canvasLayerRoots : layerRoots;
            RectTransform parent = roots.TryGetValue(windowLayer, out RectTransform layerRoot)
                && layerRoot != null
                ? layerRoot
                : hasRootCanvas ? canvasRoot : uiRoot;
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
                go.transform.SetAsLastSibling();
            }

            return uiForm;
        }

        /// <inheritdoc cref="IUIHelper.ReleaseUI"/>
        public override void ReleaseUI(object uiInstance)
        {
            if (uiInstance != null)
            {
                GameObject go = uiInstance as GameObject;
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }
        }
    }
}
