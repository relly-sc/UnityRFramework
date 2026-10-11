using System;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>广告平台 Helper 基类，平台扩展通过继承它接入 SDK。</summary>
    public abstract class AdvertisementHelperBase : MonoBehaviour, IAdvertisementHelper
    {
        public abstract string HelperName { get; }
        public abstract bool IsSupported { get; }
        public abstract bool IsInitialized { get; }
        public abstract void Initialize();
        public abstract void Load(AdvertisementType type, string placementId);
        public abstract bool IsReady(AdvertisementType type, string placementId);
        public abstract bool Show(AdvertisementType type, string placementId,
            Action<AdvertisementResult> callback);
        public abstract void Hide(AdvertisementType type, string placementId);
    }
}
