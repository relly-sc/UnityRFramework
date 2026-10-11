using System;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认空实现。Steam、未安装广告 SDK 的平台和关闭广告的平台均使用它。
    /// </summary>
    public sealed class DefaultAdvertisementHelper : AdvertisementHelperBase
    {
        private bool initialized;

        public override string HelperName => nameof(DefaultAdvertisementHelper);
        public override bool IsSupported => false;
        public override bool IsInitialized => initialized;

        public override void Initialize()
        {
            initialized = true;
        }

        public override void Load(AdvertisementType type, string placementId) { }

        public override bool IsReady(AdvertisementType type, string placementId)
        {
            return false;
        }

        public override bool Show(AdvertisementType type, string placementId,
            Action<AdvertisementResult> callback)
        {
            callback?.Invoke(AdvertisementResult.NotSupported);
            return false;
        }

        public override void Hide(AdvertisementType type, string placementId) { }
    }
}
