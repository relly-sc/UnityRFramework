using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;
using UnityEngine.UI;
using UnityRFramework.Runtime;

namespace UnityRFramework.Sample
{
    /// <summary>
    /// Download Sample 的轻量验收控制器。
    /// 负责采集界面参数、驱动下载任务并展示断点续传、校验和多格式解压结果。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DownloadAcceptanceController : MonoBehaviour
    {
        private const int MaxLogLines = 12;

        [Header("Input")]
        [SerializeField]
        [Tooltip("待下载文件的完整 URL。")]
        private InputField urlInput;

        [SerializeField]
        [Tooltip("预期文件字节数；留空表示不校验。")]
        private InputField expectedSizeInput;

        [SerializeField]
        [Tooltip("预期 SHA-256 十六进制字符串；留空表示不校验。")]
        private InputField sha256Input;

        [SerializeField]
        [Tooltip("下载成功后是否解压压缩文件。")]
        private Toggle extractArchiveToggle;

        [SerializeField]
        [Tooltip("本次验收使用的压缩文件解压辅助器。")]
        private Dropdown archiveHelperDropdown;

        [SerializeField]
        [Tooltip("压缩文件格式；Auto 表示由辅助器根据文件内容识别。")]
        private Dropdown archiveFormatDropdown;

        [SerializeField]
        [Tooltip("压缩文件密码；未加密时留空。")]
        private InputField archivePasswordInput;

        [SerializeField]
        [Tooltip("压缩文件解压到验收目录下的子目录名。")]
        private InputField extractDirectoryInput;

        [SerializeField]
        [Tooltip("解压成功后是否删除压缩包。")]
        private Toggle deleteArchiveToggle;

        [Header("Commands")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button deletePartButton;
        [SerializeField] private Button deleteResultButton;
        [SerializeField] private Button restartButton;

        [Header("Output")]
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Text statusText;
        [SerializeField] private Text metricsText;
        [SerializeField] private Text pathText;
        [SerializeField] private Text logText;

        private readonly Queue<string> logLines = new Queue<string>();
        private CancellationTokenSource downloadCts;
        private bool isDownloading;
        private bool isDestroyed;
        private readonly List<Type> archiveHelperTypes = new List<Type>();
        private readonly List<ArchiveFormat> archiveFormats = new List<ArchiveFormat>();

        /// <summary>
        /// 生命周期：启动。绑定按钮并显示验收目录。
        /// </summary>
        private void Start()
        {
            InitializeArchiveControls();
            BindEvents();
            RefreshExtractControls();
            SetIdleState("等待下载");
            AppendLog("验收目录：" + GetDownloadRoot());

            if (GameEntry.Download == null)
            {
                SetIdleState("Download 模块未就绪");
                AppendLog("请确认场景中存在带 DownloadComponent 的 UnityRFramework 预制体。");
            }
        }

        /// <summary>
        /// 生命周期：销毁。取消当前控制器发起的任务并解绑事件。
        /// </summary>
        private void OnDestroy()
        {
            isDestroyed = true;
            UnbindEvents();
            CancelCurrentDownload();
        }

        private void BindEvents()
        {
            startButton.onClick.AddListener(OnStartClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
            cancelButton.onClick.AddListener(OnCancelClicked);
            deletePartButton.onClick.AddListener(OnDeletePartClicked);
            deleteResultButton.onClick.AddListener(OnDeleteResultClicked);
            restartButton.onClick.AddListener(OnRestartClicked);
            extractArchiveToggle.onValueChanged.AddListener(OnExtractArchiveChanged);
            archiveHelperDropdown.onValueChanged.AddListener(OnArchiveHelperChanged);
        }

        private void UnbindEvents()
        {
            startButton.onClick.RemoveListener(OnStartClicked);
            continueButton.onClick.RemoveListener(OnContinueClicked);
            cancelButton.onClick.RemoveListener(OnCancelClicked);
            deletePartButton.onClick.RemoveListener(OnDeletePartClicked);
            deleteResultButton.onClick.RemoveListener(OnDeleteResultClicked);
            restartButton.onClick.RemoveListener(OnRestartClicked);
            extractArchiveToggle.onValueChanged.RemoveListener(OnExtractArchiveChanged);
            archiveHelperDropdown.onValueChanged.RemoveListener(OnArchiveHelperChanged);
        }

        private void OnStartClicked()
        {
            RunDownloadAsync(false);
        }

        private void OnContinueClicked()
        {
            RunDownloadAsync(true);
        }

        private void OnCancelClicked()
        {
            if (!isDownloading)
            {
                AppendLog("当前没有进行中的下载任务。");
                return;
            }

            statusText.text = "正在取消";
            CancelCurrentDownload();
        }

        private void OnDeletePartClicked()
        {
            if (!CanModifyFiles())
            {
                return;
            }

            try
            {
                string url = GetRequiredText(urlInput, "下载 URL");
                string partPath = GetSavePath(url) + ".part";
                bool deleted = DeleteFileIfExists(partPath);
                AppendLog(deleted ? "已删除分片：" + partPath : "没有可删除的 .part 分片。");
            }
            catch (Exception exception)
            {
                ReportCommandError("删除分片失败", exception);
            }
        }

        private void OnDeleteResultClicked()
        {
            if (!CanModifyFiles())
            {
                return;
            }

            try
            {
                string url = GetRequiredText(urlInput, "下载 URL");
                string savePath = GetSavePath(url);
                bool deletedFile = DeleteFileIfExists(savePath);
                bool deletedDirectory = DeleteDirectoryIfExists(GetExtractPath());
                AppendLog(deletedFile || deletedDirectory
                    ? "已删除下载结果和解压目录。"
                    : "没有可删除的下载结果。");
            }
            catch (Exception exception)
            {
                ReportCommandError("删除结果失败", exception);
            }
        }

        private void OnRestartClicked()
        {
            CancelCurrentDownload();
            GameEntry.Restart();
        }

        private void OnExtractArchiveChanged(bool _)
        {
            RefreshExtractControls();
        }

        private void OnArchiveHelperChanged(int index)
        {
            if (index < 0 || index >= archiveHelperTypes.Count || GameEntry.Download == null)
            {
                return;
            }

            Type helperType = archiveHelperTypes[index];
            try
            {
                GameEntry.Download.SetArchiveHelper(
                    (IArchiveHelper)Activator.CreateInstance(helperType));
                AppendLog("解压 Helper：" + helperType.FullName);
            }
            catch (Exception exception)
            {
                ReportCommandError("切换解压 Helper 失败", exception);
            }
        }

        private async void RunDownloadAsync(bool resume)
        {
            if (isDownloading)
            {
                AppendLog("已有下载任务正在执行。");
                return;
            }

            try
            {
                string url = GetRequiredText(urlInput, "下载 URL");
                string savePath = GetSavePath(url);
                DownloadOptions options = CreateOptions(resume);

                Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                if (!resume)
                {
                    DeleteFileIfExists(savePath + ".part");
                }

                downloadCts = new CancellationTokenSource();
                SetDownloadingState(true);
                statusText.text = resume ? "正在续传" : "正在下载";
                progressSlider.value = 0f;
                AppendLog((resume ? "继续下载：" : "重新下载：") + url);

                IProgress<DownloadProgress> progress = new Progress<DownloadProgress>(UpdateProgress);
                DownloadResult result = await GameEntry.Download.DownloadAsync(
                    url,
                    savePath,
                    options,
                    progress,
                    downloadCts.Token);

                if (isDestroyed)
                {
                    return;
                }

                progressSlider.value = 1f;
                statusText.text = result.Extracted ? "下载并解压成功" : "下载成功";
                metricsText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "大小 {0} | 请求 {1} 次 | 续传 {2}",
                    FormatBytes(result.FileSize),
                    result.RequestCount,
                    result.Resumed ? "是" : "否");
                pathText.text = BuildPathText(savePath, result.ExtractDirectory);
                AppendLog("完成：" + result.FilePath);
            }
            catch (OperationCanceledException)
            {
                if (!isDestroyed)
                {
                    statusText.text = "已取消，可继续下载";
                    AppendLog("任务已取消，.part 分片会保留。");
                }
            }
            catch (Exception exception)
            {
                if (!isDestroyed)
                {
                    statusText.text = "下载失败";
                    AppendLog(exception.Message);
                    Log.Error("[Sample.Download] Download failed: {0}", exception);
                }
            }
            finally
            {
                if (!isDestroyed)
                {
                    SetDownloadingState(false);
                    RefreshPathText();
                }

                downloadCts?.Dispose();
                downloadCts = null;
            }
        }

        private DownloadOptions CreateOptions(bool resume)
        {
            long expectedSize = ParseExpectedSize();
            bool extractArchive = extractArchiveToggle.isOn;
            return new DownloadOptions
            {
                Resume = resume,
                OverwriteExisting = true,
                MaxRetries = 2,
                RetryDelayMilliseconds = 500,
                ExpectedSize = expectedSize,
                ExpectedSha256 = NullIfWhiteSpace(sha256Input.text),
                ExtractArchive = extractArchive,
                ArchiveFormat = GetSelectedArchiveFormat(),
                ArchivePassword = NullIfWhiteSpace(archivePasswordInput.text),
                ExtractDirectory = extractArchive ? GetExtractPath() : null,
                DeleteArchiveAfterExtraction = extractArchive && deleteArchiveToggle.isOn
            };
        }

        private void InitializeArchiveControls()
        {
            archiveFormats.Clear();
            archiveFormats.AddRange((ArchiveFormat[])Enum.GetValues(typeof(ArchiveFormat)));
            archiveFormatDropdown.ClearOptions();
            archiveFormatDropdown.AddOptions(
                archiveFormats.Select(format => format.ToString()).ToList());
            archiveFormatDropdown.value = archiveFormats.IndexOf(ArchiveFormat.Auto);
            archiveFormatDropdown.RefreshShownValue();

            archiveHelperTypes.Clear();
            archiveHelperTypes.AddRange(GetArchiveHelperTypes());
            archiveHelperDropdown.ClearOptions();
            archiveHelperDropdown.AddOptions(
                archiveHelperTypes.Select(GetHelperDisplayName).ToList());
            int preferredIndex = archiveHelperTypes.FindIndex(type =>
                type.Name == "SharpCompressArchiveHelper");
            archiveHelperDropdown.value = preferredIndex >= 0 ? preferredIndex : 0;
            archiveHelperDropdown.RefreshShownValue();
            OnArchiveHelperChanged(archiveHelperDropdown.value);
        }

        private static IEnumerable<Type> GetArchiveHelperTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(type => typeof(IArchiveHelper).IsAssignableFrom(type)
                    && type.IsClass
                    && !type.IsAbstract
                    && type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(type => type == typeof(DefaultArchiveHelper) ? 0 : 1)
                .ThenBy(type => type.Name, StringComparer.Ordinal);
        }

        private static IEnumerable<Type> GetLoadableTypes(System.Reflection.Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }

        private static string GetHelperDisplayName(Type type)
        {
            const string suffix = "ArchiveHelper";
            return type.Name.EndsWith(suffix, StringComparison.Ordinal)
                ? type.Name.Substring(0, type.Name.Length - suffix.Length)
                : type.Name;
        }

        private ArchiveFormat GetSelectedArchiveFormat()
        {
            int index = archiveFormatDropdown.value;
            return index >= 0 && index < archiveFormats.Count
                ? archiveFormats[index]
                : ArchiveFormat.Auto;
        }

        private long ParseExpectedSize()
        {
            string value = NullIfWhiteSpace(expectedSizeInput.text);
            if (value == null)
            {
                return -1L;
            }

            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long result)
                || result < 0L)
            {
                throw new RFrameworkException("预期大小必须是大于或等于 0 的字节数。");
            }

            return result;
        }

        private void UpdateProgress(DownloadProgress progress)
        {
            if (isDestroyed)
            {
                return;
            }

            progressSlider.value = Mathf.Clamp01(progress.Progress);
            if (progress.Stage == DownloadStage.Preflight)
            {
                statusText.text = "正在预检远端大小";
                metricsText.text = "正在读取服务器 Content-Length";
                return;
            }

            if (progress.Stage == DownloadStage.Verifying)
            {
                statusText.text = "正在校验文件";
                metricsText.text = "正在校验实际大小和 SHA-256";
                return;
            }

            string total = progress.TotalBytes >= 0 ? FormatBytes(progress.TotalBytes) : "未知";
            if (progress.Stage == DownloadStage.Extracting)
            {
                statusText.text = "正在解压";
                metricsText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} / {1} | {2}",
                    FormatBytes(progress.DownloadedBytes),
                    total,
                    string.IsNullOrEmpty(progress.CurrentEntry) ? "准备中" : progress.CurrentEntry);
                return;
            }

            statusText.text = progress.IsResuming ? "正在续传" : "正在下载";
            string eta = progress.EstimatedRemaining.HasValue
                ? FormatDuration(progress.EstimatedRemaining.Value)
                : "未知";
            metricsText.text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} / {1} | {2}/s | ETA {3} | {4}",
                FormatBytes(progress.DownloadedBytes),
                total,
                FormatBytes((long)progress.BytesPerSecond),
                eta,
                progress.IsResuming ? "续传" : "新下载");
        }

        private void SetIdleState(string status)
        {
            statusText.text = status;
            metricsText.text = "0 B / 未知 | 0 B/s | ETA 未知";
            progressSlider.value = 0f;
            SetDownloadingState(false);
            RefreshPathText();
        }

        private void SetDownloadingState(bool downloading)
        {
            isDownloading = downloading;
            startButton.interactable = !downloading;
            continueButton.interactable = !downloading;
            cancelButton.interactable = downloading;
            deletePartButton.interactable = !downloading;
            deleteResultButton.interactable = !downloading;
            restartButton.interactable = true;
            RefreshExtractControls();
        }

        private void RefreshExtractControls()
        {
            bool enabled = extractArchiveToggle.isOn;
            archiveHelperDropdown.interactable = enabled && !isDownloading;
            archiveFormatDropdown.interactable = enabled && !isDownloading;
            archivePasswordInput.interactable = enabled && !isDownloading;
            extractDirectoryInput.interactable = enabled;
            deleteArchiveToggle.interactable = enabled;
        }

        private void RefreshPathText()
        {
            try
            {
                string savePath = GetSavePath(GetRequiredText(urlInput, "下载 URL"));
                pathText.text = BuildPathText(savePath, GetExtractPath());
            }
            catch (Exception)
            {
                pathText.text = "请填写有效的下载 URL。";
            }
        }

        private bool CanModifyFiles()
        {
            if (!isDownloading)
            {
                return true;
            }

            AppendLog("请先取消当前下载任务。");
            return false;
        }

        private void CancelCurrentDownload()
        {
            if (downloadCts != null && !downloadCts.IsCancellationRequested)
            {
                downloadCts.Cancel();
            }
        }

        private static string GetSavePath(string url)
        {
            string fileName = null;
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                fileName = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "download.bin";
            }

            return Path.Combine(GetDownloadRoot(), fileName);
        }

        private string GetExtractPath()
        {
            string directoryName = Path.GetFileName(NullIfWhiteSpace(extractDirectoryInput.text) ?? "Extracted");
            return Path.Combine(GetDownloadRoot(), directoryName);
        }

        private static string GetDownloadRoot()
        {
            return Path.Combine(
                Application.persistentDataPath,
                "UnityRFramework",
                "DownloadAcceptance");
        }

        private static string GetRequiredText(InputField input, string fieldName)
        {
            string value = NullIfWhiteSpace(input.text);
            if (value == null)
            {
                throw new RFrameworkException(fieldName + "不能为空。");
            }

            return value;
        }

        private static string NullIfWhiteSpace(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static bool DeleteFileIfExists(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }

        private static bool DeleteDirectoryIfExists(string path)
        {
            if (!Directory.Exists(path))
            {
                return false;
            }

            Directory.Delete(path, true);
            return true;
        }

        private static string BuildPathText(string savePath, string extractPath)
        {
            return "文件：" + savePath
                + "\n分片：" + savePath + ".part"
                + "\n解压：" + extractPath;
        }

        private void ReportCommandError(string title, Exception exception)
        {
            statusText.text = title;
            AppendLog(exception.Message);
            Log.Error("[Sample.Download] {0}: {1}", title, exception);
        }

        private void AppendLog(string message)
        {
            logLines.Enqueue(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + message);
            while (logLines.Count > MaxLogLines)
            {
                logLines.Dequeue();
            }

            logText.text = string.Join("\n", logLines);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024L)
            {
                return bytes + " B";
            }

            if (bytes < 1024L * 1024L)
            {
                return (bytes / 1024d).ToString("F1", CultureInfo.InvariantCulture) + " KB";
            }

            if (bytes < 1024L * 1024L * 1024L)
            {
                return (bytes / (1024d * 1024d)).ToString("F1", CultureInfo.InvariantCulture) + " MB";
            }

            return (bytes / (1024d * 1024d * 1024d)).ToString("F2", CultureInfo.InvariantCulture) + " GB";
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1d)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:00}:{1:00}:{2:00}",
                    (int)duration.TotalHours,
                    duration.Minutes,
                    duration.Seconds);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}",
                (int)duration.TotalMinutes,
                duration.Seconds);
        }
    }
}
