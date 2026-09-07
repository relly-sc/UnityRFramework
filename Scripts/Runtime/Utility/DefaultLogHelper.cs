using System;
using System.Globalization;
using System.IO;
using System.Text;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 使用 Unity Console 和本地滚动文本文件输出日志的默认辅助器。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public sealed class DefaultLogHelper : ILogHelper
    {
        private const long MaxFileBytes = 10L * 1024L * 1024L;
        private const int RetentionDays = 7;

        private readonly object gate = new object();
        private StreamWriter writer;
        private long estimatedBytes;
        private int volume;
        private bool fileOutputEnabled = true;
        private bool disposed;

        /// <inheritdoc/>
        public void Write(LogLevel level, string message)
        {
            WriteToConsole(level, message);

            lock (gate)
            {
                if (disposed || !fileOutputEnabled)
                {
                    return;
                }

                try
                {
                    EnsureWriter();
                    string line = string.Format(
                        CultureInfo.InvariantCulture,
                        "[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] {2}",
                        DateTime.Now, level, message);
                    writer.WriteLine(line);
                    estimatedBytes += Encoding.UTF8.GetByteCount(line)
                        + Environment.NewLine.Length;

                    if (level == LogLevel.Error)
                    {
                        writer.Flush();
                    }

                    if (estimatedBytes >= MaxFileBytes)
                    {
                        RotateFile();
                    }
                }
                catch (Exception ex) when (ex is IOException
                    || ex is UnauthorizedAccessException
                    || ex is NotSupportedException)
                {
                    DisableFileOutput(ex);
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                CloseWriter();
            }
        }

        private static void WriteToConsole(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Info:
                    UnityEngine.Debug.Log(message);
                    break;
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(message);
                    break;
                case LogLevel.Error:
                    UnityEngine.Debug.LogError(message);
                    break;
                default:
                    UnityEngine.Debug.Log(message);
                    break;
            }
        }

        private void EnsureWriter()
        {
            if (writer != null)
            {
                return;
            }

            string directory = GetLogDirectory();
            Directory.CreateDirectory(directory);
            DeleteExpiredFiles(directory);

            string suffix = volume == 0 ? string.Empty : $"-{volume}";
            string fileName = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}{suffix}.log";
            FileStream stream = new FileStream(
                Path.Combine(directory, fileName),
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite);
            writer = new StreamWriter(stream, new UTF8Encoding(false));
            estimatedBytes = stream.Length;
        }

        private void RotateFile()
        {
            CloseWriter();
            volume++;
            EnsureWriter();
        }

        private void CloseWriter()
        {
            if (writer == null)
            {
                return;
            }

            try
            {
                writer.Flush();
                writer.Dispose();
            }
            catch (IOException)
            {
                // 应用关闭阶段不因日志文件刷新失败阻塞框架清理。
            }
            finally
            {
                writer = null;
            }
        }

        private void DisableFileOutput(Exception exception)
        {
            fileOutputEnabled = false;
            CloseWriter();
            UnityEngine.Debug.LogWarning(
                $"[UnityRFramework] File logging disabled: {exception.Message}");
        }

        private static string GetLogDirectory()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            return Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Logs"));
#else
            return Path.Combine(Application.persistentDataPath, "Logs"");
#endif
        }

        private static void DeleteExpiredFiles(string directory)
        {
            DateTime threshold = DateTime.UtcNow.AddDays(-RetentionDays);
            foreach (string path in Directory.GetFiles(directory, "*.log"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(path) < threshold)
                    {
                        File.Delete(path);
                    }
                }
                catch (IOException)
                {
                    // 被占用的历史日志留待下次启动清理。
                }
                catch (UnauthorizedAccessException)
                {
                    // 无删除权限时不影响当前日志继续写入。
                }
            }
        }
    }
}
