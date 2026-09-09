using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RFramework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// UI 模块窗口生命周期、全屏遮挡和资源所有权的核心契约测试。
    /// </summary>
    public sealed class UIModuleTests
    {
        private IUIModule uiModule;
        private IResourceModule resourceModule;
        private RecordingResourceHelper resourceHelper;
        private RecordingUIHelper uiHelper;

        /// <summary>为每个测试创建隔离的 UI、资源和事件模块。</summary>
        [SetUp]
        public void SetUp()
        {
            RFrameworkModuleHost.StopAll();

            resourceHelper = new RecordingResourceHelper();
            resourceModule = RFrameworkModuleHost.Get<IResourceModule>();
            resourceModule.SetHelper(resourceHelper);
            resourceModule.InitializeAsync().GetAwaiter().GetResult();

            uiHelper = new RecordingUIHelper();
            uiModule = RFrameworkModuleHost.Get<IUIModule>();
            uiModule.SetDependencies(
                resourceModule,
                RFrameworkModuleHost.Get<IEventModule>());
            uiModule.SetHelper(uiHelper);
        }

        /// <summary>停止全部模块，避免测试间共享窗口和资源状态。</summary>
        [TearDown]
        public void TearDown()
        {
            RFrameworkModuleHost.StopAll();
        }

        /// <summary>验证正常开关生命周期、重复打开保护和资源精确归还。</summary>
        [UnityTest]
        public IEnumerator OpenAndCloseOwnsOneAssetReference()
        {
            Task<IUIForm> opening = uiModule.OpenUIFormAsync("Inventory");
            yield return WaitForTask(opening);
            AssertTaskSucceeded(opening);
            FakeUIForm form = (FakeUIForm)opening.Result;

            Assert.AreEqual(1, form.InitCount);
            Assert.AreEqual(1, form.OpenCount);
            Assert.AreEqual(1, uiModule.UIFormCount);
            Task<IUIForm> duplicate = uiModule.OpenUIFormAsync("Inventory");
            yield return WaitForTask(duplicate);
            Assert.That(GetTaskException(duplicate), Is.TypeOf<RFrameworkException>());

            uiModule.CloseUIForm("Inventory");
            resourceModule.UnloadUnusedAssets();

            Assert.AreEqual(1, form.CloseCount);
            Assert.AreEqual(1, uiHelper.ReleaseCount);
            CollectionAssert.AreEqual(new[] { "Inventory" }, resourceHelper.ReleasedLocations);
            Assert.AreEqual(0, uiModule.UIFormCount);
        }

        /// <summary>验证全屏窗口只暂停下层窗口，并在关闭后恢复。</summary>
        [UnityTest]
        public IEnumerator FullScreenFormPausesAndResumesLowerForm()
        {
            Task<IUIForm> lowerOpening = uiModule.OpenUIFormAsync(
                "Lower", windowLayer: 100);
            yield return WaitForTask(lowerOpening);
            AssertTaskSucceeded(lowerOpening);
            FakeUIForm lower = (FakeUIForm)lowerOpening.Result;

            Task<IUIForm> fullScreenOpening = uiModule.OpenUIFormAsync(
                "FullScreen", windowLayer: 200, fullScreen: true);
            yield return WaitForTask(fullScreenOpening);
            AssertTaskSucceeded(fullScreenOpening);
            FakeUIForm fullScreen = (FakeUIForm)fullScreenOpening.Result;

            Assert.AreEqual(1, lower.PauseCount);
            Assert.AreEqual(0, fullScreen.PauseCount);

            Task<IUIForm> overlayOpening = uiModule.OpenUIFormAsync(
                "Overlay", windowLayer: 300, fullScreen: true);
            yield return WaitForTask(overlayOpening);
            AssertTaskSucceeded(overlayOpening);
            Assert.AreEqual(1, fullScreen.PauseCount);
            Assert.IsTrue(uiModule.CloseTopUIForm());
            Assert.AreEqual(1, fullScreen.ResumeCount);
            Assert.AreEqual(0, lower.ResumeCount);

            uiModule.CloseUIForm("FullScreen");

            Assert.AreEqual(1, lower.ResumeCount);
        }

        /// <summary>验证加载期间关闭会放弃打开并归还已经完成加载的资源。</summary>
        [UnityTest]
        public IEnumerator CloseWhileLoadingCancelsOpeningAndReleasesAsset()
        {
            resourceHelper.DelayLoads = true;
            Task<IUIForm> opening = uiModule.OpenUIFormAsync("Delayed");

            Assert.IsTrue(uiModule.IsLoadingUIForm("Delayed"));
            uiModule.CloseUIForm("Delayed");
            resourceHelper.CompleteDelayedLoad();

            yield return WaitForTask(opening);
            Assert.IsTrue(
                opening.IsCanceled || GetTaskException(opening) is OperationCanceledException,
                "关闭加载中的窗口后，打开任务应进入取消状态。");
            resourceModule.UnloadUnusedAssets();

            CollectionAssert.AreEqual(new[] { "Delayed" }, resourceHelper.ReleasedLocations);
            Assert.AreEqual(0, uiModule.UIFormCount);
            Assert.AreEqual(0, uiHelper.InstantiateCount);
        }

        /// <summary>验证创建窗口失败时释放实例和资源，不留下已打开状态。</summary>
        [UnityTest]
        public IEnumerator CreateFormFailureCleansInstanceAndAsset()
        {
            uiHelper.FailCreate = true;

            Task<IUIForm> opening = uiModule.OpenUIFormAsync("Broken");
            yield return WaitForTask(opening);
            Assert.That(GetTaskException(opening), Is.TypeOf<RFrameworkException>());
            resourceModule.UnloadUnusedAssets();

            Assert.AreEqual(1, uiHelper.ReleaseCount);
            CollectionAssert.AreEqual(new[] { "Broken" }, resourceHelper.ReleasedLocations);
            Assert.AreEqual(0, uiModule.UIFormCount);
        }

        /// <summary>验证关闭回调异常不会阻断窗口、实例和资源清理。</summary>
        [UnityTest]
        public IEnumerator CloseFailureStillCleansOwnedForm()
        {
            Task<IUIForm> opening = uiModule.OpenUIFormAsync("FaultyClose");
            yield return WaitForTask(opening);
            AssertTaskSucceeded(opening);
            FakeUIForm form = (FakeUIForm)opening.Result;
            form.ThrowOnClose = true;

            Assert.Throws<InvalidOperationException>(() =>
                uiModule.CloseUIForm("FaultyClose"));
            resourceModule.UnloadUnusedAssets();

            Assert.AreEqual(0, uiModule.UIFormCount);
            Assert.AreEqual(1, uiHelper.ReleaseCount);
            CollectionAssert.AreEqual(new[] { "FaultyClose" }, resourceHelper.ReleasedLocations);
        }

        /// <summary>验证栈顶按层级和同层打开顺序确定，关闭栈顶会返回下一窗口。</summary>
        [UnityTest]
        public IEnumerator TopFormAndCloseTopFollowWindowOrder()
        {
            Task<IUIForm> lowerOpening = uiModule.OpenUIFormAsync("Lower", windowLayer: 100);
            yield return WaitForTask(lowerOpening);
            Task<IUIForm> firstTopOpening = uiModule.OpenUIFormAsync("FirstTop", windowLayer: 200);
            yield return WaitForTask(firstTopOpening);
            Task<IUIForm> lastTopOpening = uiModule.OpenUIFormAsync("LastTop", windowLayer: 200);
            yield return WaitForTask(lastTopOpening);

            Assert.AreEqual("LastTop", uiModule.GetTopUIForm().AssetName);
            Assert.IsTrue(uiModule.CloseTopUIForm());
            Assert.AreEqual("FirstTop", uiModule.GetTopUIForm().AssetName);
            Assert.IsTrue(uiModule.CloseTopUIForm());
            Assert.AreEqual("Lower", uiModule.GetTopUIForm().AssetName);
            Assert.IsTrue(uiModule.CloseTopUIForm());
            Assert.IsNull(uiModule.GetTopUIForm());
            Assert.IsFalse(uiModule.CloseTopUIForm());
        }

        /// <summary>验证默认 Helper 将 UI 挂到精确层级，并以 UI Root 作为回退。</summary>
        [Test]
        public void DefaultHelperUsesConfiguredLayerRootAndFallbackRoot()
        {
            GameObject helperObject = new GameObject("UIHelperTest");
            GameObject uiRootObject = new GameObject("UIRoot", typeof(RectTransform));
            GameObject panelRootObject = new GameObject("PanelRoot", typeof(RectTransform));
            GameObject canvasRootObject = new GameObject("CanvasRoot", typeof(RectTransform));
            GameObject canvasPanelRootObject = new GameObject("CanvasPanelRoot", typeof(RectTransform));
            GameObject prefab = new GameObject("FormPrefab", typeof(RectTransform));
            GameObject canvasPrefab = new GameObject("CanvasFormPrefab", typeof(RectTransform), typeof(Canvas));
            GameObject panelInstance = null;
            GameObject fallbackInstance = null;
            GameObject secondPanelInstance = null;
            GameObject canvasInstance = null;
            try
            {
                RectTransform uiRoot = uiRootObject.GetComponent<RectTransform>();
                RectTransform panelRoot = panelRootObject.GetComponent<RectTransform>();
                RectTransform canvasRoot = canvasRootObject.GetComponent<RectTransform>();
                RectTransform canvasPanelRoot = canvasPanelRootObject.GetComponent<RectTransform>();
                panelRoot.SetParent(uiRoot, false);
                canvasPanelRoot.SetParent(canvasRoot, false);

                DefaultUIHelper helper = helperObject.AddComponent<DefaultUIHelper>();
                helper.SetUIRoot(uiRoot);
                helper.SetLayerRoot(UILayer.Panel, panelRoot);
                helper.SetCanvasRoot(canvasRoot);
                helper.SetCanvasLayerRoot(UILayer.Panel, canvasPanelRoot);

                RectTransform sourceRect = prefab.GetComponent<RectTransform>();
                sourceRect.anchorMin = new Vector2(0.2f, 0.3f);
                sourceRect.anchorMax = new Vector2(0.7f, 0.8f);
                sourceRect.sizeDelta = new Vector2(320, 180);
                sourceRect.anchoredPosition = new Vector2(24, -16);
                panelInstance = (GameObject)helper.InstantiateUI(prefab);
                helper.CreateUIForm(panelInstance, "Panel", UILayer.Panel, false);
                secondPanelInstance = (GameObject)helper.InstantiateUI(prefab);
                helper.CreateUIForm(secondPanelInstance, "SecondPanel", UILayer.Panel, false);
                fallbackInstance = (GameObject)helper.InstantiateUI(prefab);
                helper.CreateUIForm(fallbackInstance, "Custom", 250, false);
                canvasInstance = (GameObject)helper.InstantiateUI(canvasPrefab);
                helper.CreateUIForm(canvasInstance, "CanvasPanel", UILayer.Panel, false);

                Assert.AreSame(panelRoot, panelInstance.transform.parent);
                Assert.AreSame(uiRoot, fallbackInstance.transform.parent);
                Assert.AreSame(canvasPanelRoot, canvasInstance.transform.parent);
                Assert.Less(panelInstance.transform.GetSiblingIndex(),
                    secondPanelInstance.transform.GetSiblingIndex());
                RectTransform instanceRect = panelInstance.GetComponent<RectTransform>();
                Assert.AreEqual(sourceRect.anchorMin, instanceRect.anchorMin);
                Assert.AreEqual(sourceRect.anchorMax, instanceRect.anchorMax);
                Assert.AreEqual(sourceRect.sizeDelta, instanceRect.sizeDelta);
                Assert.AreEqual(sourceRect.anchoredPosition, instanceRect.anchoredPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(panelInstance);
                UnityEngine.Object.DestroyImmediate(secondPanelInstance);
                UnityEngine.Object.DestroyImmediate(fallbackInstance);
                UnityEngine.Object.DestroyImmediate(canvasInstance);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(canvasPrefab);
                UnityEngine.Object.DestroyImmediate(panelRootObject);
                UnityEngine.Object.DestroyImmediate(canvasPanelRootObject);
                UnityEngine.Object.DestroyImmediate(canvasRootObject);
                UnityEngine.Object.DestroyImmediate(uiRootObject);
                UnityEngine.Object.DestroyImmediate(helperObject);
            }
        }

        /// <summary>验证外部场景 UI 只由模块代管生命周期，不释放其实例或资源。</summary>
        [Test]
        public void ExternalFormKeepsExternalOwnership()
        {
            FakeUIForm form = new FakeUIForm("SceneHud", 100, false);

            uiModule.RegisterUIForm("SceneHud", form);
            uiModule.UnregisterUIForm("SceneHud");

            Assert.AreEqual(1, form.InitCount);
            Assert.AreEqual(1, form.OpenCount);
            Assert.AreEqual(1, form.CloseCount);
            Assert.AreEqual(0, uiHelper.ReleaseCount);
            Assert.IsEmpty(resourceHelper.ReleasedLocations);
        }

        private static IEnumerator WaitForTask(Task task)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (!task.IsCompleted)
            {
                Assert.Less(DateTime.UtcNow, deadline, "异步 UI 测试超时。");
                yield return null;
            }
        }

        private static void AssertTaskSucceeded(Task task)
        {
            Assert.IsFalse(task.IsCanceled, "异步任务不应被取消。");
            Assert.IsFalse(task.IsFaulted, GetTaskException(task)?.ToString());
        }

        private static Exception GetTaskException(Task task)
        {
            return task.Exception?.GetBaseException();
        }

        private sealed class RecordingResourceHelper : IResourceHelper
        {
            private TaskCompletionSource<object> delayedLoad;

            public bool DelayLoads { get; set; }

            public List<string> ReleasedLocations { get; } = new List<string>();

            public Task InitializeAsync(
                string packageName,
                ResourcePlayMode playMode,
                string defaultHostServer,
                string fallbackHostServer)
            {
                return Task.CompletedTask;
            }

            public void Destroy()
            {
            }

            public Task<object> LoadAssetAsync(
                string location,
                Type assetType,
                uint priority,
                CancellationToken ct = default,
                IProgress<float> onProgress = null)
            {
                if (!DelayLoads)
                {
                    return Task.FromResult<object>(new object());
                }

                delayedLoad = new TaskCompletionSource<object>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                return delayedLoad.Task;
            }

            public object LoadAssetSync(string location, Type assetType)
            {
                return new object();
            }

            public void ReleaseAsset(string location, Type assetType)
            {
                ReleasedLocations.Add(location);
            }

            public Task LoadSceneAsync(
                string location,
                int sceneMode,
                bool activateOnLoad,
                uint priority,
                IProgress<float> onProgress = null)
            {
                return Task.CompletedTask;
            }

            public Task UnloadSceneAsync(string location)
            {
                return Task.CompletedTask;
            }

            public bool IsLocationValid(string location)
            {
                return true;
            }

            public long GetDownloadSize(string location)
            {
                return 0L;
            }

            public void CompleteDelayedLoad()
            {
                delayedLoad.SetResult(new object());
            }
        }

        private sealed class RecordingUIHelper : IUIHelper
        {
            public bool FailCreate { get; set; }

            public int InstantiateCount { get; private set; }

            public int ReleaseCount { get; private set; }

            public object InstantiateUI(object uiAsset)
            {
                InstantiateCount++;
                return new object();
            }

            public IUIForm CreateUIForm(
                object uiInstance,
                string assetName,
                int windowLayer,
                bool fullScreen)
            {
                return FailCreate
                    ? null
                    : new FakeUIForm(assetName, windowLayer, fullScreen);
            }

            public void ReleaseUI(object uiInstance)
            {
                ReleaseCount++;
            }
        }

        private sealed class FakeUIForm : IUIForm
        {
            public FakeUIForm(string assetName, int windowLayer, bool fullScreen)
            {
                AssetName = assetName;
                WindowLayer = windowLayer;
                FullScreen = fullScreen;
                Handle = new object();
            }

            public string AssetName { get; }

            public object Handle { get; }

            public int WindowLayer { get; }

            public bool FullScreen { get; }

            public bool IsOpened { get; private set; }

            public int InitCount { get; private set; }

            public int OpenCount { get; private set; }

            public int PauseCount { get; private set; }

            public int ResumeCount { get; private set; }

            public int CloseCount { get; private set; }

            public bool ThrowOnClose { get; set; }

            public void OnInit(object userData)
            {
                InitCount++;
            }

            public void OnOpen(object userData)
            {
                IsOpened = true;
                OpenCount++;
            }

            public void OnPause()
            {
                PauseCount++;
            }

            public void OnResume()
            {
                ResumeCount++;
            }

            public void OnClose(object userData)
            {
                IsOpened = false;
                CloseCount++;
                if (ThrowOnClose)
                {
                    throw new InvalidOperationException("Close failed for test.");
                }
            }

            public void OnUpdate(float elapseSeconds, float realElapseSeconds)
            {
            }
        }
    }
}
