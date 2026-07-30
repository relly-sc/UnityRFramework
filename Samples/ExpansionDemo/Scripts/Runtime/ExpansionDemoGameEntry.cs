using System;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;
using UnityEngine.UI;
using UnityRFramework.Expansion;
using UnityRFramework.Runtime;

/// <summary>
/// ExpansionDemo 启动入口。
/// 在进入官方 Demo 流程前检查远程清单并完成缺失资源下载。
/// </summary>
public sealed class ExpansionDemoGameEntry : MonoBehaviour
{
    private static readonly string[] StartupResourceTags = { "preload" };

    [SerializeField]
    [Tooltip("资源更新界面根节点。")]
    private GameObject updatePanel;

    [SerializeField]
    [Tooltip("更新阶段标题。")]
    private Text statusText;

    [SerializeField]
    [Tooltip("文件数量、下载大小和网速等详细信息。")]
    private Text detailText;

    [SerializeField]
    [Tooltip("下载进度条填充图。")]
    private Image progressFill;

    [SerializeField]
    [Tooltip("下载百分比文字。")]
    private Text progressText;

    [SerializeField]
    [Tooltip("下载或重试按钮。")]
    private Button primaryButton;

    [SerializeField]
    [Tooltip("下载或重试按钮文字。")]
    private Text primaryButtonText;

    [SerializeField]
    [Tooltip("退出应用按钮。")]
    private Button quitButton;

    private CancellationTokenSource startupCts;
    private IResourceUpdateService updateService;
    private ResourceUpdateInfo updateInfo;
    private Action primaryAction;
    private bool isBusy;
    private bool hasStartedDemo;
    private bool isDestroying;
    private float downloadStartedTime;

    /// <summary>
    /// 绑定按钮并启动资源检查。
    /// </summary>
    private void Start()
    {
        if (primaryButton != null)
        {
            primaryButton.onClick.AddListener(OnPrimaryButtonClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(OnQuitButtonClicked);
        }

        startupCts = new CancellationTokenSource();
        _ = CheckForUpdatesAsync(startupCts.Token);
    }

    /// <summary>
    /// 销毁时取消尚未完成的检查或下载。
    /// </summary>
    private void OnDestroy()
    {
        isDestroying = true;

        if (primaryButton != null)
        {
            primaryButton.onClick.RemoveListener(OnPrimaryButtonClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitButtonClicked);
        }

        startupCts?.Cancel();
        startupCts?.Dispose();
        startupCts = null;
    }

    /// <summary>
    /// 初始化资源包、取得最新远程清单并建立差量下载计划。
    /// </summary>
    private async Task CheckForUpdatesAsync(CancellationToken ct)
    {
        if (isBusy || hasStartedDemo)
        {
            return;
        }

        isBusy = true;
        SetPanelVisible(true);
        SetPrimaryAction(null, "检查中...");
        SetStatus("正在检查资源更新", "正在连接资源服务器...");
        SetProgress(0f, "检查中");

        try
        {
            if (GameEntry.Base == null || GameEntry.Resource == null)
            {
                throw new RFrameworkException(
                    "ExpansionDemo: UnityRFramework prefab is missing.");
            }

            await GameEntry.Resource.InitializeAsync();
            ct.ThrowIfCancellationRequested();

            updateService = FindUpdateService();
            if (updateService == null)
            {
                throw new RFrameworkException(
                    "ExpansionDemo: the current Resource Helper does not provide "
                    + "IResourceUpdateService.");
            }

            updateInfo = PrepareStartupUpdate();
            if (!updateInfo.HasUpdate)
            {
                Log.Info("[ExpansionDemo] Resources are up to date.");
                StartDemo();
                return;
            }

            string size = FormatBytes(updateInfo.TotalBytes);
            SetStatus(
                "发现资源更新",
                $"需要下载 {updateInfo.FileCount} 个文件，共 {size}。");
            SetProgress(0f, "0%");
            SetPrimaryAction(
                () => _ = DownloadUpdatesAsync(startupCts.Token),
                "下载更新");
            Log.Info(
                "[ExpansionDemo] Update found: {0} files, {1}.",
                updateInfo.FileCount,
                size);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 场景销毁或框架退出，正常结束启动任务。
        }
        catch (Exception exception)
        {
            if (ShouldIgnoreFailure(ct))
            {
                return;
            }

            ShowFailure(
                "检查更新失败",
                exception,
                () => _ = CheckForUpdatesAsync(startupCts.Token));
        }
        finally
        {
            isBusy = false;
        }
    }

    /// <summary>
    /// 下载准备好的差量资源，失败后允许重新创建下载计划并重试。
    /// </summary>
    private async Task DownloadUpdatesAsync(CancellationToken ct)
    {
        if (isBusy || hasStartedDemo)
        {
            return;
        }

        isBusy = true;
        SetPrimaryAction(null, "下载中...");
        SetStatus("正在下载资源", BuildDownloadDetail(0L, 0L, 0L, 0, updateInfo.FileCount));
        downloadStartedTime = Time.realtimeSinceStartup;

        try
        {
            Progress<ResourceUpdateProgress> progress =
                new Progress<ResourceUpdateProgress>(OnDownloadProgress);
            await updateService.DownloadPreparedUpdateAsync(progress, ct);
            ct.ThrowIfCancellationRequested();

            SetProgress(1f, "100%");
            Log.Info("[ExpansionDemo] Resource update completed.");
            StartDemo();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 场景销毁或框架退出，正常结束下载任务。
        }
        catch (Exception exception)
        {
            if (ShouldIgnoreFailure(ct))
            {
                return;
            }

            ShowFailure(
                "资源下载失败",
                exception,
                () => _ = RetryDownloadAsync(startupCts.Token));
        }
        finally
        {
            isBusy = false;
        }
    }

    /// <summary>
    /// 下载失败后重新计算差量列表，再执行下载。
    /// 已成功写入缓存的文件不会被重复下载。
    /// </summary>
    private async Task RetryDownloadAsync(CancellationToken ct)
    {
        if (isBusy || hasStartedDemo)
        {
            return;
        }

        isBusy = true;
        SetPrimaryAction(null, "重新检查...");
        try
        {
            updateInfo = PrepareStartupUpdate();
            if (!updateInfo.HasUpdate)
            {
                StartDemo();
                return;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            if (ShouldIgnoreFailure(ct))
            {
                return;
            }

            ShowFailure(
                "重新检查失败",
                exception,
                () => _ = RetryDownloadAsync(startupCts.Token));
            return;
        }
        finally
        {
            isBusy = false;
        }

        await DownloadUpdatesAsync(ct);
    }

    /// <summary>
    /// 接收下载进度并刷新常规下载信息。
    /// </summary>
    private void OnDownloadProgress(ResourceUpdateProgress progress)
    {
        if (isDestroying)
        {
            return;
        }

        float elapsed = Mathf.Max(0.01f, Time.realtimeSinceStartup - downloadStartedTime);
        long bytesPerSecond = (long)(progress.DownloadedBytes / elapsed);
        SetStatus(
            "正在下载资源",
            BuildDownloadDetail(
                progress.DownloadedBytes,
                progress.TotalBytes,
                bytesPerSecond,
                progress.DownloadedFileCount,
                progress.TotalFileCount));
        SetProgress(progress.Progress, $"{progress.Progress * 100f:F1}%");
    }

    /// <summary>
    /// 进入复用的官方 Demo Procedure 流程。
    /// </summary>
    private void StartDemo()
    {
        if (hasStartedDemo)
        {
            return;
        }

        hasStartedDemo = true;
        SetPanelVisible(false);
        GameEntry.Procedure.InitializeFromAssembly<DemoLaunchProcedure>();
        GameEntry.Procedure.StartProcedure<DemoLaunchProcedure>();
    }

    /// <summary>
    /// 从 ResourceComponent 创建的子 Helper 中查找更新服务。
    /// </summary>
    private static IResourceUpdateService FindUpdateService()
    {
        MonoBehaviour[] helpers =
            GameEntry.Resource.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour helper in helpers)
        {
            if (helper is IResourceUpdateService service)
            {
                return service;
            }
        }

        return null;
    }

    /// <summary>
    /// 只检查启动流程所需资源，保留 ondemand 标签资源供运行时按需加载。
    /// 不支持标签过滤的自定义 Helper 仍回退到原有全量更新行为。
    /// </summary>
    private ResourceUpdateInfo PrepareStartupUpdate()
    {
        if (updateService is ITaggedResourceUpdateService taggedUpdateService)
        {
            return taggedUpdateService.PrepareUpdateByTags(StartupResourceTags);
        }

        return updateService.PrepareUpdate();
    }

    private void OnPrimaryButtonClicked()
    {
        primaryAction?.Invoke();
    }

    private static void OnQuitButtonClicked()
    {
        GameEntry.Quit();
    }

    private void ShowFailure(string title, Exception exception, Action retryAction)
    {
        if (isDestroying)
        {
            return;
        }

        if (RFrameworkLog.IsInitialized)
        {
            Log.Error("[ExpansionDemo] {0}: {1}", title, exception);
        }

        SetStatus(title, exception.Message);
        SetPrimaryAction(retryAction, "重试");
    }

    private bool ShouldIgnoreFailure(CancellationToken ct)
    {
        return isDestroying || ct.IsCancellationRequested;
    }

    private void SetStatus(string status, string detail)
    {
        if (statusText != null)
        {
            statusText.text = status;
        }

        if (detailText != null)
        {
            detailText.text = detail;
        }
    }

    private void SetProgress(float value, string label)
    {
        if (progressFill != null)
        {
            progressFill.fillAmount = Mathf.Clamp01(value);
        }

        if (progressText != null)
        {
            progressText.text = label;
        }
    }

    private void SetPrimaryAction(Action action, string label)
    {
        primaryAction = action;
        if (primaryButton != null)
        {
            primaryButton.interactable = action != null;
        }

        if (primaryButtonText != null)
        {
            primaryButtonText.text = label;
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (updatePanel != null)
        {
            updatePanel.SetActive(visible);
        }
    }

    private static string BuildDownloadDetail(
        long downloadedBytes,
        long totalBytes,
        long bytesPerSecond,
        int downloadedFiles,
        int totalFiles)
    {
        return $"文件 {downloadedFiles}/{totalFiles}\n"
            + $"大小 {FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes)}\n"
            + $"速度 {FormatBytes(bytesPerSecond)}/s";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024L)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024L * 1024L)
        {
            return $"{bytes / 1024f:F1} KB";
        }

        if (bytes < 1024L * 1024L * 1024L)
        {
            return $"{bytes / (1024f * 1024f):F1} MB";
        }

        return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
    }
}
