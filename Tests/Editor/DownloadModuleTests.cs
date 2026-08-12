using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RFramework;
using UnityEngine.TestTools;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 下载模块的断点续传契约测试。
    /// </summary>
    public sealed class DownloadModuleTests
    {
        private string testDirectory;

        [SetUp]
        public void SetUp()
        {
            testDirectory = Path.Combine(Path.GetTempPath(), "UnityRFrameworkDownloadTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                RFrameworkModuleHost.StopAll();
            }
            catch (RFrameworkException)
            {
                // 测试清理继续执行，具体停止错误由对应测试断言。
            }

            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, true);
            }
        }

        /// <summary>验证已有 .part 文件会通过 Range 请求继续写入并原子提交。</summary>
        [Test]
        public void DownloadAsyncResumesExistingPartialFile()
        {
            string targetPath = Path.Combine(testDirectory, "payload.bin");
            File.WriteAllText(targetPath + ".part", "abc");

            FakeDownloadHelper helper = new FakeDownloadHelper((request, path, append) =>
            {
                Assert.AreEqual("bytes=3-", request.Headers["Range"]);
                Write(path, "def", append);
                return Response(206, "bytes 3-5/6");
            });
            RFrameworkModuleHost.Get<IWebRequestModule>().SetHelper(helper);

            DownloadResult result = RFrameworkModuleHost.Get<IDownloadModule>().DownloadAsync(
                    "https://example.invalid/payload.bin",
                    targetPath,
                    new DownloadOptions { ExpectedSize = 6 })
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual("abcdef", File.ReadAllText(targetPath));
            Assert.IsFalse(File.Exists(targetPath + ".part"));
            Assert.IsTrue(result.Resumed);
            Assert.AreEqual(6, result.FileSize);
        }

        /// <summary>验证服务器忽略 Range 返回 200 时不会把完整响应追加到旧分片。</summary>
        [Test]
        public void DownloadAsyncRestartsWhenServerIgnoresRange()
        {
            string targetPath = Path.Combine(testDirectory, "payload.bin");
            File.WriteAllText(targetPath + ".part", "old");
            int calls = 0;

            FakeDownloadHelper helper = new FakeDownloadHelper((request, path, append) =>
            {
                calls++;
                Write(path, "fresh", append);
                return Response(200, null);
            });
            RFrameworkModuleHost.Get<IWebRequestModule>().SetHelper(helper);

            DownloadResult result = RFrameworkModuleHost.Get<IDownloadModule>().DownloadAsync(
                    "https://example.invalid/payload.bin",
                    targetPath,
                    new DownloadOptions { ExpectedSize = 5 })
                .GetAwaiter()
                .GetResult();

            Assert.AreEqual(2, calls);
            Assert.AreEqual("fresh", File.ReadAllText(targetPath));
            Assert.IsFalse(result.Resumed);
        }

        /// <summary>验证完整请求收到 416 时会明确失败，不会无限重试。</summary>
        [Test]
        public void DownloadAsyncRejectsRangeNotSatisfiableAtZeroOffset()
        {
            string targetPath = Path.Combine(testDirectory, "payload.bin");
            FakeDownloadHelper helper = new FakeDownloadHelper((request, path, append) =>
                Response(416, "bytes */6"));
            RFrameworkModuleHost.Get<IWebRequestModule>().SetHelper(helper);
            using (CancellationTokenSource cts = new CancellationTokenSource(100))
            {
                Assert.Throws<RFrameworkException>(() =>
                    RFrameworkModuleHost.Get<IDownloadModule>().DownloadAsync(
                            "https://example.invalid/payload.bin",
                            targetPath,
                            new DownloadOptions { MaxRetries = 0 },
                            ct: cts.Token)
                        .GetAwaiter()
                        .GetResult());
            }
        }

        /// <summary>验证 ZIP 下载成功后会安全解压并提交目标目录。</summary>
        [UnityTest]
        public IEnumerator DownloadAsyncExtractsZipArchive()
        {
            string targetPath = Path.Combine(testDirectory, "content.zip");
            byte[] archive = CreateZip("folder/data.txt", "payload");
            FakeDownloadHelper helper = new FakeDownloadHelper((request, path, append) =>
            {
                File.WriteAllBytes(path, archive);
                return Response(200, null);
            });
            RFrameworkModuleHost.Get<IWebRequestModule>().SetHelper(helper);

            Task<DownloadResult> task = RFrameworkModuleHost.Get<IDownloadModule>().DownloadAsync(
                "https://example.invalid/content.zip",
                targetPath,
                new DownloadOptions
                {
                    ExpectedSize = archive.Length,
                    ExtractZip = true,
                    ExtractDirectory = Path.Combine(testDirectory, "content")
                });
            yield return WaitForTask(task);
            DownloadResult result = task.GetAwaiter().GetResult();

            Assert.IsTrue(result.Extracted);
            Assert.AreEqual("payload", File.ReadAllText(Path.Combine(testDirectory, "content", "folder", "data.txt")));
        }

        /// <summary>验证 ZIP 条目不能通过 .. 写出目标目录。</summary>
        [UnityTest]
        public IEnumerator DownloadAsyncRejectsZipPathTraversal()
        {
            string targetPath = Path.Combine(testDirectory, "content.zip");
            byte[] archive = CreateZip("../escape.txt", "unsafe");
            FakeDownloadHelper helper = new FakeDownloadHelper((request, path, append) =>
            {
                File.WriteAllBytes(path, archive);
                return Response(200, null);
            });
            RFrameworkModuleHost.Get<IWebRequestModule>().SetHelper(helper);

            Task<DownloadResult> task = RFrameworkModuleHost.Get<IDownloadModule>().DownloadAsync(
                "https://example.invalid/content.zip",
                targetPath,
                new DownloadOptions
                {
                    ExpectedSize = archive.Length,
                    ExtractZip = true,
                    ExtractDirectory = Path.Combine(testDirectory, "content")
                });
            yield return WaitForTask(task);
            Assert.IsTrue(task.IsFaulted);
            Assert.IsInstanceOf<RFrameworkException>(task.Exception?.GetBaseException());
            Assert.IsFalse(File.Exists(Path.Combine(testDirectory, "escape.txt")));
        }

        private static void Write(string path, string value, bool append)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            using (FileStream stream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write))
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private static WebResponse Response(int statusCode, string contentRange)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(contentRange))
            {
                headers["content-range"] = contentRange;
            }

            return new WebResponse(statusCode, string.Empty, headers, null);
        }

        private static byte[] CreateZip(string entryName, string content)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (ZipArchive archive = new ZipArchive(output, ZipArchiveMode.Create, true))
                {
                    ZipArchiveEntry entry = archive.CreateEntry(entryName);
                    using (StreamWriter writer = new StreamWriter(entry.Open(), Encoding.UTF8))
                    {
                        writer.Write(content);
                    }
                }

                return output.ToArray();
            }
        }

        private static IEnumerator WaitForTask(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
        }

        private sealed class FakeDownloadHelper : IWebRequestHelper
        {
            private readonly Func<WebRequestData, string, bool, WebResponse> download;

            public FakeDownloadHelper(Func<WebRequestData, string, bool, WebResponse> download)
            {
                this.download = download;
            }

            public Task<WebResponse> SendAsync(WebRequestData request, IProgress<float> progress, CancellationToken ct)
            {
                throw new NotSupportedException();
            }

            public Task<WebResponse> DownloadFileAsync(
                WebRequestData request,
                string savePath,
                bool append,
                IProgress<WebDownloadProgress> progress,
                CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(download(request, savePath, append));
            }
        }
    }
}
