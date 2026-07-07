using RFramework.Audio;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认音频辅助器。通过 Resource 模块同步加载 AudioClip。
    /// </summary>
    public class DefaultAudioHelper : AudioHelperBase
    {
        /// <inheritdoc cref="IAudioHelper.LoadAudioAsset"/>
        public override object LoadAudioAsset(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                return null;
            }

            // 同步加载（音频资源通常很小）
            AudioClip clip = Resources.Load<AudioClip>(assetName);
            if (clip == null)
            {
                Log.Warning("Audio clip '{0}' not found.", assetName);
            }

            return clip;
        }

        /// <inheritdoc cref="IAudioHelper.ReleaseAudioAsset"/>
        public override void ReleaseAudioAsset(object audioAsset)
        {
            if (audioAsset != null)
            {
                AudioClip clip = audioAsset as AudioClip;
                if (clip != null)
                {
                    Resources.UnloadAsset(clip);
                }
            }
        }
    }
}
