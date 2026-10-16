using System;
using System.Reflection;
using NUnit.Framework;
using RFramework;
using UnityEditor;
using UnityEngine;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    public sealed class AdvertisementModuleTests
    {
        [SetUp]
        public void SetUp()
        {
            RFrameworkModuleHost.StopAll();
        }

        [TearDown]
        public void TearDown()
        {
            RFrameworkModuleHost.StopAll();
        }

        [Test]
        public void AdvertisementModule_RegistersAndReturnsHelperResult()
        {
            IAdvertisementModule module = RFrameworkModuleHost.Get<IAdvertisementModule>();
            var helper = new FakeAdvertisementHelper();
            module.SetHelper(helper);
            module.Initialize();

            AdvertisementResult result = AdvertisementResult.Failed;
            bool accepted = module.Show(
                AdvertisementType.Rewarded, "rewarded", value => result = value);

            Assert.That(accepted, Is.True);
            Assert.That(helper.IsInitialized, Is.True);
            Assert.That(result, Is.EqualTo(AdvertisementResult.Rewarded));
        }

        [Test]
        public void AdvertisementModule_RejectsEmptyPlacement()
        {
            IAdvertisementModule module = RFrameworkModuleHost.Get<IAdvertisementModule>();
            module.SetHelper(new FakeAdvertisementHelper());

            Assert.Throws<RFrameworkException>(() =>
                module.IsReady(AdvertisementType.Banner, string.Empty));
        }

        [Test]
        public void FrameworkPrefabLoadsWithAdvertisementAndNoMissingScripts()
        {
            const string path = "Assets/UnityRFramework/Prefabs/UnityRFramework.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Assert.That(root, Is.Not.Null);
                Transform advertisement = root.transform.Find("Advertisement");
                Assert.That(advertisement, Is.Not.Null, "框架入口缺少 Advertisement 直接子物体。");
                Assert.That(advertisement.parent, Is.EqualTo(root.transform));
                Assert.That(advertisement.GetComponent<AdvertisementComponent>(), Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<AdvertisementComponent>(true).Length,
                    Is.EqualTo(1));
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                {
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject),
                        Is.Zero, "预制体存在丢失的脚本：" + node.name);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void GameEntryAdvertisementIsClearedByRuntimeReset()
        {
            MethodInfo register = typeof(UnityRFrameworkRuntime).GetMethod(
                "Register", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo reset = typeof(UnityRFrameworkRuntime).GetMethod(
                "ResetComponents", BindingFlags.Static | BindingFlags.NonPublic);
            GameObject root = PrefabUtility.LoadPrefabContents(
                "Assets/UnityRFramework/Prefabs/UnityRFramework.prefab");
            try
            {
                reset.Invoke(null, null);
                Assert.That(GameEntry.Advertisement, Is.Null);
                AdvertisementComponent component = root.GetComponentInChildren<AdvertisementComponent>(true);
                register.Invoke(null, new object[] { component });
                Assert.That(GameEntry.Advertisement, Is.SameAs(component));
                Assert.That(GameEntry.Advertisement, Is.SameAs(GameEntry.Get<AdvertisementComponent>()));

                reset.Invoke(null, null);
                Assert.That(component == null, Is.False, "重置注册表时旧组件仍然存活。");
                Assert.That(GameEntry.Advertisement, Is.Null, "重置后不能返回旧组件缓存。");
            }
            finally
            {
                reset.Invoke(null, null);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private sealed class FakeAdvertisementHelper : IAdvertisementHelper
        {
            public string HelperName => "Fake";
            public bool IsSupported => true;
            public bool IsInitialized { get; private set; }

            public void Initialize() => IsInitialized = true;
            public void Load(AdvertisementType type, string placementId) { }
            public bool IsReady(AdvertisementType type, string placementId) => IsInitialized;

            public bool Show(AdvertisementType type, string placementId,
                Action<AdvertisementResult> callback)
            {
                callback?.Invoke(AdvertisementResult.Rewarded);
                return true;
            }

            public void Hide(AdvertisementType type, string placementId) { }
        }
    }
}
