using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using RFramework;
using UnityEngine;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// Library Utility 基础行为测试。
    /// </summary>
    public sealed class UtilityTests
    {
        /// <summary>验证框架 CRC32 使用标准测试向量。</summary>
        [Test]
        public void Crc32UsesStandardVector()
        {
            byte[] bytes = Encoding.ASCII.GetBytes("123456789");
            uint expected = 0xCBF43926u;

            Assert.AreEqual(expected, unchecked((uint)Utility.Verifier.GetCrc32(bytes)));
            Assert.AreEqual(expected, BinaryFormatUtility.ComputeCrc32(bytes));
        }

        /// <summary>验证 CRC32 的共享实现可安全并发调用。</summary>
        [Test]
        public void Crc32SupportsConcurrentCalls()
        {
            byte[] bytes = Encoding.ASCII.GetBytes("123456789");
            int[] results = new int[64];

            Parallel.For(0, results.Length, index =>
                results[index] = Utility.Verifier.GetCrc32(bytes));

            int expected = unchecked((int)0xCBF43926u);
            for (int i = 0; i < results.Length; i++)
            {
                Assert.AreEqual(expected, results[i]);
            }
        }

        /// <summary>验证数组片段、流和大端字节输出使用同一 CRC32 约定。</summary>
        [Test]
        public void Crc32OverloadsProduceTheSameValue()
        {
            byte[] vector = Encoding.ASCII.GetBytes("123456789");
            byte[] source = Encoding.ASCII.GetBytes("xx123456789yy");
            int checksum = Utility.Verifier.GetCrc32(source, 2, vector.Length);

            using (MemoryStream stream = new MemoryStream(vector))
            {
                Assert.AreEqual(checksum, Utility.Verifier.GetCrc32(stream));
            }

            CollectionAssert.AreEqual(
                new byte[] { 0xCB, 0xF4, 0x39, 0x26 },
                Utility.Verifier.GetCrc32Bytes(checksum));
        }

        /// <summary>验证 JSON Helper 配置错误在设置阶段立即失败。</summary>
        [Test]
        public void JsonRejectsNullHelper()
        {
            try
            {
                Assert.Throws<RFrameworkException>(() => Utility.Json.SetJsonHelper(null));
            }
            finally
            {
                Utility.Json.SetJsonHelper(new DefaultJsonHelper());
            }
        }

        /// <summary>验证加密数据可解密，且相同明文每次产生不同密文。</summary>
        [Test]
        public void EncryptionRoundTripUsesRandomIv()
        {
            byte[] source = Encoding.UTF8.GetBytes("UnityRFramework 加密测试");
            byte[] key = Utility.Encryption.CreateKey();

            byte[] first = Utility.Encryption.Encrypt(source, key);
            byte[] second = Utility.Encryption.Encrypt(source, key);

            CollectionAssert.AreNotEqual(first, second);
            CollectionAssert.AreEqual(source, Utility.Encryption.Decrypt(first, key));
            CollectionAssert.AreEqual(source, Utility.Encryption.Decrypt(second, key));
        }

        /// <summary>验证密文被修改或密钥错误时拒绝解密。</summary>
        [Test]
        public void EncryptionRejectsTamperingAndWrongKey()
        {
            byte[] key = Utility.Encryption.CreateKey();
            byte[] encrypted = Utility.Encryption.Encrypt(
                Encoding.UTF8.GetBytes("protected"), key);
            byte[] wrongKey = Utility.Encryption.CreateKey();

            Assert.Throws<RFrameworkException>(() =>
                Utility.Encryption.Decrypt(encrypted, wrongKey));

            encrypted[encrypted.Length / 2] ^= 0x40;

            Assert.Throws<RFrameworkException>(() =>
                Utility.Encryption.Decrypt(encrypted, key));
        }

        /// <summary>验证数据保护封装使用随机密文，并绑定密钥、负载类型和调用上下文。</summary>
        [Test]
        public void DataProtectorRoundTripBindsProtectionContext()
        {
            byte[] key = Utility.Encryption.CreateKey();
            IDataProtector protector = new DefaultDataProtector(
                new TestKeyProvider("content-v1", key));
            byte[] source = Encoding.UTF8.GetBytes("protected config");
            byte[] context = Encoding.UTF8.GetBytes("Config/Items.bytes");

            byte[] first = protector.Protect(
                source, ProtectedDataPayloadKind.Config, "content-v1", context);
            byte[] second = protector.Protect(
                source, ProtectedDataPayloadKind.Config, "content-v1", context);

            CollectionAssert.AreNotEqual(first, second);
            CollectionAssert.AreEqual(
                source,
                protector.Unprotect(first, ProtectedDataPayloadKind.Config, context));
            Assert.Throws<RFrameworkException>(() => protector.Unprotect(
                first,
                ProtectedDataPayloadKind.Storage,
                context));
            Assert.Throws<RFrameworkException>(() => protector.Unprotect(
                first,
                ProtectedDataPayloadKind.Config,
                Encoding.UTF8.GetBytes("Config/Other.bytes")));
        }

        /// <summary>验证封装头、密文或认证标签被修改时均拒绝还原。</summary>
        [Test]
        public void DataProtectorRejectsTamperedData()
        {
            byte[] key = Utility.Encryption.CreateKey();
            IDataProtector protector = new DefaultDataProtector(
                new TestKeyProvider("content-v1", key));
            byte[] protectedData = protector.Protect(
                Encoding.UTF8.GetBytes("tamper check"),
                ProtectedDataPayloadKind.Config,
                "content-v1");
            for (int i = 0; i < protectedData.Length; i++)
            {
                byte[] tampered = (byte[])protectedData.Clone();
                tampered[i] ^= 0x40;
                Assert.Throws<RFrameworkException>(() => protector.Unprotect(
                    tampered,
                    ProtectedDataPayloadKind.Config));
            }

            byte[] truncated = new byte[protectedData.Length - 1];
            Buffer.BlockCopy(protectedData, 0, truncated, 0, truncated.Length);
            Assert.Throws<RFrameworkException>(() => protector.Unprotect(
                truncated,
                ProtectedDataPayloadKind.Config));
        }

        /// <summary>验证未知密钥、空数据和大数据具有明确且稳定的处理结果。</summary>
        [Test]
        public void DataProtectorHandlesKeyAndPayloadBoundaries()
        {
            byte[] key = Utility.Encryption.CreateKey();
            IDataProtector protector = new DefaultDataProtector(
                new TestKeyProvider("content-v1", key));

            byte[] empty = protector.Protect(
                Array.Empty<byte>(),
                ProtectedDataPayloadKind.Custom,
                "content-v1");
            CollectionAssert.IsEmpty(protector.Unprotect(
                empty,
                ProtectedDataPayloadKind.Custom));

            byte[] large = new byte[1024 * 1024];
            for (int i = 0; i < large.Length; i++)
            {
                large[i] = (byte)(i % 251);
            }

            byte[] protectedLarge = protector.Protect(
                large,
                ProtectedDataPayloadKind.Storage,
                "content-v1");
            CollectionAssert.AreEqual(
                large,
                protector.Unprotect(protectedLarge, ProtectedDataPayloadKind.Storage));

            IDataProtector missingKeyProtector = new DefaultDataProtector(
                new TestKeyProvider(null, null));
            Assert.Throws<RFrameworkException>(() => missingKeyProtector.Unprotect(
                protectedLarge,
                ProtectedDataPayloadKind.Storage));

            IDataProtector wrongKeyProtector = new DefaultDataProtector(
                new TestKeyProvider("content-v1", Utility.Encryption.CreateKey()));
            Assert.Throws<RFrameworkException>(() => wrongKeyProtector.Unprotect(
                protectedLarge,
                ProtectedDataPayloadKind.Storage));
        }

        /// <summary>验证密钥轮换后仍可按封装中的 KeyId 读取旧数据。</summary>
        [Test]
        public void DataProtectorResolvesEmbeddedKeyIdAfterRotation()
        {
            byte[] oldKey = Utility.Encryption.CreateKey();
            byte[] currentKey = Utility.Encryption.CreateKey();
            IDataProtector protector = new DefaultDataProtector(
                new TestKeyProvider("save-v1", oldKey, "save-v2", currentKey));
            byte[] source = Encoding.UTF8.GetBytes("rotating save key");

            byte[] oldData = protector.Protect(
                source,
                ProtectedDataPayloadKind.Storage,
                "save-v1");
            byte[] currentData = protector.Protect(
                source,
                ProtectedDataPayloadKind.Storage,
                "save-v2");

            CollectionAssert.AreEqual(
                source,
                protector.Unprotect(oldData, ProtectedDataPayloadKind.Storage));
            CollectionAssert.AreEqual(
                source,
                protector.Unprotect(currentData, ProtectedDataPayloadKind.Storage));
        }

        /// <summary>验证本地路径可转换为标准文件 URI，嵌套空目录可递归清理。</summary>
        [Test]
        public void PathUtilityUsesStandardFileUriAndRemovesEmptyTree()
        {
            string root = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "UnityRFrameworkUtilityTest",
                Guid.NewGuid().ToString("N"));
            string nested = System.IO.Path.Combine(root, "A", "B");

            try
            {
                Directory.CreateDirectory(nested);

                string remotePath = Utility.Path.GetRemotePath(
                    System.IO.Path.Combine(root, "config file.json"));

                Assert.IsTrue(Uri.TryCreate(remotePath, UriKind.Absolute, out Uri uri));
                Assert.AreEqual(Uri.UriSchemeFile, uri.Scheme);
                StringAssert.Contains("%20", remotePath);
                Assert.IsTrue(Utility.Path.RemoveEmptyDirectory(root));
                Assert.IsFalse(Directory.Exists(root));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private sealed class TestKeyProvider : IKeyProvider
        {
            private readonly string keyId;
            private readonly byte[] key;
            private readonly string secondaryKeyId;
            private readonly byte[] secondaryKey;

            public TestKeyProvider(string keyId, byte[] key)
                : this(keyId, key, null, null)
            {
            }

            public TestKeyProvider(
                string keyId,
                byte[] key,
                string secondaryKeyId,
                byte[] secondaryKey)
            {
                this.keyId = keyId;
                this.key = key;
                this.secondaryKeyId = secondaryKeyId;
                this.secondaryKey = secondaryKey;
            }

            public bool TryGetKey(string requestedKeyId, out byte[] resolvedKey)
            {
                if (key != null && string.Equals(
                        keyId,
                        requestedKeyId,
                        StringComparison.Ordinal))
                {
                    resolvedKey = (byte[])key.Clone();
                    return true;
                }

                if (secondaryKey != null && string.Equals(
                        secondaryKeyId,
                        requestedKeyId,
                        StringComparison.Ordinal))
                {
                    resolvedKey = (byte[])secondaryKey.Clone();
                    return true;
                }

                resolvedKey = null;
                return false;
            }
        }

        /// <summary>验证相同种子生成相同随机序列。</summary>
        [Test]
        public void RandomSeedProducesRepeatableSequence()
        {
            Utility.Random.SetSeed(9274);
            int first = Utility.Random.GetRandom();
            int second = Utility.Random.GetRandom(10, 100);

            Utility.Random.SetSeed(9274);
            Assert.AreEqual(first, Utility.Random.GetRandom());
            Assert.AreEqual(second, Utility.Random.GetRandom(10, 100));
        }

        /// <summary>验证字符串读取按 CRLF 前进，并保留空行。</summary>
        [Test]
        public void StringReadLinePreservesEmptyLines()
        {
            const string source = "first\r\n\r\nthird";
            int position = 0;

            Assert.AreEqual("first", source.ReadLine(ref position));
            Assert.AreEqual(string.Empty, source.ReadLine(ref position));
            Assert.AreEqual("third", source.ReadLine(ref position));
            Assert.IsNull(source.ReadLine(ref position));
        }

        /// <summary>验证 Unity 扩展使用原生组件、层级和 Transform API。</summary>
        [Test]
        public void UnityExtensionsModifySceneObjects()
        {
            GameObject root = new GameObject("UtilityRoot");
            GameObject child = new GameObject("UtilityChild");
            child.transform.SetParent(root.transform);

            try
            {
                Assert.AreSame(root.AddComponent<BoxCollider>(), root.GetOrAddComponent<BoxCollider>());
                Assert.IsTrue(root.InScene());

                root.SetLayerRecursively(3);
                Assert.AreEqual(3, root.layer);
                Assert.AreEqual(3, child.layer);

                root.transform.localPosition = new Vector3(1f, 2f, 3f);
                root.transform.SetLocalPositionX(8f);
                root.transform.AddLocalPositionZ(4f);
                Assert.AreEqual(new Vector3(8f, 2f, 7f), root.transform.localPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证可按完整类型名查找当前应用域已加载类型。</summary>
        [Test]
        public void AssemblyUtilityFindsLoadedType()
        {
            Assert.AreSame(typeof(UtilityTests),
                Utility.Assembly.GetType(typeof(UtilityTests).FullName));
        }
    }
}
