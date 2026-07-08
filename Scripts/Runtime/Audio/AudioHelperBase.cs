using System;
using RFramework.Audio;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 音频辅助器基类。子类需实现音频加载/释放/播放全部引擎特定操作。
    /// </summary>
    public abstract class AudioHelperBase : MonoBehaviour, IAudioHelper
    {
        /// <inheritdoc cref="IAudioHelper.LoadAudioAsset"/>
        public abstract object LoadAudioAsset(string assetName);

        /// <inheritdoc cref="IAudioHelper.ReleaseAudioAsset"/>
        public abstract void ReleaseAudioAsset(object audioAsset);

        /// <inheritdoc cref="IAudioHelper.PlayBgm"/>
        public abstract void PlayBgm(object audioAsset, float volume, bool loop,
            float fadeInSeconds, float completeDelaySeconds, Action onComplete);

        /// <inheritdoc cref="IAudioHelper.StopBgm"/>
        public abstract void StopBgm(float fadeOutSeconds);

        /// <inheritdoc cref="IAudioHelper.PauseBgm"/>
        public abstract void PauseBgm();

        /// <inheritdoc cref="IAudioHelper.ResumeBgm"/>
        public abstract void ResumeBgm();

        /// <inheritdoc cref="IAudioHelper.SetBgmVolume"/>
        public abstract void SetBgmVolume(float volume);

        /// <inheritdoc cref="IAudioHelper.PlaySfx"/>
        public abstract void PlaySfx(int handleId, object audioAsset, float volume,
            float completeDelaySeconds, Action onComplete);

        /// <inheritdoc cref="IAudioHelper.StopSfx"/>
        public abstract void StopSfx(int handleId);

        /// <inheritdoc cref="IAudioHelper.StopAllSfx"/>
        public abstract void StopAllSfx();

        /// <inheritdoc cref="IAudioHelper.PlayUI"/>
        public abstract void PlayUI(object audioAsset, float volume);
    }
}
