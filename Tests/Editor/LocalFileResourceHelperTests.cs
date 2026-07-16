using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityRFramework.Runtime;
using Object = UnityEngine.Object;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// LocalFileResourceHelper 的本地覆盖、随包文件与 Resources 兜底测试。
    /// </summary>
    public sealed class LocalFileResourceHelperTests
    {
        private const string TestRoot = "UnityRFrameworkLocalFileTests";

        private GameObject owner;
        private LocalFileResourceHelper helper;

        /// <summary>创建测试 Helper 并清理上次遗留文件。</summary>
        [SetUp]
        public void SetUp()
        {
            CleanupFiles();
            owner = new GameObject("LocalFileResourceHelper Tests");
            helper = owner.AddComponent<LocalFileResourceHelper>();
        }

        /// <summary>销毁测试 Helper 并删除临时文件。</summary>
        [TearDown]
        public void TearDown()
        {
            if (helper != null)
            {
                helper.Destroy();
            }

            if (owner != null)
            {
                Object.DestroyImmediate(owner);
            }

            CleanupFiles();
        }

        /// <summary>验证 persistentDataPath 中的同名文件优先于 StreamingAssets。</summary>
        [UnityTest]
        public IEnumerator PersistentBytesOverrideStreamingAssets()
        {
            const string location = TestRoot + "/override.bytes";
            WriteFile(GetStreamingPath(location), Encoding.UTF8.GetBytes("streaming"));
            WriteFile(GetPersistentPath(location), Encoding.UTF8.GetBytes("persistent"));

            yield return InitializeHelper();
            Task<object> loadTask = helper.LoadAssetAsync(location, typeof(byte[]), 0);
            yield return WaitTask(loadTask);

            Assert.AreEqual("persistent", Encoding.UTF8.GetString((byte[])loadTask.Result));
            helper.ReleaseAsset(location, typeof(byte[]));
        }

        /// <summary>验证没有 persistent 覆盖文件时读取 StreamingAssets。</summary>
        [UnityTest]
        public IEnumerator StreamingBytesLoadWhenOverrideIsMissing()
        {
            const string location = TestRoot + "/streaming.json";
            WriteFile(GetStreamingPath(location), Encoding.UTF8.GetBytes("{\"source\":\"streaming\"}"));

            yield return InitializeHelper();
            Task<object> loadTask = helper.LoadAssetAsync(location, typeof(string), 0);
            yield return WaitTask(loadTask);

            Assert.AreEqual("{\"source\":\"streaming\"}", (string)loadTask.Result);
            helper.ReleaseAsset(location, typeof(string));
        }

        /// <summary>验证 persistentDataPath 图片可以解码为 Texture2D。</summary>
        [UnityTest]
        public IEnumerator PersistentImageLoadsAsTexture()
        {
            const string location = TestRoot + "/image.png";
            Texture2D source = new Texture2D(2, 3, TextureFormat.RGBA32, false);
            source.SetPixel(0, 0, Color.red);
            source.Apply();
            WriteFile(GetPersistentPath(location), source.EncodeToPNG());
            Object.DestroyImmediate(source);

            yield return InitializeHelper();
            Task<object> loadTask = helper.LoadAssetAsync(location, typeof(Texture2D), 0);
            yield return WaitTask(loadTask);

            Texture2D loaded = (Texture2D)loadTask.Result;
            Assert.AreEqual(2, loaded.width);
            Assert.AreEqual(3, loaded.height);
            helper.ReleaseAsset(location, typeof(Texture2D));
        }

        /// <summary>验证 AudioModule 使用的 object 请求可按 WAV 扩展名推断为 AudioClip。</summary>
        [UnityTest]
        public IEnumerator PersistentWavLoadsAsAudioClip()
        {
            const string location = TestRoot + "/voice.wav";
            WriteFile(GetPersistentPath(location), CreateSilentWav());

            yield return InitializeHelper();
            Task<object> loadTask = helper.LoadAssetAsync(location, typeof(object), 0);
            yield return WaitTask(loadTask);

            AudioClip clip = (AudioClip)loadTask.Result;
            Assert.Greater(clip.samples, 0);
            Assert.AreEqual(1, clip.channels);
            helper.ReleaseAsset(location, typeof(object));
        }

        /// <summary>验证 DefaultResourceHelper 可把 Resources TextAsset 转为原始字节。</summary>
        [Test]
        public void ResourcesFallbackSupportsByteArray()
        {
            DefaultResourceHelper defaultHelper = owner.AddComponent<DefaultResourceHelper>();
            defaultHelper.InitializeAsync(null, default, null, null).GetAwaiter().GetResult();
            const string location =
                "ConfigPipelineAcceptance/Config/Json/Acceptance_Action.json";

            byte[] bytes = (byte[])defaultHelper.LoadAssetSync(location, typeof(byte[]));

            Assert.IsNotNull(bytes);
            StringAssert.Contains("Tables", Encoding.UTF8.GetString(bytes));
            defaultHelper.ReleaseAsset(location, typeof(byte[]));
            defaultHelper.Destroy();
        }

        /// <summary>验证视频 URL 优先指向 persistentDataPath 覆盖文件。</summary>
        [Test]
        public void AssetUrlPrefersPersistentFile()
        {
            const string location = TestRoot + "/video.mp4";
            WriteFile(GetPersistentPath(location), new byte[] { 1, 2, 3 });

            Assert.AreEqual(GetPersistentPath(location), helper.GetAssetUrl(location));
        }

        private IEnumerator InitializeHelper()
        {
            Task task = helper.InitializeAsync(null, default, null, null);
            yield return WaitTask(task);
        }

        private static IEnumerator WaitTask(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.InnerException ?? task.Exception;
            }

            if (task.IsCanceled)
            {
                throw new OperationCanceledException();
            }
        }

        private static string GetPersistentPath(string location)
        {
            return Path.Combine(Application.persistentDataPath,
                location.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string GetStreamingPath(string location)
        {
            return Path.Combine(Application.streamingAssetsPath,
                location.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void WriteFile(string path, byte[] bytes)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, bytes);
        }

        private static void CleanupFiles()
        {
            DeleteDirectory(Path.Combine(Application.persistentDataPath, TestRoot));
            DeleteDirectory(Path.Combine(Application.streamingAssetsPath, TestRoot));
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static byte[] CreateSilentWav()
        {
            const int sampleRate = 8000;
            const int sampleCount = 800;
            const short channelCount = 1;
            const short bitsPerSample = 16;
            int dataLength = sampleCount * channelCount * bitsPerSample / 8;
            using (MemoryStream stream = new MemoryStream(44 + dataLength))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataLength);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channelCount);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channelCount * bitsPerSample / 8);
                writer.Write((short)(channelCount * bitsPerSample / 8));
                writer.Write(bitsPerSample);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataLength);
                writer.Write(new byte[dataLength]);
                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
