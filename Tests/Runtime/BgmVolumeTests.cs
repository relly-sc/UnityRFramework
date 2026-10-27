using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RFramework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityRFramework.Runtime;
using Object = UnityEngine.Object;

namespace UnityRFramework.Tests
{
    /// <summary>使用真实 AudioSource 验证音乐播放中的音量应用，不依赖音频硬件测量。</summary>
    public sealed class BgmVolumeTests
    {
        private GameObject owner;
        private GameObject componentOwner;
        private AudioClip clip;
        private AudioComponent component;
        private IAudioModule module;
        private AudioSource source;
        private float previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            RFrameworkModuleHost.StopAll();
            // 无声测试片段，真实进入播放状态但不向用户输出测试声音。
            clip = AudioClip.Create("BgmVolumeTest", 44100, 1, 44100, false);
            var resource = RFrameworkModuleHost.Get<IResourceModule>();
            resource.SetHelper(new ClipResourceHelper(clip));
            resource.InitializeAsync().GetAwaiter().GetResult();
            owner = new GameObject("BgmVolumeTest");
            var helper = owner.AddComponent<DefaultAudioHelper>();
            module = RFrameworkModuleHost.Get<IAudioModule>();
            module.SetDependencies(resource, RFrameworkModuleHost.Get<IEventModule>(), RFrameworkModuleHost.Get<IPoolModule>());
            module.SetHelper(helper);
            // 保留组件真实转发入口，不让测试实例进入全局组件注册表。
            componentOwner = new GameObject("BgmVolumeTestComponent");
            componentOwner.SetActive(false);
            component = componentOwner.AddComponent<AudioComponent>();
            typeof(AudioComponent).GetField("audioModule", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(component, module);
            source = helper.transform.Find("BGM").GetComponent<AudioSource>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RFrameworkModuleHost.StopAll();
            Object.Destroy(owner);
            Object.Destroy(componentOwner);
            Object.Destroy(clip);
            Time.timeScale = previousTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayingMusicImmediatelyFollowsVolumeAndPlaybackMultiplier()
        {
            component.PlayBgm("test-bgm", volume: 0.5f);
            yield return null;
            Assert.IsTrue(source.isPlaying);
            AssertVolume(0.5f);

            component.BgmVolume = 0f;
            AssertVolume(0f);
            yield return null;
            AssertVolume(0f);
            component.BgmVolume = 0.6f;
            AssertVolume(0.3f);
            Assert.IsTrue(source.isPlaying);
            Assert.AreSame(clip, source.clip);
        }

        [UnityTest]
        public IEnumerator AsyncPlaybackUsesTheSameLiveVolumePath()
        {
            Task<AudioHandle> playing = component.PlayBgmAsync("async-bgm", volume: 0.5f);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!playing.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(playing.IsCompleted, "测试资源加载超时。");
            playing.GetAwaiter().GetResult();
            Assert.IsTrue(source.isPlaying);
            component.BgmVolume = 0f;
            AssertVolume(0f);
            component.BgmVolume = 0.8f;
            AssertVolume(0.4f);
        }

        [Test]
        public void MutedVolumeChangesStaySilentAndRestoreTheLatestValue()
        {
            component.PlayBgm("muted-bgm", volume: 0.5f);
            component.Muted = true;
            component.BgmVolume = 0f;
            AssertVolume(0f);
            component.BgmVolume = 0.4f;
            AssertVolume(0f);
            component.Muted = false;
            AssertVolume(0.2f);
        }

        [Test]
        public void PausedVolumeChangesApplyBeforeResume()
        {
            component.PlayBgm("paused-bgm", volume: 0.5f);
            module.PauseBgm();
            component.BgmVolume = 0.4f;
            AssertVolume(0.2f);
            module.ResumeBgm();
            AssertVolume(0.2f);
        }

        [UnityTest]
        public IEnumerator FadeInCannotOverwriteZeroOrMutedVolume()
        {
            component.PlayBgm("fade-in", volume: 0.5f, fadeInSeconds: 0.3f);
            yield return null;
            component.BgmVolume = 0f;
            AssertVolume(0f);
            yield return null;
            yield return null;
            AssertVolume(0f);
            component.Muted = true;
            component.BgmVolume = 0.8f;
            yield return new WaitForSecondsRealtime(0.4f);
            AssertVolume(0f);
            component.Muted = false;
            AssertVolume(0.4f);
            Assert.IsTrue(source.isPlaying);
        }

        [UnityTest]
        public IEnumerator FadeOutRespectsLiveVolumeAndStillStopsWithoutCompletionCallback()
        {
            int completions = 0;
            component.PlayBgm("fade-out", onComplete: () => completions++);
            module.StopBgm(0.2f);
            component.BgmVolume = 0f;
            AssertVolume(0f);
            yield return null;
            AssertVolume(0f);
            component.BgmVolume = 0.5f;
            Assert.That(source.volume, Is.InRange(0f, 0.5f));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsFalse(source.isPlaying);
            Assert.IsNull(source.clip);
            Assert.AreEqual(0, completions);
        }

        [UnityTest]
        public IEnumerator VolumeChangesDoNotCancelNaturalCompletionCallback()
        {
            int completions = 0;
            component.PlayBgm("complete", loop: false, onComplete: () => completions++);
            component.BgmVolume = 0f;
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.AreEqual(1, completions);
        }

        [TestCase(-1f, 0f)]
        [TestCase(2f, 1f)]
        [TestCase(float.NegativeInfinity, 0f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void OutOfRangeVolumeIsClamped(float value, float expected)
        {
            component.PlayBgm("bounds", volume: 0.5f);
            component.BgmVolume = value;
            Assert.AreEqual(expected, component.BgmVolume);
            AssertVolume(expected * 0.5f);
        }

        [Test]
        public void NaNIsRejectedWithoutChangingTheActiveVolume()
        {
            component.PlayBgm("nan", volume: 0.5f);
            component.BgmVolume = 0.4f;
            Assert.Throws<RFrameworkException>(() => component.BgmVolume = float.NaN);
            Assert.AreEqual(0.4f, component.BgmVolume);
            AssertVolume(0.2f);
        }

        [Test]
        public void VolumeCanBeSetBeforeAHelperOrPlaybackExists()
        {
            var helper = owner.GetComponentInChildren<DefaultAudioHelper>();
            module.SetHelper(null);
            component.BgmVolume = 0.4f;
            component.Muted = true;
            module.SetHelper(helper);
            component.PlayBgm("initial", volume: 0.5f);
            AssertVolume(0f);
            component.Muted = false;
            AssertVolume(0.2f);
        }

        private void AssertVolume(float expected)
        {
            Assert.That(source.volume, Is.EqualTo(expected).Within(0.0001f));
        }

        private sealed class ClipResourceHelper : IResourceHelper
        {
            private readonly AudioClip clip;
            public ClipResourceHelper(AudioClip clip) { this.clip = clip; }
            public Task InitializeAsync(string packageName, ResourcePlayMode playMode,
                string defaultHostServer, string fallbackHostServer) => Task.CompletedTask;
            public void Destroy() { }
            public object LoadAssetSync(string location, Type assetType) => clip;
            public Task<object> LoadAssetAsync(string location, Type assetType, uint priority,
                CancellationToken ct = default, IProgress<float> onProgress = null)
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult<object>(clip);
            }
            public void ReleaseAsset(string location, Type assetType) { }
            public Task LoadSceneAsync(string location, int sceneMode, bool activateOnLoad, uint priority,
                IProgress<float> onProgress = null) => throw new NotSupportedException();
            public Task UnloadSceneAsync(string location) => throw new NotSupportedException();
            public bool IsLocationValid(string location) => true;
            public long GetDownloadSize(string location) => 0;
        }
    }
}
