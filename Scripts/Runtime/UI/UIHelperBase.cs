using RFramework.UI;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// UI 辅助器基类。继承自 MonoBehaviour 并实现 IUIHelper 接口。
    /// </summary>
    public abstract class UIHelperBase : MonoBehaviour, IUIHelper
    {
        /// <inheritdoc cref="IUIHelper.InstantiateUI"/>
        public abstract object InstantiateUI(object uiAsset);

        /// <inheritdoc cref="IUIHelper.CreateUIForm"/>
        public abstract IUIForm CreateUIForm(object uiInstance, string assetName, int windowLayer, bool fullScreen);

        /// <inheritdoc cref="IUIHelper.ReleaseUI"/>
        public abstract void ReleaseUI(object uiInstance);
    }
}
