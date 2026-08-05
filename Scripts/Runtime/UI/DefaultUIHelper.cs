using RFramework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认 UI 辅助器实现。使用纯 Unity API（Instantiate / Destroy）完成 UI 实例化和销毁。
    /// </summary>
    public class DefaultUIHelper : UIHelperBase
    {
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
