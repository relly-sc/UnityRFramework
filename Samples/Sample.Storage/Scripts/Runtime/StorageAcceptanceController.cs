using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityRFramework.Runtime;

namespace UnityRFramework.Sample.Storage
{
    /// <summary>Storage 独立验收场景控制器。</summary>
    [DisallowMultipleComponent]
    public sealed class StorageAcceptanceController : MonoBehaviour
    {
        private const string KeyId = "SaveKey.SampleStorage";

        [SerializeField] private Dropdown slotDropdown;
        [SerializeField] private InputField playerNameInput;
        [SerializeField] private InputField levelInput;
        [SerializeField] private InputField coinsInput;
        [SerializeField] private Toggle encryptionToggle;
        [SerializeField] private Toggle compressionToggle;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button listButton;
        [SerializeField] private Button corruptButton;
        [SerializeField] private Button deleteKeyButton;
        [SerializeField] private Button recreateKeyButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Text environmentText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text logText;

        private readonly Queue<string> logLines = new Queue<string>();
        private Button[] operationButtons;
        private StorageComponent storage;
        private bool busy;

        private void Start()
        {
            operationButtons = new[]
            {
                saveButton, loadButton, deleteButton, listButton, corruptButton,
                deleteKeyButton, recreateKeyButton
            };
            saveButton.onClick.AddListener(() => Run(SaveAsync));
            loadButton.onClick.AddListener(() => Run(LoadAsync));
            deleteButton.onClick.AddListener(() => Run(DeleteAsync));
            listButton.onClick.AddListener(() => Run(ListAsync));
            corruptButton.onClick.AddListener(() => Run(CorruptAndRecoverAsync));
            deleteKeyButton.onClick.AddListener(DeleteKey);
            recreateKeyButton.onClick.AddListener(RecreateKey);
            restartButton.onClick.AddListener(Restart);
            quitButton.onClick.AddListener(GameEntry.Quit);

            storage = GameEntry.Storage;
            if (storage == null)
            {
                SetStatus("Storage 模块未就绪", true);
                SetOperationsInteractable(false);
                return;
            }

            string keyStore = string.IsNullOrEmpty(storage.ManagedSaveKeyStoreTypeName)
                ? "未启用自动密钥管理"
                : GetShortTypeName(storage.ManagedSaveKeyStoreTypeName);
            environmentText.text = "存档目录：" + storage.StorageRootPath
                + "\n密钥仓：" + keyStore;
            SetStatus("准备完成", false);
            AppendLog("可直接保存，也可开启压缩或加密后保存。", false);
        }

        private void OnDestroy()
        {
            saveButton.onClick.RemoveAllListeners();
            loadButton.onClick.RemoveAllListeners();
            deleteButton.onClick.RemoveAllListeners();
            listButton.onClick.RemoveAllListeners();
            corruptButton.onClick.RemoveAllListeners();
            deleteKeyButton.onClick.RemoveAllListeners();
            recreateKeyButton.onClick.RemoveAllListeners();
            restartButton.onClick.RemoveAllListeners();
            quitButton.onClick.RemoveAllListeners();
        }

        private async void Run(Func<Task> action)
        {
            if (busy || storage == null) return;
            busy = true;
            SetOperationsInteractable(false);
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                SetStatus("操作异常：" + exception.Message, true);
            }
            finally
            {
                busy = false;
                SetOperationsInteractable(true);
            }
        }

        private async Task SaveAsync()
        {
            if (!TryReadData(out StorageAcceptanceData data)) return;
            StorageResult result = await storage.SaveAsync(CurrentSlot, data, CreateOptions());
            ShowResult("保存", result);
        }

        private async Task LoadAsync()
        {
            StorageLoadResult<StorageAcceptanceData> result =
                await storage.LoadAsync<StorageAcceptanceData>(CurrentSlot, CreateOptions());
            if (!result.Succeeded)
            {
                ShowFailure("读取", result.Reason, result.Message);
                return;
            }

            playerNameInput.text = result.Data.PlayerName;
            levelInput.text = result.Data.Level.ToString();
            coinsInput.text = result.Data.Coins.ToString();
            string source = result.RecoveredFromBackup ? "备份" : "主存档";
            SetStatus("读取成功，数据来自" + source, false);
            AppendLog($"读取 {CurrentSlot}：{result.Data.PlayerName}，等级 {result.Data.Level}，金币 {result.Data.Coins}", false);
        }

        private async Task DeleteAsync()
        {
            StorageResult result = await storage.DeleteAsync(CurrentSlot);
            ShowResult("删除", result);
        }

        private async Task ListAsync()
        {
            IReadOnlyList<StorageSlotInfo> slots = await storage.GetSlotsAsync();
            if (slots.Count == 0)
            {
                SetStatus("当前没有存档", false);
                AppendLog("槽位列表为空。", false);
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                StorageSlotInfo slot = slots[i];
                AppendLog($"{slot.SlotName}：{slot.Size} 字节，备份：{(slot.HasBackup ? "有" : "无")}", false);
            }
            SetStatus("已列出 " + slots.Count + " 个槽位", false);
        }

        private async Task CorruptAndRecoverAsync()
        {
            StorageOptions options = CreateOptions();
            StorageLoadResult<StorageAcceptanceData> previous =
                await storage.LoadAsync<StorageAcceptanceData>(CurrentSlot, options);
            if (!previous.Succeeded)
            {
                ShowFailure("读取待备份数据", previous.Reason, previous.Message);
                AppendLog("请先按当前加密和压缩选项保存该槽位，再执行损坏恢复。", true);
                return;
            }

            if (previous.RecoveredFromBackup)
            {
                SetStatus("当前主存档已损坏", true);
                AppendLog("请先点击保存，将已恢复的数据重新写为主存档，再执行损坏恢复测试。", true);
                return;
            }

            StorageAcceptanceData replacement = new StorageAcceptanceData(
                previous.Data.PlayerName + "（覆盖测试）",
                previous.Data.Level + 1,
                previous.Data.Coins + 1);
            StorageResult overwritten = await storage.SaveAsync(CurrentSlot, replacement, options);
            if (!overwritten.Succeeded)
            {
                ShowFailure("生成备份", overwritten.Reason, overwritten.Message);
                return;
            }

            File.WriteAllBytes(Path.Combine(storage.StorageRootPath, CurrentSlot + ".save"),
                new byte[] { 0, 1, 2, 3 });
            StorageLoadResult<StorageAcceptanceData> recovered =
                await storage.LoadAsync<StorageAcceptanceData>(CurrentSlot, options);
            bool valid = recovered.Succeeded && recovered.RecoveredFromBackup
                && SameData(recovered.Data, previous.Data);
            if (valid)
            {
                playerNameInput.text = recovered.Data.PlayerName;
                levelInput.text = recovered.Data.Level.ToString();
                coinsInput.text = recovered.Data.Coins.ToString();
            }
            SetStatus(valid ? "损坏恢复成功" : "损坏恢复失败", !valid);
            AppendLog(valid
                ? $"已恢复此前保存的数据：{recovered.Data.PlayerName}，等级 {recovered.Data.Level}，金币 {recovered.Data.Coins}。"
                : $"恢复结果：{recovered.Reason} {recovered.Message}", !valid);
        }

        private void DeleteKey()
        {
            InstallSaveKeyProvider provider = storage.ManagedSaveKeyProvider;
            bool deleted = provider != null && provider.DeleteKey(KeyId);
            SetStatus(deleted ? "测试密钥已删除" : "测试密钥不存在或未启用", !deleted);
            AppendLog("删除密钥后，已有加密存档应返回“密钥不可用”。", false);
        }

        private void RecreateKey()
        {
            InstallSaveKeyProvider provider = storage.ManagedSaveKeyProvider;
            bool created = provider != null && provider.EnsureKey(KeyId);
            SetStatus(created ? "已生成新的测试密钥" : "测试密钥已存在", false);
            AppendLog("新密钥不能解密使用旧密钥保存的数据，请重新保存。", false);
        }

        private void Restart()
        {
            if (SceneManager.GetActiveScene().buildIndex != 0)
            {
                SetStatus("软重启要求当前场景位于 Build Settings 的第 0 项", true);
                AppendLog("请先将 StorageAcceptance 设置为第 0 个启用场景。", true);
                return;
            }

            GameEntry.Restart();
        }

        private StorageOptions CreateOptions()
        {
            return new StorageOptions
            {
                Version = 1,
                ProtectionMode = encryptionToggle.isOn
                    ? StorageProtectionMode.EncryptedAndAuthenticated
                    : StorageProtectionMode.None,
                CompressionMode = compressionToggle.isOn
                    ? StorageCompressionMode.GZip
                    : StorageCompressionMode.None,
                KeyId = KeyId,
                CreateBackup = true,
                RecoverFromBackup = true
            };
        }

        private bool TryReadData(out StorageAcceptanceData data)
        {
            data = null;
            if (!int.TryParse(levelInput.text, out int level)
                || !int.TryParse(coinsInput.text, out int coins))
            {
                SetStatus("等级和金币必须填写整数", true);
                return false;
            }

            data = new StorageAcceptanceData(playerNameInput.text, level, coins);
            return true;
        }

        private string CurrentSlot => "sample-slot-" + (slotDropdown.value + 1);

        private void ShowResult(string operation, StorageResult result)
        {
            if (!result.Succeeded)
            {
                ShowFailure(operation, result.Reason, result.Message);
                return;
            }

            SetStatus(operation + "成功", false);
            AppendLog($"{operation} {result.SlotName}：{result.Size} 字节", false);
        }

        private void ShowFailure(string operation, StorageExceptionReason reason, string message)
        {
            SetStatus(operation + "失败：" + ToChinese(reason), true);
            AppendLog(message, true);
        }

        private void SetOperationsInteractable(bool value)
        {
            if (operationButtons == null) return;
            for (int i = 0; i < operationButtons.Length; i++)
                operationButtons[i].interactable = value;
        }

        private void SetStatus(string message, bool error)
        {
            statusText.text = message;
            statusText.color = error ? new Color(1f, 0.45f, 0.38f) : new Color(0.45f, 0.92f, 0.62f);
        }

        private void AppendLog(string message, bool error)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            logLines.Enqueue((error ? "[失败] " : "") + message);
            while (logLines.Count > 8) logLines.Dequeue();
            logText.text = string.Join("\n", logLines.ToArray());
        }

        private static string GetShortTypeName(string typeName)
        {
            int separator = typeName.LastIndexOf('.');
            return separator < 0 ? typeName : typeName.Substring(separator + 1);
        }

        private static string ToChinese(StorageExceptionReason reason)
        {
            switch (reason)
            {
                case StorageExceptionReason.NotFound: return "存档不存在";
                case StorageExceptionReason.KeyUnavailable: return "密钥不可用";
                case StorageExceptionReason.AuthenticationFailed: return "认证失败";
                case StorageExceptionReason.FormatInvalid: return "存档格式损坏";
                case StorageExceptionReason.SerializationFailed: return "序列化失败";
                case StorageExceptionReason.MigrationFailed: return "版本迁移失败";
                case StorageExceptionReason.IoFailure: return "文件读写失败";
                case StorageExceptionReason.Cancelled: return "操作已取消";
                case StorageExceptionReason.InvalidArgument: return "参数无效";
                case StorageExceptionReason.ProtectionModeMismatch: return "加密选项与存档不一致";
                case StorageExceptionReason.CompressionModeMismatch: return "压缩选项与存档不一致";
                default: return reason.ToString();
            }
        }

        private static bool SameData(StorageAcceptanceData left, StorageAcceptanceData right)
        {
            return left != null && right != null
                && left.PlayerName == right.PlayerName
                && left.Level == right.Level
                && left.Coins == right.Coins
                && left.SavedAt == right.SavedAt;
        }
    }

    [Serializable]
    public sealed class StorageAcceptanceData
    {
        public string PlayerName;
        public int Level;
        public int Coins;
        public string SavedAt;

        public StorageAcceptanceData(string playerName, int level, int coins)
        {
            PlayerName = string.IsNullOrWhiteSpace(playerName) ? "未命名玩家" : playerName.Trim();
            Level = level;
            Coins = coins;
            SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
