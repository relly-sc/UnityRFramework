using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>存档模块的 Unity 入口组件。</summary>
    [AddComponentMenu("UnityRFramework/Storage")]
    [DisallowMultipleComponent]
    public sealed class StorageComponent : UnityRFrameworkComponent
    {
        private const string DefaultHelperTypeName =
            "UnityRFramework.Runtime.DefaultStorageHelper";
        private const string DefaultSerializerTypeName =
            "UnityRFramework.Runtime.JsonStorageSerializer";

        [SerializeField]
        [Tooltip("存档文件辅助器类型全名。")]
        private string storageHelperTypeName = DefaultHelperTypeName;

        [SerializeField]
        [Tooltip("存档序列化器类型全名。")]
        private string storageSerializerTypeName = DefaultSerializerTypeName;

        [SerializeField]
        [Tooltip("Application.persistentDataPath 下的存档子目录。")]
        private string storageDirectoryName = "Saves";

        [SerializeField]
        [Min(1)]
        [Tooltip("默认业务存档版本。")]
        private int defaultVersion = 1;

        [SerializeField]
        [Tooltip("默认存档压缩方式。")]
        private StorageCompressionMode defaultCompressionMode = StorageCompressionMode.None;

        [SerializeField]
        [Tooltip("默认数据保护方式；启用后必须在保存或加载前注入 IDataProtector。")]
        private StorageProtectionMode defaultProtectionMode = StorageProtectionMode.None;

        [SerializeField]
        [Tooltip("默认存档密钥标识。轮换时应使用 SaveKey.版本号，并在迁移完成前保留旧密钥。")]
        private string defaultProtectionKeyId = InstallSaveKeyProvider.DefaultKeyId;

        [SerializeField]
        [Tooltip("启用存档保护时自动创建安装级 SaveKey。关闭后由项目注入密钥提供器。")]
        private bool automaticallyManageSaveKey = true;

        [SerializeField]
        [Tooltip("默认是否保留上一份存档备份。")]
        private bool createBackupByDefault = true;

        [SerializeField]
        [Tooltip("主存档损坏时默认是否尝试读取备份。")]
        private bool recoverFromBackupByDefault = true;

        private IStorageModule storageModule;

        /// <summary>由组件自动创建或项目注入密钥仓后建立的安装级存档密钥提供器。</summary>
        public InstallSaveKeyProvider ManagedSaveKeyProvider { get; private set; }

        /// <summary>当前存档根目录。</summary>
        public string StorageRootPath { get; private set; }

        /// <summary>当前自动管理的安装级密钥仓类型；未启用时为空。</summary>
        public string ManagedSaveKeyStoreTypeName { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            storageModule = RFrameworkModuleHost.Get<IStorageModule>();
            InstallHelper();
            InstallSerializer();
            InstallDefaultDataProtection();
        }

        /// <summary>替换平台文件系统辅助器。</summary>
        public void SetHelper(IStorageHelper helper)
        {
            storageModule.SetHelper(helper);
        }

        /// <summary>替换存档序列化器。</summary>
        public void SetSerializer(IStorageSerializer serializer)
        {
            storageModule.SetSerializer(serializer);
        }

        /// <summary>注入或移除存档数据保护器。</summary>
        public void SetDataProtector(IDataProtector protector)
        {
            ManagedSaveKeyProvider = null;
            ManagedSaveKeyStoreTypeName = string.Empty;
            GetStorageModule().SetDataProtector(protector);
        }

        /// <summary>使用指定密钥仓确保安装级 SaveKey，并立即接入默认数据保护器。</summary>
        public void SetManagedSaveKeyStore(
            IKeyStore keyStore,
            string keyId = InstallSaveKeyProvider.DefaultKeyId)
        {
            if (keyStore == null) throw new ArgumentNullException(nameof(keyStore));

            var provider = new InstallSaveKeyProvider(keyStore);
            provider.EnsureKey(keyId);
            ManagedSaveKeyProvider = provider;
            ManagedSaveKeyStoreTypeName = keyStore.GetType().FullName;
            defaultProtectionKeyId = keyId;
            GetStorageModule().SetDataProtector(new DefaultDataProtector(provider));
        }

        /// <summary>保存槽位；未提供选项时使用 Inspector 默认值。</summary>
        public Task<StorageResult> SaveAsync<T>(
            string slotName, T data, StorageOptions options = null,
            CancellationToken ct = default(CancellationToken))
        {
            return storageModule.SaveAsync(slotName, data, options ?? CreateDefaultOptions(), ct);
        }

        /// <summary>加载槽位；未提供选项时使用 Inspector 默认值。</summary>
        public Task<StorageLoadResult<T>> LoadAsync<T>(
            string slotName, StorageOptions options = null,
            IStorageMigration<T> migration = null,
            CancellationToken ct = default(CancellationToken))
        {
            return storageModule.LoadAsync(
                slotName, options ?? CreateDefaultOptions(), migration, ct);
        }

        /// <summary>删除槽位及其备份。</summary>
        public Task<StorageResult> DeleteAsync(
            string slotName, CancellationToken ct = default(CancellationToken))
        {
            return storageModule.DeleteAsync(slotName, ct);
        }

        /// <summary>检查槽位是否存在。</summary>
        public Task<bool> ExistsAsync(
            string slotName, CancellationToken ct = default(CancellationToken))
        {
            return storageModule.ExistsAsync(slotName, ct);
        }

        /// <summary>列举现有槽位。</summary>
        public Task<IReadOnlyList<StorageSlotInfo>> GetSlotsAsync(
            CancellationToken ct = default(CancellationToken))
        {
            return storageModule.GetSlotsAsync(ct);
        }

        private StorageOptions CreateDefaultOptions()
        {
            return new StorageOptions
            {
                Version = Math.Max(1, defaultVersion),
                CompressionMode = defaultCompressionMode,
                ProtectionMode = defaultProtectionMode,
                KeyId = string.IsNullOrWhiteSpace(defaultProtectionKeyId)
                    ? InstallSaveKeyProvider.DefaultKeyId
                    : defaultProtectionKeyId.Trim(),
                CreateBackup = createBackupByDefault,
                RecoverFromBackup = recoverFromBackupByDefault
            };
        }

        private void InstallDefaultDataProtection()
        {
            if (defaultProtectionMode != StorageProtectionMode.EncryptedAndAuthenticated
                || !automaticallyManageSaveKey)
            {
                return;
            }

            string keyId = string.IsNullOrWhiteSpace(defaultProtectionKeyId)
                ? InstallSaveKeyProvider.DefaultKeyId
                : defaultProtectionKeyId.Trim();
            SetManagedSaveKeyStore(CreateAutomaticKeyStore(), keyId);
        }

        private static IKeyStore CreateAutomaticKeyStore()
        {
            string directoryName = Application.isEditor ? "EditorKeys" : "Keys";
            string rootDirectory = Path.Combine(
                Application.persistentDataPath,
                "UnityRFramework",
                directoryName);

            return InstallSaveKeyStoreRegistry.Create(rootDirectory);
        }

        private IStorageModule GetStorageModule()
        {
            return storageModule ?? (storageModule = RFrameworkModuleHost.Get<IStorageModule>());
        }

        private void InstallHelper()
        {
            if (string.IsNullOrWhiteSpace(storageHelperTypeName))
                storageHelperTypeName = DefaultHelperTypeName;

            StorageHelperBase helper = ComponentFactory.Create<StorageHelperBase>(
                storageHelperTypeName, null);
            if (helper == null)
            {
                throw new RFrameworkException(
                    $"Storage helper '{storageHelperTypeName}' is missing or invalid.");
            }

            helper.name = helper.GetType().Name + " (Storage Helper)";
            helper.transform.SetParent(transform);
            string directory = string.IsNullOrWhiteSpace(storageDirectoryName)
                ? "Saves"
                : storageDirectoryName.Trim();
            if (Path.IsPathRooted(directory))
            {
                throw new RFrameworkException("Storage directory name must be a relative child path.");
            }

            string persistentRoot = Path.GetFullPath(Application.persistentDataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            StorageRootPath = Path.GetFullPath(Path.Combine(persistentRoot, directory));
            if (!StorageRootPath.StartsWith(persistentRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new RFrameworkException(
                    "Storage directory must remain inside Application.persistentDataPath.");
            }
            helper.Initialize(StorageRootPath);
            storageModule.SetHelper(helper);
        }

        private void InstallSerializer()
        {
            if (string.IsNullOrWhiteSpace(storageSerializerTypeName))
                storageSerializerTypeName = DefaultSerializerTypeName;

            Type type = Utility.Assembly.GetType(storageSerializerTypeName);
            if (type == null || !typeof(IStorageSerializer).IsAssignableFrom(type))
            {
                throw new RFrameworkException(
                    $"Storage serializer '{storageSerializerTypeName}' is missing or invalid.");
            }

            try
            {
                storageModule.SetSerializer((IStorageSerializer)Activator.CreateInstance(type));
            }
            catch (Exception exception)
            {
                throw new RFrameworkException(
                    $"Storage serializer '{storageSerializerTypeName}' could not be created.",
                    exception);
            }
        }
    }
}
