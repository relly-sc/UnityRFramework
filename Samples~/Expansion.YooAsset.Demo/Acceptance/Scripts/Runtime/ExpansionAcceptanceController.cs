using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// Expansion 第三方 Helper 端到端验收控制器。
    /// 场景布局和引用由 ExpansionAcceptanceBuilder 在编辑器中生成，本类只负责状态、文本和事件。
    /// </summary>
    public sealed class ExpansionAcceptanceController : MonoBehaviour
    {
        [SerializeField]
        private Text statusText;

        [SerializeField]
        private Button runButton;

        [SerializeField]
        private Button cancelButton;

        [SerializeField]
        private Button restartButton;

        [SerializeField]
        private string rawFileLocation = "ExpansionProbe";

        [SerializeField]
        private string additiveSceneLocation = "ExpansionContent";

        [SerializeField]
        private string remoteProbeLocation = "RemoteProbe";

        [SerializeField]
        private string webProbeRelativePath = "ExpansionAcceptance/WebProbe.txt";

        [SerializeField]
        private bool runOnStart = true;

        [SerializeField]
        private bool restartOnceAfterSuccess = true;

        private static bool restartAttempted;

        private CancellationTokenSource lifetimeCts;
        private bool isRunning;

        private void Awake()
        {
            lifetimeCts = new CancellationTokenSource();
            runButton.onClick.AddListener(RunAcceptance);
            cancelButton.onClick.AddListener(RunCancellationProbe);
            restartButton.onClick.AddListener(RestartFramework);
        }

        private void Start()
        {
            if (runOnStart)
            {
                RunAcceptance();
            }
        }

        private void OnDestroy()
        {
            runButton?.onClick.RemoveListener(RunAcceptance);
            cancelButton?.onClick.RemoveListener(RunCancellationProbe);
            restartButton?.onClick.RemoveListener(RestartFramework);

            if (lifetimeCts != null)
            {
                lifetimeCts.Cancel();
                lifetimeCts.Dispose();
                lifetimeCts = null;
            }
        }

        /// <summary>
        /// 启动完整的 Expansion 验收流程。
        /// </summary>
        public void RunAcceptance()
        {
            if (isRunning || lifetimeCts == null)
            {
                return;
            }

            _ = RunAcceptanceAsync(lifetimeCts.Token);
        }

        /// <summary>
        /// 单独运行正在进行中的 Web 请求取消验证。
        /// </summary>
        public void RunCancellationProbe()
        {
            if (isRunning || lifetimeCts == null)
            {
                return;
            }

            _ = RunCancellationProbeFromButtonAsync(lifetimeCts.Token);
        }

        /// <summary>
        /// 在不退出应用进程的情况下重启框架。
        /// 当前 ExpansionAcceptance 场景必须位于 Build Settings 的第 0 项。
        /// </summary>
        public void RestartFramework()
        {
            AppendStatus("请求框架软重启。");
            GameEntry.Restart();
        }

        private async Task RunAcceptanceAsync(CancellationToken ct)
        {
            isRunning = true;
            SetButtonsInteractable(false);
            statusText.text = string.Empty;

            try
            {
                AppendStatus("1/6 初始化 YooAsset Resource Helper...");
                await GameEntry.Resource.InitializeAsync();
                ct.ThrowIfCancellationRequested();

                AppendStatus("2/6 加载 YooAsset 二进制 TextAsset...");
                byte[] data = await GameEntry.Resource.LoadAssetAsync<byte[]>(rawFileLocation, 0, ct);
                string text = data != null ? Encoding.UTF8.GetString(data) : string.Empty;
                if (!text.Contains("UnityRFramework Expansion"))
                {
                    throw new RFrameworkException("二进制 TextAsset 内容校验失败。");
                }

                GameEntry.Resource.UnloadAsset<byte[]>(rawFileLocation);
                AppendStatus($"二进制 TextAsset 通过，{data.Length} bytes。");

                AppendStatus("3/6 加载并卸载 YooAsset Additive 场景...");
                await GameEntry.Resource.LoadSceneAsync(
                    additiveSceneLocation,
                    (int)LoadSceneMode.Additive,
                    true,
                    0);
                await GameEntry.Resource.UnloadSceneAsync(additiveSceneLocation);
                AppendStatus("场景加载与卸载通过。");

                AppendStatus("4/6 加载 YooAsset 远程 JSON 探针...");
                byte[] remoteData = await GameEntry.Resource.LoadAssetAsync<byte[]>(
                    remoteProbeLocation,
                    0,
                    ct);
                string remoteJson = remoteData != null
                    ? Encoding.UTF8.GetString(remoteData)
                    : string.Empty;
                RemoteProbePayload remoteProbe =
                    JsonUtility.FromJson<RemoteProbePayload>(remoteJson);
                if (remoteProbe == null
                    || remoteProbe.Version <= 0
                    || string.IsNullOrWhiteSpace(remoteProbe.Message))
                {
                    throw new RFrameworkException("YooAsset 远程 JSON 探针内容校验失败。");
                }

                GameEntry.Resource.UnloadAsset<byte[]>(remoteProbeLocation);
                AppendStatus(
                    $"远程探针通过：v{remoteProbe.Version}，{remoteProbe.Message}");

                AppendStatus("5/6 执行 UniTask Web 请求...");
                string webProbeUrl = BuildWebProbeUrl();
                WebResponse response = await GameEntry.WebRequest.GetAsync(
                    webProbeUrl,
                    null,
                    null,
                    "ExpansionAcceptance",
                    0,
                    ct);
                if (response == null || !response.IsSuccess)
                {
                    throw new RFrameworkException(
                        $"Web 请求失败：{response?.StatusCode} {response?.Error} {response?.ErrorMessage}");
                }

                AppendStatus($"Web 请求通过，HTTP {response.StatusCode}。");

                AppendStatus("6/6 执行 Web 请求取消...");
                await VerifyCancellationAsync(ct);
                AppendStatus("Web 请求取消通过。");

                AppendStatus(restartAttempted
                    ? "PASS：框架重启后全部第三方链路再次通过。"
                    : "PASS：首次第三方链路通过。");

                if (restartOnceAfterSuccess && !restartAttempted)
                {
                    restartAttempted = true;
                    AppendStatus("即将执行一次框架软重启...");
                    await Task.Delay(500, ct);
                    GameEntry.Restart();
                }
            }
            catch (OperationCanceledException)
            {
                AppendStatus("验收流程已取消。");
            }
            catch (Exception exception)
            {
                AppendStatus($"FAIL：{exception.Message}");
                Log.Error("[ExpansionAcceptance] Acceptance failed: {0}", exception);
            }
            finally
            {
                isRunning = false;
                SetButtonsInteractable(true);
            }
        }

        private async Task RunCancellationProbeFromButtonAsync(CancellationToken ct)
        {
            isRunning = true;
            SetButtonsInteractable(false);
            try
            {
                AppendStatus("开始单独验证 Web 请求取消...");
                await VerifyCancellationAsync(ct);
                AppendStatus("Web 请求取消通过。");
            }
            catch (Exception exception)
            {
                AppendStatus($"取消验证失败：{exception.Message}");
                Log.Error("[ExpansionAcceptance] Cancellation probe failed: {0}", exception);
            }
            finally
            {
                isRunning = false;
                SetButtonsInteractable(true);
            }
        }

        private async Task VerifyCancellationAsync(CancellationToken lifetimeToken)
        {
            using (CancellationTokenSource requestCts =

                   CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken))
            {
                Task<WebResponse> requestTask = GameEntry.WebRequest.GetAsync(
                    BuildWebProbeUrl(),
                    null,
                    null,
                    "ExpansionAcceptanceCancellation",
                    0,
                    requestCts.Token);

                requestCts.Cancel();

                WebResponse response = await requestTask;
                if (response == null || response.Error != WebRequestError.Aborted)
                {
                    throw new RFrameworkException(
                        $"取消结果不符合预期：{response?.StatusCode} {response?.Error} {response?.ErrorMessage}");
                }
            }
        }

        private string BuildWebProbeUrl()
        {
            string path = Application.streamingAssetsPath.TrimEnd('/', '\\')
                + "/" + webProbeRelativePath.TrimStart('/', '\\');
            if (path.Contains("://") || path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return new Uri(path).AbsoluteUri;
        }

        private void AppendStatus(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            statusText.text = string.IsNullOrEmpty(statusText.text)
                ? line
                : $"{statusText.text}\n{line}";
            Log.Info("[ExpansionAcceptance] {0}", message);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            runButton.interactable = interactable;
            cancelButton.interactable = interactable;
            restartButton.interactable = interactable;
        }

        [Serializable]
        private sealed class RemoteProbePayload
        {
            [SerializeField]
            private int version;

            [SerializeField]
            private string message;

            public int Version => version;

            public string Message => message;
        }
    }
}
