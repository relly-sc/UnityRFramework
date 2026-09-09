using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// UI 辅助器基类。继承自 MonoBehaviour 并实现 IUIHelper 接口。
    /// </summary>
    public abstract class UIHelperBase : MonoBehaviour, IUIHelper
    {
        /// <summary>
        /// 设置未匹配到专用层级时使用的 UI 根节点。
        /// </summary>
        public virtual void SetUIRoot(RectTransform uiRoot)
        {
        }

        /// <summary>
        /// 设置带根 Canvas 的 UI 未匹配专用层级时使用的持久化根节点。
        /// </summary>
        public virtual void SetCanvasRoot(RectTransform canvasRoot)
        {
        }

        /// <summary>
        /// 设置指定逻辑层级对应的 UI 容器。
        /// </summary>
        public virtual void SetLayerRoot(int windowLayer, RectTransform layerRoot)
        {
        }

        /// <summary>
        /// 设置带根 Canvas 的 UI 使用的专用层级容器。
        /// </summary>
        public virtual void SetCanvasLayerRoot(int windowLayer, RectTransform layerRoot)
        {
        }

        /// <inheritdoc cref="IUIHelper.InstantiateUI"/>
        public abstract object InstantiateUI(object uiAsset);

        /// <inheritdoc cref="IUIHelper.CreateUIForm"/>
        public abstract IUIForm CreateUIForm(object uiInstance, string assetName, int windowLayer, bool fullScreen);

        /// <inheritdoc cref="IUIHelper.ReleaseUI"/>
        public abstract void ReleaseUI(object uiInstance);
    }
}
