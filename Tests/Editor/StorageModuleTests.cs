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
            Assert.IsTrue(File.ReadAllText("Assets/UnityRFramework/Package.json")
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
            public byte[] Serialize<T>(T data)
            {
                SaveData value = data as SaveData;
                if (value == null) throw new InvalidOperationException();
                return Encoding.UTF8.GetBytes(value.Number + "\n" + value.Text);
            }

            public T Deserialize<T>(byte[] data)
            {
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

            public Task<byte[]> ReadAsync(string slotName, bool backup, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                Dictionary<string, byte[]> source = backup ? backups : current;
                if (!source.TryGetValue(slotName, out byte[] data))
                    throw new FileNotFoundException();
                return Task.FromResult((byte[])data.Clone());
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
