using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RFramework;
using UnityEditor;
using UnityEngine;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>Storage 核心模块的原子性、保护和版本契约测试。</summary>
    public sealed class StorageModuleTests
    {
        private MemoryStorageHelper helper;
        private IStorageModule module;

        [SetUp]
        public void SetUp()
        {
            helper = new MemoryStorageHelper();
            module = RFrameworkModuleHost.Get<IStorageModule>();
            module.SetHelper(helper);
            module.SetSerializer(new TestSerializer());
        }

        [Test]
        public void FrameworkEntryPrefabsContainExactlyOneStorageComponent()
        {
            string[] guids = AssetDatabase.FindAssets(
                "UnityRFramework t:Prefab",
                new[] { "Assets/UnityRFramework" });
            var frameworkPrefabPaths = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("/UnityRFramework.prefab", StringComparison.Ordinal))
                {
                    continue;
                }

                frameworkPrefabPaths.Add(path);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.NotNull(prefab, path);
                Assert.AreEqual(
                    0,
                    prefab.GetComponents<StorageComponent>().Length,
                    $"StorageComponent 不应挂在框架入口根节点：{path}");
                Transform storageRoot = prefab.transform.Find("Storage");
                Assert.NotNull(
                    storageRoot,
                    $"框架入口 Prefab 缺少 Storage 直接子物体：{path}");
                StorageComponent[] components =
                    storageRoot.GetComponents<StorageComponent>();
                Assert.AreEqual(
                    1,
                    components.Length,
                    $"Storage 子物体必须恰好包含一个 StorageComponent：{path}");
                Assert.AreEqual(
                    1,
                    prefab.GetComponentsInChildren<StorageComponent>(true).Length,
                    $"框架入口 Prefab 中只能存在一个 StorageComponent：{path}");

                var serialized = new SerializedObject(components[0]);
                Assert.AreEqual(
                    "UnityRFramework.Runtime.DefaultStorageHelper",
                    serialized.FindProperty("storageHelperTypeName").stringValue,
                    path);
                Assert.AreEqual(
                    "UnityRFramework.Runtime.JsonStorageSerializer",
                    serialized.FindProperty("storageSerializerTypeName").stringValue,
                    path);
                Assert.AreEqual(
                    "Saves",
                    serialized.FindProperty("storageDirectoryName").stringValue,
                    path);
                Assert.AreEqual(1, serialized.FindProperty("defaultVersion").intValue, path);
                Assert.AreEqual(
                    (int)StorageCompressionMode.None,
                    serialized.FindProperty("defaultCompressionMode").enumValueIndex,
                    path);
                Assert.AreEqual(
                    (int)StorageProtectionMode.None,
                    serialized.FindProperty("defaultProtectionMode").enumValueIndex,
                    path);
                Assert.AreEqual(
                    InstallSaveKeyProvider.DefaultKeyId,
                    serialized.FindProperty("defaultProtectionKeyId").stringValue,
                    path);
                Assert.IsTrue(
                    serialized.FindProperty("automaticallyManageSaveKey").boolValue,
                    path);
                Assert.IsTrue(
                    serialized.FindProperty("createBackupByDefault").boolValue,
                    path);
                Assert.IsTrue(
                    serialized.FindProperty("recoverFromBackupByDefault").boolValue,
                    path);
            }

            Assert.IsNotEmpty(frameworkPrefabPaths, "未找到 UnityRFramework.prefab。");
        }

        [Test]
        public void StorageSampleIsPublishedAndUsesDedicatedData()
        {
            const string root = "Assets/UnityRFramework/Samples/Sample.Storage";
            Assert.IsTrue(AssetDatabase.IsValidFolder(root));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<MonoScript>(
                root + "/Scripts/Runtime/StorageAcceptanceController.cs"));
            Assert.IsTrue(File.ReadAllText("Assets/UnityRFramework/Tools~/SamplesPackage.json")
                .Contains("\"displayName\": \"Sample.Storage\""));

            string source = File.ReadAllText(
                root + "/Scripts/Runtime/StorageAcceptanceController.cs");
            StringAssert.Contains("SaveKey.SampleStorage", source);
            StringAssert.Contains("sample-slot-", source);
        }

        [TearDown]
        public void TearDown()
        {
            RFrameworkModuleHost.StopAll();
        }

        [Test]
        public void SaveLoadDeleteAndListSlotsWork()
        {
            StorageResult saved = Await(module.SaveAsync("slot-a", new SaveData(7, "hello")));
            Assert.IsTrue(Await(module.SaveAsync("slot-b", new SaveData(8, "world"))).Succeeded);
            StorageLoadResult<SaveData> loaded = Await(module.LoadAsync<SaveData>("slot-a"));
            IReadOnlyList<StorageSlotInfo> slots = Await(module.GetSlotsAsync());

            Assert.IsTrue(saved.Succeeded);
            Assert.IsTrue(loaded.Succeeded);
            Assert.AreEqual(7, loaded.Data.Number);
            Assert.AreEqual("hello", loaded.Data.Text);
            Assert.AreEqual(2, slots.Count);
            Assert.IsTrue(Await(module.ExistsAsync("slot-a")));
            Assert.IsTrue(Await(module.DeleteAsync("slot-a")).Succeeded);
            Assert.IsFalse(Await(module.ExistsAsync("slot-a")));
        }

        [Test]
        public void AutomaticSaveUsesReservedSlotAndRejectsManualOverwrite()
        {
            StorageResult automatic = Await(module.SaveAutoAsync(
                new SaveData(9, "automatic")));
            StorageResult manual = Await(module.SaveAsync(
                StorageSlotNames.AutoSave, new SaveData(10, "manual")));
            StorageLoadResult<SaveData> loaded = Await(module.LoadAsync<SaveData>(
                StorageSlotNames.AutoSave));

            Assert.IsTrue(automatic.Succeeded);
            Assert.IsFalse(manual.Succeeded);
            Assert.AreEqual(StorageExceptionReason.InvalidArgument, manual.Reason);
            Assert.IsTrue(loaded.Succeeded);
            Assert.AreEqual(9, loaded.Data.Number);
            Assert.AreEqual("automatic", loaded.Data.Text);
        }

        [Test]
        public void CorruptPrimaryLoadsPreviousBackupWithoutRewritingPrimary()
        {
            Await(module.SaveAsync("slot", new SaveData(1, "old")));
            Await(module.SaveAsync("slot", new SaveData(2, "new")));
            helper.Corrupt("slot");

            StorageLoadResult<SaveData> loaded = Await(module.LoadAsync<SaveData>("slot"));

            Assert.IsTrue(loaded.Succeeded);
            Assert.IsTrue(loaded.RecoveredFromBackup);
            Assert.AreEqual(1, loaded.Data.Number);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2 }, helper.GetCurrent("slot"));
        }

        [TestCase(false, 10)]
        [TestCase(true, 10)]
        [TestCase(false, -2)]
        [TestCase(true, -2)]
        public void BusinessInvalidPrimaryCanReadBackupWithoutChangingEitherFile(bool protect, int backupNumber)
        {
            module.SetDataProtector(new DefaultDataProtector(new TestKeyProvider("SaveKey", CreateKey(7))));
            var options = new StorageOptions
            {
                ProtectionMode = protect ? StorageProtectionMode.EncryptedAndAuthenticated : StorageProtectionMode.None,
                CompressionMode = protect ? StorageCompressionMode.GZip : StorageCompressionMode.None
            };
            Assert.IsTrue(Await(module.SaveAsync("business", new SaveData(backupNumber, "backup"), options)).Succeeded);
            Assert.IsTrue(Await(module.SaveAsync("business", new SaveData(-1, "primary"), options)).Succeeded);
            byte[] primaryBytes = (byte[])helper.GetCurrent("business").Clone();
            byte[] backupBytes = (byte[])helper.GetBackup("business").Clone();

            var primary = Await(module.LoadAsync<SaveData>("business", options));
            Assert.IsTrue(primary.Succeeded, primary.Message);
            Assert.IsFalse(primary.RecoveredFromBackup);
            Assert.That(primary.Data.Number, Is.LessThan(0), "框架可读不代表业务数据合法。");
            Assert.AreEqual(0, helper.BackupReads);

            var backup = Await(module.LoadBackupAsync<SaveData>("business", options));
            Assert.IsTrue(backup.Succeeded, backup.Message);
            Assert.IsTrue(backup.RecoveredFromBackup);
            Assert.AreEqual("business", backup.SlotName);
            Assert.AreEqual(backupNumber, backup.Data.Number);
            var liveData = new SaveData(99, "live");
            if (backup.Data.Number >= 0) liveData = backup.Data;
            Assert.AreEqual(backupNumber >= 0 ? backupNumber : 99, liveData.Number,
                "业务校验失败时应保留当前对局，不自动采用非法备份。");
            CollectionAssert.AreEqual(primaryBytes, helper.GetCurrent("business"));
            CollectionAssert.AreEqual(backupBytes, helper.GetBackup("business"));
        }

        [Test]
        public void ExplicitBackupUsesFullPipelineAndMigrationEvenWhenAutomaticRecoveryIsDisabled()
        {
            module.SetDataProtector(new DefaultDataProtector(new TestKeyProvider("SaveKey", CreateKey(17))));
            var options = new StorageOptions
            {
                ProtectionMode = StorageProtectionMode.EncryptedAndAuthenticated,
                CompressionMode = StorageCompressionMode.GZip,
                RecoverFromBackup = false
            };
            Await(module.SaveAsync("migration-backup", new SaveData(4, "old"), options));
            Await(module.SaveAsync("migration-backup", new SaveData(9, "current"), options));
            byte[] primary = (byte[])helper.GetCurrent("migration-backup").Clone();
            byte[] previous = (byte[])helper.GetBackup("migration-backup").Clone();
            options.Version = 2;

            var result = Await(module.LoadBackupAsync("migration-backup", options, new IncrementMigration()));

            Assert.IsTrue(result.Succeeded, result.Message);
            Assert.IsTrue(result.RecoveredFromBackup);
            Assert.AreEqual(5, result.Data.Number);
            Assert.AreEqual(2, result.Version);
            Assert.AreEqual(0, helper.PrimaryReads);
            CollectionAssert.AreEqual(primary, helper.GetCurrent("migration-backup"));
            CollectionAssert.AreEqual(previous, helper.GetBackup("migration-backup"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NewerPrimaryIsRejectedBeforeDeserializationAndNeverFallsBack(bool serializerFails)
        {
            Await(module.SaveAsync("future", new SaveData(1, "backup")));
            Await(module.SaveAsync("future", new SaveData(2, "future"), new StorageOptions { Version = 2 }));
            var serializer = new TestSerializer();
            module.SetSerializer(serializerFails ? (IStorageSerializer)new FailingSerializer() : serializer);
            byte[] primary = (byte[])helper.GetCurrent("future").Clone();
            byte[] backup = (byte[])helper.GetBackup("future").Clone();

            var result = Await(module.LoadAsync<SaveData>("future"));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(StorageExceptionReason.MigrationFailed, result.Reason);
            StringAssert.Contains("newer", result.Message);
            Assert.AreEqual(0, serializer.DeserializeCalls);
            Assert.AreEqual(0, helper.BackupReads);
            CollectionAssert.AreEqual(primary, helper.GetCurrent("future"));
            CollectionAssert.AreEqual(backup, helper.GetBackup("future"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MigrationFailureDoesNotFallBackToCompatibleBackup(bool hasMigration)
        {
            Await(module.SaveAsync("migration-failure", new SaveData(8, "backup"), new StorageOptions { Version = 2 }));
            Await(module.SaveAsync("migration-failure", new SaveData(9, "primary")));
            byte[] primary = (byte[])helper.GetCurrent("migration-failure").Clone();
            byte[] backup = (byte[])helper.GetBackup("migration-failure").Clone();

            var result = Await(module.LoadAsync("migration-failure", new StorageOptions { Version = 2 },
                hasMigration ? new FailingMigration() : (IStorageMigration<SaveData>)null));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(StorageExceptionReason.MigrationFailed, result.Reason);
            Assert.AreEqual(0, helper.BackupReads);
            CollectionAssert.AreEqual(primary, helper.GetCurrent("migration-failure"));
            CollectionAssert.AreEqual(backup, helper.GetBackup("migration-failure"));
        }

        [Test]
        public void UnavailablePrimaryKeyDoesNotSilentlyUseBackupWithAnOlderAvailableKey()
        {
            var options = new StorageOptions { ProtectionMode = StorageProtectionMode.EncryptedAndAuthenticated };
            var oldProtector = new DefaultDataProtector(new TestKeyProvider("SaveKey", CreateKey(3)));
            module.SetDataProtector(oldProtector);
            Await(module.SaveAsync("key-change", new SaveData(1, "backup"), options));
            module.SetDataProtector(new DefaultDataProtector(new TestKeyProvider("NewKey", CreateKey(5))));
            options.KeyId = "NewKey";
            Await(module.SaveAsync("key-change", new SaveData(2, "primary"), options));
            byte[] primary = (byte[])helper.GetCurrent("key-change").Clone();
            byte[] backup = (byte[])helper.GetBackup("key-change").Clone();
            module.SetDataProtector(oldProtector);

            var result = Await(module.LoadAsync<SaveData>("key-change", options));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(StorageExceptionReason.KeyUnavailable, result.Reason);
            Assert.AreEqual(0, helper.BackupReads);
            CollectionAssert.AreEqual(primary, helper.GetCurrent("key-change"));
            CollectionAssert.AreEqual(backup, helper.GetBackup("key-change"));
            Assert.IsTrue(Await(module.LoadBackupAsync<SaveData>("key-change", options)).Succeeded);
        }

        [TestCase(StorageExceptionReason.NotFound)]
        [TestCase(StorageExceptionReason.FormatInvalid)]
        [TestCase(StorageExceptionReason.SerializationFailed)]
        [TestCase(StorageExceptionReason.IoFailure)]
        public void BackupFailureDoesNotReadPrimaryOrReplaceLiveData(StorageExceptionReason reason)
        {
            Await(module.SaveAsync("backup-failure", new SaveData(1, "backup")));
            if (reason != StorageExceptionReason.NotFound)
                Await(module.SaveAsync("backup-failure", new SaveData(2, "primary")));
            if (reason == StorageExceptionReason.FormatInvalid) helper.GetBackup("backup-failure")[0] = 0;
            if (reason == StorageExceptionReason.SerializationFailed) module.SetSerializer(new FailingSerializer());
            helper.FailBackupReads = reason == StorageExceptionReason.IoFailure;
            byte[] primary = (byte[])helper.GetCurrent("backup-failure").Clone();
            byte[] backup = reason == StorageExceptionReason.NotFound ? null : (byte[])helper.GetBackup("backup-failure").Clone();
            var liveData = new SaveData(99, "live");

            var result = Await(module.LoadBackupAsync<SaveData>("backup-failure"));
            if (result.Succeeded) liveData = result.Data;

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(reason, result.Reason);
            Assert.IsNull(result.Data);
            Assert.IsFalse(result.RecoveredFromBackup);
            Assert.AreEqual(99, liveData.Number);
            Assert.AreEqual(0, helper.PrimaryReads);
            CollectionAssert.AreEqual(primary, helper.GetCurrent("backup-failure"));
            if (backup != null) CollectionAssert.AreEqual(backup, helper.GetBackup("backup-failure"));
        }

        [Test]
        public void BackupSharesSaveLockAndWaitingCancellationDoesNotTouchFiles()
        {
            Await(module.SaveAsync("locked", new SaveData(1, "backup")));
            Await(module.SaveAsync("locked", new SaveData(2, "primary")));
            byte[] primary = (byte[])helper.GetCurrent("locked").Clone();
            byte[] backup = (byte[])helper.GetBackup("locked").Clone();
            helper.WriteDelayMilliseconds = 5000;
            using (var saveCancellation = new CancellationTokenSource())
            using (var loadCancellation = new CancellationTokenSource())
            {
                var save = module.SaveAsync("locked", new SaveData(3, "pending"), ct: saveCancellation.Token);
                try
                {
                    Assert.IsTrue(SpinWait.SpinUntil(() => helper.ActiveWrites > 0, TimeSpan.FromSeconds(1)));
                    var load = module.LoadBackupAsync<SaveData>("locked", ct: loadCancellation.Token);
                    Assert.IsFalse(load.IsCompleted, "备份读取必须等待同槽位的写锁。");
                    loadCancellation.Cancel();
                    Assert.AreEqual(StorageExceptionReason.Cancelled, Await(load).Reason);
                    Assert.AreEqual(0, helper.BackupReads);
                }
                finally
                {
                    saveCancellation.Cancel();
                    Await(save);
                }
            }
            CollectionAssert.AreEqual(primary, helper.GetCurrent("locked"));
            CollectionAssert.AreEqual(backup, helper.GetBackup("locked"));
            Assert.IsTrue(Await(module.LoadBackupAsync<SaveData>("locked")).Succeeded, "取消后必须释放槽位锁。");
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void BackupReadCancellationAndShutdownAreContained(bool stopModule, bool explicitBackup)
        {
            Await(module.SaveAsync("cancel-backup", new SaveData(1, "backup")));
            Await(module.SaveAsync("cancel-backup", new SaveData(2, "primary")));
            if (!explicitBackup) helper.Corrupt("cancel-backup");
            byte[] primary = (byte[])helper.GetCurrent("cancel-backup").Clone();
            byte[] backup = (byte[])helper.GetBackup("cancel-backup").Clone();
            helper.BackupReadDelayMilliseconds = 5000;
            using (var cancellation = new CancellationTokenSource())
            {
                var load = explicitBackup
                    ? module.LoadBackupAsync<SaveData>("cancel-backup", ct: cancellation.Token)
                    : module.LoadAsync<SaveData>("cancel-backup", ct: cancellation.Token);
                Assert.AreEqual(1, helper.BackupReads);
                if (stopModule) RFrameworkModuleHost.StopAll();
                else cancellation.Cancel();
                var result = Await(load);
                Assert.IsFalse(result.Succeeded);
                Assert.AreEqual(StorageExceptionReason.Cancelled, result.Reason);
            }
            CollectionAssert.AreEqual(primary, helper.GetCurrent("cancel-backup"));
            CollectionAssert.AreEqual(backup, helper.GetBackup("cancel-backup"));
        }

        [TestCase(null)]
        [TestCase("../slot")]
        public void BackupRejectsInvalidSlotWithoutReading(string slotName)
        {
            var result = Await(module.LoadBackupAsync<SaveData>(slotName));
            Assert.AreEqual(StorageExceptionReason.InvalidArgument, result.Reason);
            Assert.AreEqual(0, helper.BackupReads);
        }

        [Test]
        public void EncryptedPrimaryDoesNotFallBackToPlaintextBackupWhenProtectionIsDisabled()
        {
            var keys = new TestKeyProvider("SaveKey", CreateKey(9));
            module.SetDataProtector(new DefaultDataProtector(keys));
            var plain = new StorageOptions
            {
                ProtectionMode = StorageProtectionMode.None,
                CreateBackup = true,
                RecoverFromBackup = true
            };
            var encrypted = new StorageOptions
            {
                ProtectionMode = StorageProtectionMode.EncryptedAndAuthenticated,
                KeyId = "SaveKey",
                CreateBackup = true,
                RecoverFromBackup = true
            };

            Assert.IsTrue(Await(module.SaveAsync(
                "mode-change", new SaveData(1, "plaintext"), plain)).Succeeded);
            Assert.IsTrue(Await(module.SaveAsync(
                "mode-change", new SaveData(2, "encrypted"), encrypted)).Succeeded);

            StorageLoadResult<SaveData> result = Await(
                module.LoadAsync<SaveData>("mode-change", plain));

            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(result.RecoveredFromBackup);
            Assert.AreEqual(StorageExceptionReason.ProtectionModeMismatch, result.Reason);
        }

        [Test]
        public void CorruptEncryptedPrimaryLoadsEncryptedBackup()
        {
            var keys = new TestKeyProvider("SaveKey", CreateKey(13));
            module.SetDataProtector(new DefaultDataProtector(keys));
            var options = new StorageOptions
            {
                ProtectionMode = StorageProtectionMode.EncryptedAndAuthenticated,
                KeyId = "SaveKey",
                CreateBackup = true,
                RecoverFromBackup = true
            };

            Assert.IsTrue(Await(module.SaveAsync(
                "encrypted-backup", new SaveData(3, "old"), options)).Succeeded);
            Assert.IsTrue(Await(module.SaveAsync(
                "encrypted-backup", new SaveData(4, "new"), options)).Succeeded);
            helper.Corrupt("encrypted-backup");

            StorageLoadResult<SaveData> result = Await(
                module.LoadAsync<SaveData>("encrypted-backup", options));

            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(result.RecoveredFromBackup);
            Assert.AreEqual(3, result.Data.Number);
        }

        [Test]
        public void CompressedPrimaryDoesNotFallBackToUncompressedBackupWithWrongOptions()
        {
            var plain = new StorageOptions { CreateBackup = true, RecoverFromBackup = true };
            var compressed = new StorageOptions
            {
                CompressionMode = StorageCompressionMode.GZip,
                CreateBackup = true,
                RecoverFromBackup = true
            };
            Assert.IsTrue(Await(module.SaveAsync(
                "compression-change", new SaveData(1, "plain"), plain)).Succeeded);
            Assert.IsTrue(Await(module.SaveAsync(
                "compression-change", new SaveData(2, "compressed"), compressed)).Succeeded);

            StorageLoadResult<SaveData> result = Await(
                module.LoadAsync<SaveData>("compression-change", plain));

            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(result.RecoveredFromBackup);
            Assert.AreEqual(StorageExceptionReason.CompressionModeMismatch, result.Reason);
        }

        [Test]
        public void EncryptedCompressionMismatchDoesNotFallBackToPreviousBackup()
        {
            var keys = new TestKeyProvider("SaveKey", CreateKey(15));
            module.SetDataProtector(new DefaultDataProtector(keys));
            var uncompressed = new StorageOptions
            {
                ProtectionMode = StorageProtectionMode.EncryptedAndAuthenticated,
                KeyId = "SaveKey",
                CreateBackup = true,
                RecoverFromBackup = true
            };
            var compressed = new StorageOptions
            {
                ProtectionMode = StorageProtectionMode.EncryptedAndAuthenticated,
                CompressionMode = StorageCompressionMode.GZip,
                KeyId = "SaveKey",
                CreateBackup = true,
                RecoverFromBackup = true
            };
            Assert.IsTrue(Await(module.SaveAsync(
                "protected-compression-change", new SaveData(1, "old"), uncompressed)).Succeeded);
            Assert.IsTrue(Await(module.SaveAsync(
                "protected-compression-change", new SaveData(2, "new"), compressed)).Succeeded);

            StorageLoadResult<SaveData> result = Await(
                module.LoadAsync<SaveData>("protected-compression-change", uncompressed));

            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(result.RecoveredFromBackup);
            Assert.AreEqual(StorageExceptionReason.CompressionModeMismatch, result.Reason);
        }

        [Test]
        public void EncryptionAndCompressionRoundTripAndRejectTampering()
        {
            var keys = new TestKeyProvider("save-key", CreateKey(11));
            module.SetDataProtector(new DefaultDataProtector(keys));
            var options = new StorageOptions
            {
                ProtectionMode = StorageProtectionMode.EncryptedAndAuthenticated,
                CompressionMode = StorageCompressionMode.GZip,
                KeyId = "save-key",
                CreateBackup = false,
                RecoverFromBackup = false
            };

            StorageResult saved = Await(module.SaveAsync(
                "secure", new SaveData(42, "hidden-value"), options));
            byte[] stored = helper.GetCurrent("secure");
            StorageLoadResult<SaveData> loaded =
                Await(module.LoadAsync<SaveData>("secure", options));

            Assert.IsTrue(saved.Succeeded);
            Assert.IsFalse(Encoding.UTF8.GetString(stored).Contains("hidden-value"));
            Assert.IsTrue(loaded.Succeeded);
            Assert.AreEqual(42, loaded.Data.Number);

            stored[stored.Length / 2] ^= 0x40;
            StorageLoadResult<SaveData> tampered =
                Await(module.LoadAsync<SaveData>("secure", options));
            Assert.IsFalse(tampered.Succeeded);
            Assert.AreEqual(StorageExceptionReason.AuthenticationFailed, tampered.Reason);
        }

        [Test]
        public void MissingProtectorReturnsKeyUnavailable()
        {
            StorageResult result = Await(module.SaveAsync(
                "secure",
                new SaveData(1, "value"),
                new StorageOptions
                {
                    ProtectionMode = StorageProtectionMode.EncryptedAndAuthenticated
                }));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(StorageExceptionReason.KeyUnavailable, result.Reason);
        }

        [Test]
        public void DeletedSaveKeyReturnsKeyUnavailableWithoutReplacingSave()
        {
            var keys = new TestKeyProvider("SaveKey", CreateKey(19));
            module.SetDataProtector(new DefaultDataProtector(keys));
            var options = new StorageOptions
            {
                ProtectionMode = StorageProtectionMode.EncryptedAndAuthenticated,
                KeyId = "SaveKey",
                CreateBackup = false,
                RecoverFromBackup = false
            };
            Assert.IsTrue(Await(module.SaveAsync(
                "missing-key", new SaveData(5, "protected"), options)).Succeeded);
            byte[] previous = (byte[])helper.GetCurrent("missing-key").Clone();

            keys.Available = false;
            StorageLoadResult<SaveData> result = Await(
                module.LoadAsync<SaveData>("missing-key", options));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(StorageExceptionReason.KeyUnavailable, result.Reason);
            CollectionAssert.AreEqual(previous, helper.GetCurrent("missing-key"));
        }

        [Test]
        public void OlderSaveRequiresAndUsesMigration()
        {
            Await(module.SaveAsync(
                "versioned", new SaveData(4, "old"),
                new StorageOptions { Version = 1 }));
            var target = new StorageOptions { Version = 2 };

            StorageLoadResult<SaveData> withoutMigration =
                Await(module.LoadAsync<SaveData>("versioned", target));
            StorageLoadResult<SaveData> migrated =
                Await(module.LoadAsync("versioned", target, new IncrementMigration()));

            Assert.AreEqual(StorageExceptionReason.MigrationFailed, withoutMigration.Reason);
            Assert.IsTrue(migrated.Succeeded);
            Assert.AreEqual(5, migrated.Data.Number);
            Assert.AreEqual(2, migrated.Version);
        }

        [Test]
        public void SameSlotWritesAreSerialized()
        {
            helper.WriteDelayMilliseconds = 20;
            Task<StorageResult>[] writes = new Task<StorageResult>[5];
            for (int i = 0; i < writes.Length; i++)
            {
                writes[i] = module.SaveAsync("same", new SaveData(i, "value"));
            }

            Task.WhenAll(writes).GetAwaiter().GetResult();

            Assert.AreEqual(1, helper.MaximumConcurrentWrites);
        }

        [Test]
        public void FrameworkStopCancelsPendingSaveBeforeCommit()
        {
            Assert.IsTrue(Await(module.SaveAsync(
                "pending", new SaveData(1, "old"))).Succeeded);
            byte[] previous = (byte[])helper.GetCurrent("pending").Clone();
            helper.WriteDelayMilliseconds = 500;
            Task<StorageResult> save = module.SaveAsync(
                "pending", new SaveData(2, "new"));
            Assert.IsTrue(SpinWait.SpinUntil(
                () => helper.ActiveWrites > 0,
                TimeSpan.FromSeconds(1)));

            RFrameworkModuleHost.StopAll();
            StorageResult result = Await(save);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(StorageExceptionReason.Cancelled, result.Reason);
            CollectionAssert.AreEqual(previous, helper.GetCurrent("pending"));
        }

        [Test]
        public void SerializerAndFileFailuresAreContained()
        {
            module.SetSerializer(new FailingSerializer());
            StorageResult serialization = Await(module.SaveAsync(
                "serialization", new SaveData(1, "value")));
            module.SetSerializer(new TestSerializer());
            helper.FailWrites = true;
            StorageResult file = Await(module.SaveAsync(
                "file", new SaveData(1, "value")));

            Assert.AreEqual(StorageExceptionReason.SerializationFailed, serialization.Reason);
            Assert.AreEqual(StorageExceptionReason.IoFailure, file.Reason);
            Assert.NotNull(RFrameworkModuleHost.Get<IPoolModule>());
        }

        [Test]
        public void ModuleRestartCanReadExistingSave()
        {
            Assert.IsTrue(Await(module.SaveAsync(
                "restart", new SaveData(9, "persistent"))).Succeeded);
            RFrameworkModuleHost.StopAll();

            module = RFrameworkModuleHost.Get<IStorageModule>();
            module.SetHelper(helper);
            module.SetSerializer(new TestSerializer());
            StorageLoadResult<SaveData> loaded = Await(
                module.LoadAsync<SaveData>("restart"));

            Assert.IsTrue(loaded.Succeeded);
            Assert.AreEqual(9, loaded.Data.Number);
            Assert.AreEqual("persistent", loaded.Data.Text);
        }

        [Test]
        public void DefaultFileHelperCreatesAndReadsBackup()
        {
            string root = Path.Combine(Path.GetTempPath(), "UnityRFrameworkStorageTests", Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject("StorageHelperTest");
            try
            {
                DefaultStorageHelper fileHelper = gameObject.AddComponent<DefaultStorageHelper>();
                fileHelper.Initialize(root);
                Await(fileHelper.WriteAtomicAsync("slot", new byte[] { 1 }, true, CancellationToken.None));
                Await(fileHelper.WriteAtomicAsync("slot", new byte[] { 2 }, true, CancellationToken.None));

                CollectionAssert.AreEqual(
                    new byte[] { 2 }, Await(fileHelper.ReadAsync("slot", false, CancellationToken.None)));
                CollectionAssert.AreEqual(
                    new byte[] { 1 }, Await(fileHelper.ReadAsync("slot", true, CancellationToken.None)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ComponentReadsRealBackupWithDefaultOrEncryptedOptionsWithoutChangingFiles(bool protect)
        {
            string root = Path.Combine(Path.GetTempPath(), "UnityRFrameworkStorageTests", Guid.NewGuid().ToString("N"));
            var gameObject = new GameObject("BackupReadTest");
            gameObject.SetActive(false);
            try
            {
                var fileHelper = gameObject.AddComponent<DefaultStorageHelper>();
                fileHelper.Initialize(root);
                module.SetHelper(fileHelper);
                Utility.Json.SetJsonHelper(new DefaultJsonHelper());
                module.SetSerializer(new JsonStorageSerializer());
                module.SetDataProtector(new DefaultDataProtector(new TestKeyProvider("SaveKey", CreateKey(23))));
                var options = new StorageOptions
                {
                    ProtectionMode = protect ? StorageProtectionMode.EncryptedAndAuthenticated : StorageProtectionMode.None,
                    CompressionMode = protect ? StorageCompressionMode.GZip : StorageCompressionMode.None,
                    RecoverFromBackup = false
                };
                Assert.IsTrue(Await(module.SaveAutoAsync(new SaveData(4, "backup"), options)).Succeeded);
                Assert.IsTrue(Await(module.SaveAutoAsync(new SaveData(-1, "primary"), options)).Succeeded);
                string primaryPath = Path.Combine(root, StorageSlotNames.AutoSave + ".save");
                byte[] primary = File.ReadAllBytes(primaryPath);
                byte[] backup = File.ReadAllBytes(primaryPath + ".bak");
                var component = gameObject.AddComponent<StorageComponent>();
                options.Version = 2;

                var result = protect
                    ? Await(component.LoadBackupAsync(StorageSlotNames.AutoSave, options, new IncrementMigration()))
                    : Await(component.LoadBackupAsync<SaveData>(StorageSlotNames.AutoSave));

                Assert.IsTrue(result.Succeeded, result.Message);
                Assert.IsTrue(result.RecoveredFromBackup);
                Assert.AreEqual(protect ? 5 : 4, result.Data.Number);
                Assert.AreEqual(protect ? 2 : 1, result.Version);
                CollectionAssert.AreEqual(primary, File.ReadAllBytes(primaryPath));
                CollectionAssert.AreEqual(backup, File.ReadAllBytes(primaryPath + ".bak"));
                Assert.IsFalse(File.Exists(primaryPath + ".tmp"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Serializable]
        private sealed class SaveData
        {
            public int Number;
            public string Text;

            public SaveData(int number, string text)
            {
                Number = number;
                Text = text;
            }
        }

        private sealed class TestSerializer : IStorageSerializer
        {
            public int DeserializeCalls { get; private set; }

            public byte[] Serialize<T>(T data)
            {
                SaveData value = data as SaveData;
                if (value == null) throw new InvalidOperationException();
                return Encoding.UTF8.GetBytes(value.Number + "\n" + value.Text);
            }

            public T Deserialize<T>(byte[] data)
            {
                DeserializeCalls++;
                string[] parts = Encoding.UTF8.GetString(data).Split(new[] { '\n' }, 2);
                return (T)(object)new SaveData(int.Parse(parts[0]), parts[1]);
            }
        }

        private sealed class IncrementMigration : IStorageMigration<SaveData>
        {
            public SaveData Migrate(SaveData data, int fromVersion, int targetVersion)
            {
                data.Number++;
                return data;
            }
        }

        private sealed class FailingSerializer : IStorageSerializer
        {
            public byte[] Serialize<T>(T data)
            {
                throw new InvalidOperationException("Expected serializer failure.");
            }

            public T Deserialize<T>(byte[] data)
            {
                throw new InvalidOperationException("Expected serializer failure.");
            }
        }

        private sealed class FailingMigration : IStorageMigration<SaveData>
        {
            public SaveData Migrate(SaveData data, int fromVersion, int targetVersion)
            {
                throw new InvalidOperationException("Expected migration failure.");
            }
        }

        private sealed class TestKeyProvider : IKeyProvider
        {
            private readonly string keyId;
            private readonly byte[] key;

            public bool Available { get; set; } = true;

            public TestKeyProvider(string keyId, byte[] key)
            {
                this.keyId = keyId;
                this.key = key;
            }

            public bool TryGetKey(string requestedKeyId, out byte[] result)
            {
                if (!Available || requestedKeyId != keyId)
                {
                    result = null;
                    return false;
                }
                result = (byte[])key.Clone();
                return true;
            }
        }

        private sealed class MemoryStorageHelper : IStorageHelper
        {
            private readonly Dictionary<string, byte[]> current = new Dictionary<string, byte[]>();
            private readonly Dictionary<string, byte[]> backups = new Dictionary<string, byte[]>();
            private int concurrentWrites;

            public int WriteDelayMilliseconds { get; set; }
            public bool FailWrites { get; set; }
            public bool FailBackupReads { get; set; }
            public int BackupReadDelayMilliseconds { get; set; }
            public int PrimaryReads { get; private set; }
            public int BackupReads { get; private set; }
            public int MaximumConcurrentWrites { get; private set; }
            public int ActiveWrites => Volatile.Read(ref concurrentWrites);

            public async Task WriteAtomicAsync(
                string slotName, byte[] data, bool createBackup, CancellationToken ct)
            {
                int active = Interlocked.Increment(ref concurrentWrites);
                MaximumConcurrentWrites = Math.Max(MaximumConcurrentWrites, active);
                try
                {
                    if (WriteDelayMilliseconds > 0)
                        await Task.Delay(WriteDelayMilliseconds, ct).ConfigureAwait(false);
                    if (FailWrites) throw new IOException("Expected file failure.");
                    if (createBackup && current.TryGetValue(slotName, out byte[] old))
                        backups[slotName] = (byte[])old.Clone();
                    current[slotName] = (byte[])data.Clone();
                }
                finally
                {
                    Interlocked.Decrement(ref concurrentWrites);
                }
            }

            public async Task<byte[]> ReadAsync(string slotName, bool backup, CancellationToken ct)
            {
                if (backup) BackupReads++;
                else PrimaryReads++;
                if (backup && BackupReadDelayMilliseconds > 0)
                    await Task.Delay(BackupReadDelayMilliseconds, ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                if (backup && FailBackupReads) throw new IOException("Expected backup read failure.");
                Dictionary<string, byte[]> source = backup ? backups : current;
                if (!source.TryGetValue(slotName, out byte[] data))
                    throw new FileNotFoundException();
                return (byte[])data.Clone();
            }

            public Task DeleteAsync(string slotName, CancellationToken ct)
            {
                current.Remove(slotName);
                backups.Remove(slotName);
                return Task.CompletedTask;
            }

            public Task<bool> ExistsAsync(string slotName, CancellationToken ct)
            {
                return Task.FromResult(current.ContainsKey(slotName));
            }

            public Task<IReadOnlyList<StorageSlotInfo>> GetSlotsAsync(CancellationToken ct)
            {
                var result = new List<StorageSlotInfo>();
                foreach (KeyValuePair<string, byte[]> pair in current)
                {
                    result.Add(new StorageSlotInfo(
                        pair.Key, pair.Value.Length, DateTime.UtcNow,
                        backups.ContainsKey(pair.Key)));
                }
                return Task.FromResult<IReadOnlyList<StorageSlotInfo>>(result);
            }

            public void Corrupt(string slotName)
            {
                current[slotName] = new byte[] { 0, 1, 2 };
            }

            public byte[] GetCurrent(string slotName)
            {
                return current[slotName];
            }

            public byte[] GetBackup(string slotName)
            {
                return backups[slotName];
            }

        }

        private static byte[] CreateKey(byte seed)
        {
            byte[] key = new byte[32];
            for (int i = 0; i < key.Length; i++) key[i] = (byte)(seed + i);
            return key;
        }

        private static T Await<T>(Task<T> task)
        {
            return task.GetAwaiter().GetResult();
        }

        private static void Await(Task task)
        {
            task.GetAwaiter().GetResult();
        }
    }
}
