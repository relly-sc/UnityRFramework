using System.Collections;
using System.Collections.Generic;
using RFramework;
using RFramework.Audio;
using RFramework.Event;
using RFramework.Pool;
using RFramework.Resource;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 音频组件。管理 BGM/SFX/UI AudioSource，
    /// 绑定 AudioModule 的所有 Runtime 层钩子，处理淡入淡出和回调协程。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Audio")]
    [DisallowMultipleComponent]
    public sealed class AudioComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 音频模块核心引用。
        /// </summary>
        private AudioModule audioModule;

        /// <summary>
        /// BGM 专属 AudioSource（单实例）。
        /// </summary>
        private AudioSource bgmSource;

        /// <summary>
        /// SFX AudioSource 池（并发播放，最多 16 个）。
        /// </summary>
        private readonly List<AudioSource> sfxPool = new List<AudioSource>();

        /// <summary>
        /// UI 音效专属 AudioSource（单实例）。
        /// </summary>
        private AudioSource uiSource;

        /// <summary>
        /// BGM 淡入淡出协程引用。
        /// </summary>
        private Coroutine bgmFadeCoroutine;

        /// <summary>
        /// 句柄 ID → SFX 播放信息（AudioSource + 句柄 ID）映射。
        /// </summary>
        private readonly Dictionary<int, SfxPlayInfo> sfxPlayInfos = new Dictionary<int, SfxPlayInfo>();

        /// <summary>
        /// 句柄 ID → 回调协程映射。
        /// </summary>
        private readonly Dictionary<int, Coroutine> callbackCoroutines = new Dictionary<int, Coroutine>();

        /// <summary>
        /// SFX AudioSource 池最大数量。
        /// </summary>
        private const int SfxPoolMaxSize = 16;

        /// <summary>
        /// 音频辅助器类型名（Inspector 配置）。
        /// </summary>
        [SerializeField] private string audioHelperTypeName = "UnityRFramework.Runtime.DefaultAudioHelper";

        /// <summary>
        /// SFX 播放信息（AudioSource + 句柄 ID）。
        /// </summary>
        private struct SfxPlayInfo
        {
            /// <summary>
            /// 当前播放的 AudioSource。
            /// </summary>
            public AudioSource Source;

            /// <summary>
            /// 对应的句柄 ID。
            /// </summary>
            public int HandleId;
        }

        /// <summary>
        /// BGM 音量（0~1）。
        /// </summary>
        public float BgmVolume
        {
            get { return audioModule != null ? audioModule.BgmVolume : 1f; }
            set { if (audioModule != null) audioModule.BgmVolume = value; }
        }

        /// <summary>
        /// SFX 音量（0~1）。
        /// </summary>
        public float SfxVolume
        {
            get { return audioModule != null ? audioModule.SfxVolume : 1f; }
            set { if (audioModule != null) audioModule.SfxVolume = value; }
        }

        /// <summary>
        /// UI 音效音量（0~1）。
        /// </summary>
        public float UIVolume
        {
            get { return audioModule != null ? audioModule.UIVolume : 1f; }
            set { if (audioModule != null) audioModule.UIVolume = value; }
        }

        /// <summary>
        /// 全局静音开关。
        /// </summary>
        public bool Muted
        {
            get { return audioModule != null && audioModule.Muted; }
            set { if (audioModule != null) audioModule.Muted = value; }
        }

        /// <inheritdoc/>
        protected override void Awake()
        {
            base.Awake();

            audioModule = (AudioModule)RFrameworkModuleEntry.GetModule<IAudioModule>();
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

            CreateAudioSources();
            BindHooks();
        }

        /// <summary>
        /// 创建 BGM / SFX 池 / UI 三组 AudioSource 子物体。
        /// </summary>
        private void CreateAudioSources()
        {
            GameObject bgmGo = new GameObject("BGM");
            bgmGo.transform.SetParent(transform);
            bgmSource = bgmGo.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;

            for (int i = 0; i < SfxPoolMaxSize; i++)
            {
                GameObject sfxGo = new GameObject($"SFX_{i}");
                sfxGo.transform.SetParent(transform);
                AudioSource sfx = sfxGo.AddComponent<AudioSource>();
                sfx.playOnAwake = false;
                sfxPool.Add(sfx);
            }

            GameObject uiGo = new GameObject("UI");
            uiGo.transform.SetParent(transform);
            uiSource = uiGo.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
        }

        /// <summary>
        /// 绑定 AudioModule 的所有 Runtime 层钩子。
        /// </summary>
        private void BindHooks()
        {
            audioModule.OnPlayBgm = HandlePlayBgm;
            audioModule.OnStopBgm = HandleStopBgm;
            audioModule.OnPauseNative = () => bgmSource.Pause();
            audioModule.OnResumeNative = () => bgmSource.UnPause();
            audioModule.OnPlaySfx = HandlePlaySfx;
            audioModule.OnStopAllSfx = HandleStopAllSfx;
            audioModule.OnPlayUI = HandlePlayUI;
            audioModule.OnSetBgmVolume = (vol) => bgmSource.volume = vol;
            audioModule.OnStartCallback = HandleStartCallback;
            audioModule.OnCancelCallback = HandleCancelCallback;
            audioModule.OnStopSfxById = HandleStopSfxById;
        }

        // ====== BGM ======

        /// <summary>
        /// BGM 播放处理：设置 AudioClip → 从 0 音量开始 → 淡入到目标音量。
        /// </summary>
        /// <param name="audioAsset">AudioClip 对象。</param>
        /// <param name="volume">目标音量。</param>
        /// <param name="loop">是否循环。</param>
        /// <param name="fadeIn">淡入时长（秒）。</param>
        private void HandlePlayBgm(object audioAsset, float volume, bool loop, float fadeIn)
        {
            AudioClip clip = audioAsset as AudioClip;
            if (clip == null) return;

            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.volume = 0f;
            bgmSource.Play();
            StartBgmFade(volume, fadeIn);
        }

        /// <summary>
        /// BGM 停止处理：支持淡出后停止或立即停止。
        /// </summary>
        /// <param name="fadeOut">淡出时长（秒），0 为立即停止。</param>
        private void HandleStopBgm(float fadeOut)
        {
            if (fadeOut > 0f && bgmSource.isPlaying)
            {
                StartBgmFade(0f, fadeOut, stopAfterFade: true);
            }
            else
            {
                bgmSource.Stop();
                bgmSource.clip = null;
            }
        }

        /// <summary>
        /// 启动 BGM 淡入/淡出协程。
        /// </summary>
        /// <param name="target">目标音量。</param>
        /// <param name="duration">过渡时长。</param>
        /// <param name="stopAfterFade">淡出完毕后是否停止 AudioSource。</param>
        private void StartBgmFade(float target, float duration, bool stopAfterFade = false)
        {
            if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = StartCoroutine(FadeRoutine(target, duration, stopAfterFade));
        }

        /// <summary>
        /// BGM 淡入/淡出协程：从当前音量平滑过渡到目标音量，完成后可选停止。
        /// </summary>
        /// <param name="target">目标音量。</param>
        /// <param name="duration">过渡时长。</param>
        /// <param name="stopAfterFade">完毕后是否停止。</param>
        private IEnumerator FadeRoutine(float target, float duration, bool stopAfterFade)
        {
            float start = bgmSource.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }
            bgmSource.volume = target;
            if (stopAfterFade)
            {
                bgmSource.Stop();
                bgmSource.clip = null;
            }
            bgmFadeCoroutine = null;
        }

        // ====== SFX ======

        /// <summary>
        /// SFX 播放处理：从池中获取空闲 AudioSource 并播放。
        /// </summary>
        /// <param name="handleId">句柄 ID。</param>
        /// <param name="audioAsset">AudioClip 对象。</param>
        /// <param name="volume">最终音量。</param>
        private void HandlePlaySfx(int handleId, object audioAsset, float volume)
        {
            AudioClip clip = audioAsset as AudioClip;
            if (clip == null) return;

            AudioSource sfx = GetIdleSfxSource();
            if (sfx == null) return;

            sfx.clip = clip;
            sfx.volume = volume;
            sfx.loop = false;
            sfx.Play();

            // 记录 handleId → AudioSource 映射，用于后续单独停止
            sfxPlayInfos[handleId] = new SfxPlayInfo { Source = sfx, HandleId = handleId };
        }

        /// <summary>
        /// 从 SFX 池中获取一个空闲的 AudioSource。全部忙时返回第一个（覆盖最早的）。
        /// </summary>
        /// <returns>AudioSource 实例。</returns>
        private AudioSource GetIdleSfxSource()
        {
            for (int i = 0; i < sfxPool.Count; i++)
            {
                if (!sfxPool[i].isPlaying)
                    return sfxPool[i];
            }
            return sfxPool[0];
        }

        /// <summary>
        /// 停止所有 SFX 并清空播放信息映射。
        /// </summary>
        private void HandleStopAllSfx()
        {
            foreach (KeyValuePair<int, SfxPlayInfo> kv in sfxPlayInfos)
            {
                kv.Value.Source.Stop();
            }
            sfxPlayInfos.Clear();
        }

        /// <summary>
        /// 按句柄 ID 停止指定 SFX。
        /// </summary>
        /// <param name="handleId">句柄 ID。</param>
        private void HandleStopSfxById(int handleId)
        {
            if (sfxPlayInfos.TryGetValue(handleId, out SfxPlayInfo info))
            {
                info.Source.Stop();
                sfxPlayInfos.Remove(handleId);
            }
        }

        // ====== UI ======

        /// <summary>
        /// UI 音效播放处理：直接在 uiSource 上播放。
        /// </summary>
        /// <param name="audioAsset">AudioClip 对象。</param>
        /// <param name="volume">最终音量。</param>
        private void HandlePlayUI(object audioAsset, float volume)
        {
            AudioClip clip = audioAsset as AudioClip;
            if (clip == null) return;
            uiSource.clip = clip;
            uiSource.volume = volume;
            uiSource.loop = false;
            uiSource.Play();
        }

        // ====== 回调管理 ======

        /// <summary>
        /// 启动回调协程：找到对应 AudioSource → 等播完 → 延迟 → 执行回调 → 清理。
        /// </summary>
        /// <param name="handleId">句柄 ID。</param>
        /// <param name="delaySeconds">播放完毕后延迟秒数。</param>
        /// <param name="callback">延迟后执行的回调。</param>
        private void HandleStartCallback(int handleId, float delaySeconds, System.Action callback)
        {
            // 找到对应的 AudioSource
            AudioSource source = null;
            if (handleId == audioModule.BgmHandleId)
            {
                source = bgmSource;
            }
            else if (sfxPlayInfos.TryGetValue(handleId, out SfxPlayInfo info))
            {
                source = info.Source;
            }

            if (source == null) return;

            Coroutine coroutine = StartCoroutine(CallbackRoutine(handleId, source, delaySeconds, callback));
            callbackCoroutines[handleId] = coroutine;
        }

        /// <summary>
        /// 取消回调协程。
        /// </summary>
        /// <param name="handleId">句柄 ID。</param>
        private void HandleCancelCallback(int handleId)
        {
            if (callbackCoroutines.TryGetValue(handleId, out Coroutine coroutine))
            {
                StopCoroutine(coroutine);
                callbackCoroutines.Remove(handleId);
            }
        }

        /// <summary>
        /// 回调协程：等待音频播放完毕 → 延迟 N 秒 → 执行回调 → 清理记录。
        /// </summary>
        /// <param name="handleId">句柄 ID（用于清理）。</param>
        /// <param name="source">对应的 AudioSource。</param>
        /// <param name="delaySeconds">延迟秒数。</param>
        /// <param name="callback">回调委托。</param>
        private IEnumerator CallbackRoutine(int handleId, AudioSource source, float delaySeconds, System.Action callback)
        {
            // 等待音频自然播完
            while (source.isPlaying)
            {
                yield return null;
            }

            // 延迟 N 秒
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            // 执行回调
            callback?.Invoke();

            // 清理
            callbackCoroutines.Remove(handleId);
        }
    }
}
