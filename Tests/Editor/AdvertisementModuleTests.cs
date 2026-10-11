using System;
using NUnit.Framework;
using RFramework;

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
