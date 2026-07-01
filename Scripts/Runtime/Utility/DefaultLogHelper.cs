using System;
using System.IO;
using System.IO.Compression;
using System.Text;

using UnityEngine;

using RFramework;
using System.Diagnostics;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认游戏框架日志辅助器。
    /// 实现 ILogHelper 接口，将日志同时输出到 Unity Console 和本地文件。
    /// 特性：按日期命名文件、超 10MB 自动分卷、过期自动清理、非编辑器构建 GZip 压缩。
    /// </summary>
    public class DefaultLogHelper : RFrameworkLog.ILogHelper
    {
        private const string TimeFormat = "HH:mm:ss";
        private const string FileTimeFormat = "yyyyMMddHHmmss";
        private const int MaxFileSize = 10 * 1024 * 1024;

#if UNITY_STANDALONE
        private const int MaxRetentionDays = 14;
#else
        private const int MaxRetentionDays = 7;
#endif

        private FileStream fileStream;
        private StreamWriter streamWriter;
        private StringBuilder stringBuilder = new StringBuilder();
        private int volumeIndex = 1;
        private string logDirectory;
        private bool isDisposed;

        /// <summary>
        /// 记录日志。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志内容。</param>
        public void Log(RFrameworkLogLevel level, object message)
        {
            switch (level)
            {
                case RFrameworkLogLevel.Info:
                    UnityEngine.Debug.Log(message);
                    break;
                case RFrameworkLogLevel.Warning:
                    UnityEngine.Debug.LogWarning(message);
                    break;
                case RFrameworkLogLevel.Error:
                    UnityEngine.Debug.LogError(message);
                    break;
                default:
                    throw new RFrameworkException(message.ToString());
            }

            WriteToFile(level, message);
        }

        /// <summary>
        /// 初始化日志文件写入器。
        /// </summary>
        /// <param name="writeToFile">是否启用文件写入，false 时仅输出到 Console</param>
        public void Initialize(bool writeToFile = true)
        {
            if (!writeToFile)
            {
                return;
            }

            logDirectory = GetLogDirectory();
            CreateLogFile();

            // 程序启动时清理旧日志文件
            DeleteOldLogFiles();
        }

        /// <summary>
        /// 关闭并释放所有文件资源。
        /// </summary>
        public void Shutdown()
        {
            if (isDisposed || streamWriter == null)
            {
                return;
            }

            isDisposed = true;
            DisposeLogFile();
        }

        /// <summary>
        /// 获取日志文件存储目录。
        /// 独立平台放在 DataPath 同级的 Logs 目录下，移动平台放在 persistentDataPath 下。
        /// </summary>
        private static string GetLogDirectory()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            return Path.Combine(Application.dataPath, "..", "Logs", "RFramework");
#else
            return Path.Combine(Application.persistentDataPath, "Logs", "RFramework");
#endif
        }

        /// <summary>
        /// 将日志内容写入文件。
        /// 通过 StackTrace 提取原始调用位置，跳过 RFrameworkLog / DefaultLogHelper 自身帧。
        /// </summary>
        private void WriteToFile(RFrameworkLogLevel level, object message)
        {
            if (streamWriter == null)
            {
                return;
            }

            stringBuilder.Clear();
            stringBuilder.Append('[');
            stringBuilder.Append(GetLevelTag(level));
            stringBuilder.Append("] [");
            stringBuilder.Append(DateTime.Now.ToString(TimeFormat));
            stringBuilder.Append("] ");
            stringBuilder.Append(message);

            // 通过 StackTrace 提取原始调用位置
            AppendCallerFrame();

            streamWriter.WriteLine(stringBuilder.ToString());

            // Error 级别始终立即写入磁盘，防止崩溃时数据丢失
            if (level == RFrameworkLogLevel.Error)
            {
                streamWriter.Flush();
            }

            CheckVolume();
        }

        /// <summary>
        /// 日志等级转换为文件标签。
        /// </summary>
        private static string GetLevelTag(RFrameworkLogLevel level)
        {
            return level switch
            {
                RFrameworkLogLevel.Info => "Info",
                RFrameworkLogLevel.Warning => "Warning",
                RFrameworkLogLevel.Error => "Error",
                _ => "???"
            };
        }

        /// <summary>
        /// 创建新日志文件。文件名格式：yyyyMMddHHmmss（分卷时追加 -N 后缀）。
        /// </summary>
        private void CreateLogFile()
        {
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            string fileName = DateTime.Now.ToString(FileTimeFormat);
            if (volumeIndex > 1)
            {
                fileName += "-" + volumeIndex;
            }

            string path = Path.Combine(logDirectory, fileName);
            fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            streamWriter = new StreamWriter(fileStream);
        }

        /// <summary>
        /// 释放当前日志文件流。非编辑器构建下将文件内容压缩为 .log.zip 并删除原始文件。
        /// </summary>
        private void DisposeLogFile()
        {
            if (streamWriter == null)
            {
                return;
            }

            string filePath = fileStream.Name;

#if !UNITY_EDITOR
            // 非编辑器构建：压缩原始文件为 zip
            using (MemoryStream memoryStream = new MemoryStream())
            {
                fileStream.Seek(0, SeekOrigin.Begin);
                fileStream.CopyTo(memoryStream);

                streamWriter.Dispose();
                fileStream.Dispose();

                File.Delete(filePath);
                using (FileStream zipStream = new FileStream(filePath + ".log.zip", FileMode.Create, FileAccess.Write))
                {
                    CompressToZip(memoryStream, zipStream);
                }
            }
#else
            streamWriter.Dispose();
            fileStream.Dispose();
#endif

            streamWriter = null;
            fileStream = null;
        }

        /// <summary>
        /// 通过 StackTrace 提取调用 Debug.Log 的原始位置并追加到日志行。
        /// 跳过 UnityEngine.Debug、RFrameworkLog 和 DefaultLogHelper 自身的栈帧。
        /// </summary>
        private void AppendCallerFrame()
        {
            StackTrace stackTrace = new StackTrace(2, true);
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                StackFrame frame = stackTrace.GetFrame(i);
                string typeName = frame.GetMethod().DeclaringType.FullName;

                // 跳过框架内部栈帧
                if (typeName.StartsWith("UnityEngine.Debug") ||
                    typeName.StartsWith("UnityEngine.Logger") ||
                    typeName.Contains("RFrameworkLog") ||
                    typeName.Contains("DefaultLogHelper"))
                {
                    continue;
                }

                stringBuilder.Append("  ");
                stringBuilder.Append(typeName);
                stringBuilder.Append('.');
                stringBuilder.Append(frame.GetMethod().Name);
                stringBuilder.Append('(');

                string fileName = frame.GetFileName();
                if (!string.IsNullOrEmpty(fileName))
                {
                    stringBuilder.Append(Path.GetFileName(fileName));
                    stringBuilder.Append(':');
                    stringBuilder.Append(frame.GetFileLineNumber());
                }

                stringBuilder.Append(')');
                return;
            }
        }

        /// <summary>
        /// 检查当前日志文件是否超过分卷大小阈值，超过则创建新卷。
        /// </summary>
        private void CheckVolume()
        {
            if (fileStream.Length < MaxFileSize)
            {
                return;
            }

            volumeIndex++;
            DisposeLogFile();
            CreateLogFile();
        }

        /// <summary>
        /// 清理超过保留天数的旧日志文件。
        /// </summary>
        private void DeleteOldLogFiles()
        {
            if (!Directory.Exists(logDirectory))
            {
                return;
            }

            string[] files = Directory.GetFiles(logDirectory);
            DateTime now = DateTime.Now;

            foreach (string file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                // 提取文件名中的时间戳部分（分卷名如 20250101120000-2，取 - 前的部分）
                int dashIndex = fileName.IndexOf('-');
                string timePart = dashIndex > 0 ? fileName.Substring(0, dashIndex) : fileName;

                if (DateTime.TryParseExact(timePart, FileTimeFormat,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime fileDate))
                {
                    if (now.Subtract(fileDate).Days > MaxRetentionDays)
                    {
                        File.Delete(file);
                    }
                }
            }
        }

        /// <summary>
        /// 使用 GZip 压缩流数据。
        /// </summary>
        private static void CompressToZip(Stream source, Stream target)
        {
            using (GZipStream compressionStream = new GZipStream(target, CompressionMode.Compress))
            {
                if (source.CanSeek)
                {
                    source.Seek(0, SeekOrigin.Begin);
                }
                source.CopyTo(compressionStream);
            }
        }
    }
}
