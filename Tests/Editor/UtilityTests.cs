using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using RFramework;
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

        /// <summary>验证 XOR 支持短数据和非零起始位置。</summary>
        [Test]
        public void EncryptionHandlesShortAndOffsetRanges()
        {
            byte[] shortBytes = { 1, 2, 3 };
            Utility.Encryption.GetQuickSelfXorBytes(shortBytes, new byte[] { 1 });
            CollectionAssert.AreEqual(new byte[] { 0, 3, 2 }, shortBytes);

            byte[] rangeBytes = { 10, 20, 30, 40, 50 };
            Utility.Encryption.GetSelfXorBytes(
                rangeBytes, 2, 2, new byte[] { 1, 2, 3 });
            CollectionAssert.AreEqual(new byte[] { 10, 20, 29, 41, 50 }, rangeBytes);
        }

        /// <summary>验证程序集列表调用方不能改写 Utility 内部缓存。</summary>
        [Test]
        public void AssemblyListIsIsolatedFromCallers()
        {
            System.Reflection.Assembly[] first = Utility.Assembly.GetAssemblies();
            Assert.IsNotEmpty(first);
            System.Reflection.Assembly expected = first[0];
            first[0] = null;

            System.Reflection.Assembly[] second = Utility.Assembly.GetAssemblies();
            Assert.AreSame(expected, second[0]);
        }
    }
}
