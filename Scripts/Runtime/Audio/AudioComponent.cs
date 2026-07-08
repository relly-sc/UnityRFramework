using RFramework;
using RFramework.Audio;
using RFramework.Event;
using RFramework.Pool;
using RFramework.Resource;
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
