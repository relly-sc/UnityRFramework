using System;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 音频组件。仅负责获取模块、注入依赖和创建辅助器，
    /// 所有 AudioSource 操作委托给 IAudioHelper（默认 DefaultAudioHelper）。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Audio")]
    [DisallowMultipleComponent]
    public sealed class AudioComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 音频模块核心引用（接口，不依赖具体实现）。
        /// </summary>
        private IAudioModule audioModule;

        /// <summary>
        /// 音频辅助器类型名（Inspector 配置）。
        /// </summary>
        [SerializeField]
        private string audioHelperTypeName = "UnityRFramework.Runtime.DefaultAudioHelper";

        /// <summary>
        /// BGM 音量（0~1），转发到模块。
        /// </summary>
        public float BgmVolume
        {
            get { return audioModule != null ? audioModule.BgmVolume : 1f; }
            set { if (audioModule != null) { audioModule.BgmVolume = value; } }
        }

        /// <summary>
        /// SFX 音量（0~1），转发到模块。
        /// </summary>
        public float SfxVolume
        {
            get { return audioModule != null ? audioModule.SfxVolume : 1f; }
            set { if (audioModule != null) { audioModule.SfxVolume = value; } }
        }

        /// <summary>
        /// UI 音效音量（0~1），转发到模块。
        /// </summary>
        public float UIVolume
        {
            get { return audioModule != null ? audioModule.UIVolume : 1f; }
            set { if (audioModule != null) { audioModule.UIVolume = value; } }
        }

        /// <summary>
        /// 全局静音开关，转发到模块。
        /// </summary>
        public bool Muted
        {
            get { return audioModule != null && audioModule.Muted; }
            set { if (audioModule != null) { audioModule.Muted = value; } }
        }

        /// <summary>
        /// 获取已缓存音频资源数量。
        /// </summary>
        public int LoadedAudioAssetCount
        {
            get { return audioModule != null ? audioModule.LoadedAudioAssetCount : 0; }
        }

        /// <summary>
        /// 获取当前 BGM 资源路径；未播放时返回 null。
        /// </summary>
        public string CurrentBgmAssetName
        {
            get { return audioModule != null ? audioModule.CurrentBgmAssetName : null; }
        }

        /// <summary>
        /// 获取当前 BGM 是否处于暂停状态。
        /// </summary>
        public bool IsBgmPaused
        {
            get { return audioModule != null && audioModule.IsBgmPaused; }
        }

        /// <summary>同步播放 Resources 中的 BGM。</summary>
        /// <param name="assetName">音频资源路径。</param>
        /// <param name="volume">音量倍率。</param>
        /// <param name="loop">是否循环。</param>
        /// <param name="fadeInSeconds">淡入时长。</param>
        /// <param name="completeDelaySeconds">完成回调延迟。</param>
        /// <param name="onComplete">完成回调。</param>
        /// <returns>播放句柄。</returns>
        public AudioHandle PlayBgm(string assetName, float volume = 1f, bool loop = true,
            float fadeInSeconds = 0f, float completeDelaySeconds = 0f,
            Action onComplete = null)
        {
            return audioModule.PlayBgm(assetName, volume, loop, fadeInSeconds,
                completeDelaySeconds, onComplete);
        }

        /// <summary>异步加载并播放本地文件或 Resources 中的 BGM。</summary>
        /// <param name="assetName">音频资源路径。</param>
        /// <param name="volume">音量倍率。</param>
        /// <param name="loop">是否循环。</param>
        /// <param name="fadeInSeconds">淡入时长。</param>
        /// <param name="completeDelaySeconds">完成回调延迟。</param>
        /// <param name="onComplete">完成回调。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>播放句柄任务。</returns>
        public Task<AudioHandle> PlayBgmAsync(string assetName, float volume = 1f,
            bool loop = true, float fadeInSeconds = 0f, float completeDelaySeconds = 0f,
            Action onComplete = null, CancellationToken ct = default)
        {
            return audioModule.PlayBgmAsync(assetName, volume, loop, fadeInSeconds,
                completeDelaySeconds, onComplete, ct);
        }

        /// <summary>同步播放 Resources 中的 SFX。</summary>
        /// <param name="assetName">音频资源路径。</param>
        /// <param name="volume">音量倍率。</param>
        /// <param name="completeDelaySeconds">完成回调延迟。</param>
        /// <param name="onComplete">完成回调。</param>
        /// <returns>播放句柄。</returns>
        public AudioHandle PlaySfx(string assetName, float volume = 1f,
            float completeDelaySeconds = 0f, Action onComplete = null)
        {
            return audioModule.PlaySfx(assetName, volume, completeDelaySeconds, onComplete);
        }

        /// <summary>异步加载并播放本地文件或 Resources 中的 SFX。</summary>
        /// <param name="assetName">音频资源路径。</param>
        /// <param name="volume">音量倍率。</param>
        /// <param name="completeDelaySeconds">完成回调延迟。</param>
        /// <param name="onComplete">完成回调。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>播放句柄任务。</returns>
        public Task<AudioHandle> PlaySfxAsync(string assetName, float volume = 1f,
            float completeDelaySeconds = 0f, Action onComplete = null,
            CancellationToken ct = default)
        {
            return audioModule.PlaySfxAsync(
                assetName, volume, completeDelaySeconds, onComplete, ct);
        }

        /// <summary>同步播放 Resources 中的 UI 音效。</summary>
        /// <param name="assetName">音频资源路径。</param>
        /// <param name="volume">音量倍率。</param>
        public void PlayUI(string assetName, float volume = 1f)
        {
            audioModule.PlayUI(assetName, volume);
        }

        /// <summary>异步加载并播放本地文件或 Resources 中的 UI 音效。</summary>
        /// <param name="assetName">音频资源路径。</param>
        /// <param name="volume">音量倍率。</param>
        /// <param name="ct">取消令牌。</param>
        public Task PlayUIAsync(string assetName, float volume = 1f,
            CancellationToken ct = default)
        {
            return audioModule.PlayUIAsync(assetName, volume, ct);
        }

        /// <summary>
        /// 停止所有播放并清空音频缓存，使下一次播放重新读取本地文件。
        /// </summary>
        public void ClearCache()
        {
            audioModule.ClearCache();
        }

        /// <inheritdoc/>
        protected override void Awake()
        {
            base.Awake();

            audioModule = RFrameworkModuleEntry.GetModule<IAudioModule>();
            if (audioModule == null)
            {
                Log.Error("Can not find module '{0}'.", nameof(IAudioModule));
                return;
            }

            IResourceModule resourceModule = RFrameworkModuleEntry.GetModule<IResourceModule>();
            IEventModule eventModule = RFrameworkModuleEntry.GetModule<IEventModule>();
            IPoolModule poolModule = RFrameworkModuleEntry.GetModule<IPoolModule>();
            audioModule.SetDependencies(resourceModule, eventModule, poolModule);

            AudioHelperBase audioHelper = Helper.CreateHelper<AudioHelperBase>(audioHelperTypeName, null);
            if (audioHelper != null)
            {
                audioModule.SetHelper(audioHelper);
                audioHelper.transform.SetParent(transform);
            }
        }
    }
}
