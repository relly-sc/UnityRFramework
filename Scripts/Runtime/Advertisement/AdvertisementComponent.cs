using System;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>统一广告入口；平台 SDK 通过 AdvertisementHelperBase 注入。</summary>
    [AddComponentMenu("UnityRFramework/Advertisement")]
    [DisallowMultipleComponent]
    public sealed class AdvertisementComponent : UnityRFrameworkComponent
    {
        private const string DefaultHelperTypeName =
            "UnityRFramework.Runtime.DefaultAdvertisementHelper";

        [SerializeField]
        [Tooltip("广告 Helper 类型全名。默认使用不支持广告的空实现。")]
        private string advertisementHelperTypeName = DefaultHelperTypeName;

        [SerializeField]
        [Tooltip("组件唤醒后自动初始化广告 Helper。")]
        private bool initializeOnAwake = true;

        private IAdvertisementModule advertisementModule;

        public string HelperName => advertisementModule?.HelperName ?? "None";
        public bool IsSupported => advertisementModule != null && advertisementModule.IsSupported;
        public bool IsInitialized => advertisementModule != null && advertisementModule.IsInitialized;

        protected override void Awake()
        {
            base.Awake();
            advertisementModule = RFrameworkModuleHost.Get<IAdvertisementModule>();
            InstallHelper();
            if (initializeOnAwake)
            {
                Initialize();
            }
        }

        public void SetHelper(AdvertisementHelperBase helper)
        {
            if (helper == null)
            {
                throw new RFrameworkException("Advertisement helper is invalid.");
            }

            advertisementModule.SetHelper(helper);
        }

        public void Initialize() => advertisementModule.Initialize();

        public void Load(AdvertisementType type, string placementId) =>
            advertisementModule.Load(type, placementId);

        public bool IsReady(AdvertisementType type, string placementId) =>
            advertisementModule.IsReady(type, placementId);

        public bool Show(AdvertisementType type, string placementId,
            Action<AdvertisementResult> callback = null) =>
            advertisementModule.Show(type, placementId, callback);

        public void Hide(AdvertisementType type, string placementId) =>
            advertisementModule.Hide(type, placementId);

        private void InstallHelper()
        {
            if (string.IsNullOrWhiteSpace(advertisementHelperTypeName))
            {
                advertisementHelperTypeName = DefaultHelperTypeName;
            }

            AdvertisementHelperBase helper = ComponentFactory.Create<AdvertisementHelperBase>(
                advertisementHelperTypeName, null);
            if (helper == null)
            {
                throw new RFrameworkException(
                    $"Advertisement helper '{advertisementHelperTypeName}' is missing or invalid.");
            }

            advertisementModule.SetHelper(helper);
            helper.transform.SetParent(transform);
        }
    }
}
