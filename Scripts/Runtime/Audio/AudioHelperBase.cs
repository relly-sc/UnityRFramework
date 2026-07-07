using RFramework.Audio;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 音频辅助器基类。
    /// </summary>
    public abstract class AudioHelperBase : MonoBehaviour, IAudioHelper
    {
        /// <inheritdoc cref="IAudioHelper.LoadAudioAsset"/>
        public abstract object LoadAudioAsset(string assetName);

        /// <inheritdoc cref="IAudioHelper.ReleaseAudioAsset"/>
        public abstract void ReleaseAudioAsset(object audioAsset);
    }
}
