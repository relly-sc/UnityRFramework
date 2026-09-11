using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using RFramework;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 基于 SharpZipLib 的 ZIP 解压辅助器。
    /// 支持普通 ZIP、Zip64 以及 SharpZipLib 可读取的传统/AES 加密 ZIP。
    /// </summary>
    public sealed class SharpZipLibArchiveHelper : IArchiveHelper
    {
        private const int BufferSize = 1024 * 1024;

        /// <summary>
        /// 获取或设置默认 ZIP 密码。单次任务传入的密码优先于该值。
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 使用无密码配置创建辅助器。
        /// </summary>
        public SharpZipLibArchiveHelper()
        {
        }

        /// <summary>
        /// 使用指定 ZIP 密码创建辅助器。
        /// </summary>
        /// <param name="password">ZIP 密码。</param>
        public SharpZipLibArchiveHelper(string password)
        {
            Password = password;
        }

        /// <inheritdoc />
        public Task ExtractAsync(
            string archivePath,
            string destinationDirectory,
            ArchiveExtractionOptions options,
            IProgress<ArchiveProgress> progress = null,
            CancellationToken ct = default)
        {
            ValidateArguments(archivePath, destinationDirectory, options);
            if (options.Format != ArchiveFormat.Auto && options.Format != ArchiveFormat.Zip)
            {
                throw new RFrameworkException(
                    $"SharpZipLibArchiveHelper: archive format '{options.Format}' is not supported.");
            }

            string password = string.IsNullOrEmpty(options.Password)
                ? Password
                : options.Password;
            return Task.Run(
                () => ExtractCore(
                    archivePath,
                    destinationDirectory,
                    options,
                    progress,
                    password,
                    ct),
                ct);
        }

        private static void ExtractCore(
            string archivePath,
            string destinationDirectory,
            ArchiveExtractionOptions options,
            IProgress<ArchiveProgress> progress,
            string password,
            CancellationToken ct)
        {
            Directory.CreateDirectory(destinationDirectory);
            string root = EnsureTrailingSeparator(Path.GetFullPath(destinationDirectory));
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            using (FileStream archiveStream = new FileStream(
                archivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.SequentialScan))
            using (ZipFile archive = new ZipFile(archiveStream))
            {
                archive.IsStreamOwner = false;
                if (!string.IsNullOrEmpty(password))
                {
                    archive.Password = password;
                }

                int totalEntries = checked((int)archive.Count);
                if (options.MaxEntries > 0 && totalEntries > options.MaxEntries)
                {
                    throw new RFrameworkException(
                        $"SharpZipLibArchiveHelper: ZIP contains too many entries ({totalEntries}).");
                }

                long totalBytes = CalculateTotalBytes(archive, options.MaxExtractedBytes);
                long processedBytes = 0L;
                int processedEntries = 0;
                byte[] buffer = new byte[BufferSize];

                foreach (ZipEntry entry in archive)
                {
                    ct.ThrowIfCancellationRequested();
                    string entryPath = GetSafeEntryPath(root, entry.Name, comparison);
                    if (entry.IsDirectory)
                    {
                        Directory.CreateDirectory(entryPath);
                        processedEntries++;
                        progress?.Report(new ArchiveProgress(
                            processedBytes, totalBytes, processedEntries, totalEntries, entry.Name));
                        continue;
                    }

                    if (!entry.IsFile)
                    {
                        processedEntries++;
                        progress?.Report(new ArchiveProgress(
                            processedBytes, totalBytes, processedEntries, totalEntries, entry.Name));
                        continue;
                    }

                    string parent = Path.GetDirectoryName(entryPath);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    using (Stream input = archive.GetInputStream(entry))
                    using (FileStream output = new FileStream(
                        entryPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        BufferSize,
                        FileOptions.SequentialScan))
                    {
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            output.Write(buffer, 0, read);
                            processedBytes = checked(processedBytes + read);
                            if (options.MaxExtractedBytes > 0
                                && processedBytes > options.MaxExtractedBytes)
                            {
                                throw new RFrameworkException(
                                    "SharpZipLibArchiveHelper: ZIP extracted size exceeds the configured limit.");
                            }

                            progress?.Report(new ArchiveProgress(
                                processedBytes, totalBytes, processedEntries, totalEntries, entry.Name));
                        }
                    }

                    processedEntries++;
                    progress?.Report(new ArchiveProgress(
                        processedBytes, totalBytes, processedEntries, totalEntries, entry.Name));
                }
            }
        }

        private static long CalculateTotalBytes(ZipFile archive, long maximumBytes)
        {
            long total = 0L;
            foreach (ZipEntry entry in archive)
            {
                if (!entry.IsFile || entry.Size < 0L)
                {
                    continue;
                }

                total = checked(total + entry.Size);
                if (maximumBytes > 0L && total > maximumBytes)
                {
                    throw new RFrameworkException(
                        "SharpZipLibArchiveHelper: ZIP extracted size exceeds the configured limit.");
                }
            }

            return total;
        }

        private static string GetSafeEntryPath(
            string root,
            string entryName,
            StringComparison comparison)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                throw new RFrameworkException("SharpZipLibArchiveHelper: ZIP entry name is empty.");
            }

            string normalized = entryName
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            if (normalized.IndexOf(':') >= 0)
            {
                throw new RFrameworkException(
                    $"SharpZipLibArchiveHelper: unsafe ZIP entry path '{entryName}'.");
            }

            string entryPath = Path.GetFullPath(Path.Combine(root, normalized));
            if (!entryPath.StartsWith(root, comparison))
            {
                throw new RFrameworkException(
                    $"SharpZipLibArchiveHelper: unsafe ZIP entry path '{entryName}'.");
            }

            return entryPath;
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static void ValidateArguments(
            string archivePath,
            string destinationDirectory,
            ArchiveExtractionOptions options)
        {
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            {
                throw new RFrameworkException("SharpZipLibArchiveHelper: archive file does not exist.");
            }

            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                throw new RFrameworkException(
                    "SharpZipLibArchiveHelper: destination directory is invalid.");
            }

            if (options == null)
            {
                throw new RFrameworkException("SharpZipLibArchiveHelper: options cannot be null.");
            }

            if (options.MaxEntries < 0 || options.MaxExtractedBytes < 0L)
            {
                throw new RFrameworkException(
                    "SharpZipLibArchiveHelper: extraction limits cannot be negative.");
            }
        }
    }
}
