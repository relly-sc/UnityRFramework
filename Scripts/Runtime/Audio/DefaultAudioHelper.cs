using System;
using System.Collections;
using System.Collections.Generic;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认音频辅助器。自建 BGM/SFX 池/UI 三组 AudioSource，
    /// 实现淡入淡出和完成回调协程。通过 Resource 模块同步加载 AudioClip。
    /// </summary>
    public class DefaultAudioHelper : AudioHelperBase
    {
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
        /// BGM 完成回调协程引用。
        /// </summary>
        private Coroutine bgmCompleteCoroutine;

        /// <summary>
        /// 句柄 ID → SFX 播放信息（AudioSource + 完成回调协程）映射。
        /// </summary>
        private readonly Dictionary<int, SfxPlayInfo> sfxPlayInfos = new Dictionary<int, SfxPlayInfo>();

        /// <summary>
        /// SFX AudioSource 池最大数量。
        /// </summary>
        private const int SfxPoolMaxSize = 16;

        /// <summary>
        /// SFX 播放信息（AudioSource + 完成回调协程）。
        /// </summary>
        private struct SfxPlayInfo
        {
            /// <summary>
            /// 当前播放的 AudioSource。
            /// </summary>
            public AudioSource Source;

            /// <summary>
            /// 完成回调协程引用（用于 Stop 时取消）。
            /// </summary>
            public Coroutine CompleteCoroutine;
        }

        /// <summary>
        /// 创建 BGM / SFX 池 / UI 三组 AudioSource 子物体。
        /// </summary>
        private void Awake()
        {
            CreateAudioSources();
        }

        /// <summary>
        /// 创建 AudioSource 子物体和组件。
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

        // ====== BGM ======

        /// <inheritdoc cref="IAudioHelper.PlayBgm"/>
        public override void PlayBgm(object audioAsset, float volume, bool loop,
            float fadeInSeconds, float completeDelaySeconds, Action onComplete)
        {
            AudioClip clip = audioAsset as AudioClip;
            if (clip == null)
            {
                return;
            }

            StopBgmCoroutines();

            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.volume = 0f;
            bgmSource.Play();

            StartBgmFade(volume, fadeInSeconds);

            if (onComplete != null)
            {
                bgmCompleteCoroutine = StartCoroutine(BgmCompleteRoutine(completeDelaySeconds, onComplete));
            }
        }

        /// <inheritdoc cref="IAudioHelper.StopBgm"/>
        public override void StopBgm(float fadeOutSeconds)
        {
            StopBgmCoroutines();

            if (fadeOutSeconds > 0f && bgmSource.isPlaying)
            {
                bgmFadeCoroutine = StartCoroutine(FadeOutAndStopRoutine(fadeOutSeconds));
            }
            else
            {
                bgmSource.Stop();
                bgmSource.clip = null;
            }
        }

        /// <inheritdoc cref="IAudioHelper.PauseBgm"/>
        public override void PauseBgm()
        {
            bgmSource.Pause();
        }

        /// <inheritdoc cref="IAudioHelper.ResumeBgm"/>
        public override void ResumeBgm()
        {
            bgmSource.UnPause();
        }

        /// <inheritdoc cref="IAudioHelper.SetBgmVolume"/>
        public override void SetBgmVolume(float volume)
        {
            bgmSource.volume = volume;
        }

        // ====== SFX ======

        /// <inheritdoc cref="IAudioHelper.PlaySfx"/>
        public override void PlaySfx(int handleId, object audioAsset, float volume,
            float completeDelaySeconds, Action onComplete)
        {
            AudioClip clip = audioAsset as AudioClip;
            if (clip == null)
            {
                return;
            }

            AudioSource sfx = GetIdleSfxSource();
            if (sfx == null)
            {
                return;
            }

            sfx.clip = clip;
            sfx.volume = volume;
            sfx.loop = false;
            sfx.Play();

            Coroutine completeCoroutine = null;
            if (onComplete != null)
            {
                completeCoroutine = StartCoroutine(
                    SfxCompleteRoutine(handleId, sfx, completeDelaySeconds, onComplete));
            }

            sfxPlayInfos[handleId] = new SfxPlayInfo
            {
                Source = sfx,
                CompleteCoroutine = completeCoroutine
            };
        }

        /// <inheritdoc cref="IAudioHelper.StopSfx"/>
        public override void StopSfx(int handleId)
        {
            if (sfxPlayInfos.TryGetValue(handleId, out SfxPlayInfo info))
            {
                if (info.CompleteCoroutine != null)
                {
                    StopCoroutine(info.CompleteCoroutine);
                }

                info.Source.Stop();
                sfxPlayInfos.Remove(handleId);
            }
        }

        /// <inheritdoc cref="IAudioHelper.StopAllSfx"/>
        public override void StopAllSfx()
        {
            foreach (KeyValuePair<int, SfxPlayInfo> kv in sfxPlayInfos)
            {
                if (kv.Value.CompleteCoroutine != null)
                {
                    StopCoroutine(kv.Value.CompleteCoroutine);
                }

                kv.Value.Source.Stop();
            }

            sfxPlayInfos.Clear();
        }

        // ====== UI ======

        /// <inheritdoc cref="IAudioHelper.PlayUI"/>
        public override void PlayUI(object audioAsset, float volume)
        {
            AudioClip clip = audioAsset as AudioClip;
            if (clip == null)
            {
                return;
            }

            uiSource.clip = clip;
            uiSource.volume = volume;
            uiSource.loop = false;
            uiSource.Play();
        }

        // ====== 内部方法 ======

        /// <summary>
        /// 取消 BGM 的淡入淡出和完成回调协程。
        /// </summary>
        private void StopBgmCoroutines()
        {
            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
                bgmFadeCoroutine = null;
            }

            if (bgmCompleteCoroutine != null)
            {
                StopCoroutine(bgmCompleteCoroutine);
                bgmCompleteCoroutine = null;
            }
        }

        /// <summary>
        /// 启动 BGM 淡入协程。
        /// </summary>
        /// <param name="target">目标音量。</param>
        /// <param name="duration">过渡时长。</param>
        private void StartBgmFade(float target, float duration)
        {
            if (duration <= 0f)
            {
                bgmSource.volume = target;
                return;
            }

            bgmFadeCoroutine = StartCoroutine(FadeRoutine(target, duration));
        }

        /// <summary>
        /// BGM 通用淡入淡出协程：从当前音量平滑过渡到目标音量。
        /// </summary>
        /// <param name="target">目标音量。</param>
        /// <param name="duration">过渡时长。</param>
        private IEnumerator FadeRoutine(float target, float duration)
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
            bgmFadeCoroutine = null;
        }

        /// <summary>
        /// BGM 淡出后停止协程：淡出 → 停止 AudioSource → 清空 clip。
        /// </summary>
        /// <param name="fadeOut">淡出时长。</param>
        private IEnumerator FadeOutAndStopRoutine(float fadeOut)
        {
            float start = bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < fadeOut)
            {
                elapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(start, 0f, elapsed / fadeOut);
                yield return null;
            }

            bgmSource.volume = 0f;
            bgmSource.Stop();
            bgmSource.clip = null;
            bgmFadeCoroutine = null;
        }

        /// <summary>
        /// BGM 完成回调协程：等播完 → 延迟 → 执行回调。
        /// </summary>
        /// <param name="delaySeconds">延迟秒数。</param>
        /// <param name="callback">回调委托。</param>
        private IEnumerator BgmCompleteRoutine(float delaySeconds, Action callback)
        {
            while (bgmSource.isPlaying)
            {
                yield return null;
            }

            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            callback?.Invoke();
            bgmCompleteCoroutine = null;
        }

        /// <summary>
        /// SFX 完成回调协程：等播完 → 延迟 → 执行回调 → 清理播放信息。
        /// </summary>
        /// <param name="handleId">句柄 ID（用于清理映射）。</param>
        /// <param name="source">对应的 AudioSource。</param>
        /// <param name="delaySeconds">延迟秒数。</param>
        /// <param name="callback">回调委托。</param>
        private IEnumerator SfxCompleteRoutine(int handleId, AudioSource source,
            float delaySeconds, Action callback)
        {
            while (source.isPlaying)
            {
                yield return null;
            }

            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            callback?.Invoke();
            sfxPlayInfos.Remove(handleId);
        }

        /// <summary>
        /// 从 SFX 池中获取空闲 AudioSource。全部忙时返回第一个（覆盖最早的）。
        /// </summary>
        /// <returns>AudioSource 实例。</returns>
        private AudioSource GetIdleSfxSource()
        {
            for (int i = 0; i < sfxPool.Count; i++)
            {
                if (!sfxPool[i].isPlaying)
                {
                    return sfxPool[i];
                }
            }

            return sfxPool[0];
        }
    }
}
