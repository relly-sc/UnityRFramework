using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 基于 SharpCompress 的多格式压缩文件解压辅助器。
    /// 支持 ZIP、RAR、7z、TAR、GZip 与 BZip2，并拒绝符号链接和越界路径。
    /// </summary>
    public sealed class SharpCompressArchiveHelper : IArchiveHelper
    {
        private const int BufferSize = 1024 * 1024;

        /// <inheritdoc />
        public Task ExtractAsync(
            string archivePath,
            string destinationDirectory,
            ArchiveExtractionOptions options,
            IProgress<ArchiveProgress> progress = null,
            CancellationToken ct = default)
        {
            ValidateArguments(archivePath, destinationDirectory, options);
            return Task.Run(
                () => ExtractCore(archivePath, destinationDirectory, options, progress, ct),
                ct);
        }

        private static void ExtractCore(
            string archivePath,
            string destinationDirectory,
            ArchiveExtractionOptions options,
            IProgress<ArchiveProgress> progress,
            CancellationToken ct)
        {
            Directory.CreateDirectory(destinationDirectory);
            string root = EnsureTrailingSeparator(Path.GetFullPath(destinationDirectory));
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            ReaderOptions readerOptions = new ReaderOptions
            {
                Password = options.Password,
                ExtensionHint = GetExtensionHint(options.Format)
            };

            if (options.Format == ArchiveFormat.BZip2)
            {
                ExtractBZip2(
                    archivePath, destinationDirectory, options, progress, ct);
                return;
            }

            ArchiveType? detected;
            if (ArchiveFactory.IsArchive(archivePath, readerOptions, out detected))
            {
                ValidateDetectedFormat(detected.Value, options.Format);
                if (detected == ArchiveType.Rar)
                {
                    // RAR 固实压缩必须按顺序读取，不能使用 Archive API 随机访问。
                    using (IReader reader = RarReader.OpenReader(archivePath, readerOptions))
                    {
                        ExtractReader(
                            reader,
                            root,
                            comparison,
                            options,
                            progress,
                            -1L,
                            0,
                            ct);
                    }

                    return;
                }

                using (IArchive archive = ArchiveFactory.OpenArchive(archivePath, readerOptions))
                {
                    int totalEntries = CountEntries(archive, options);
                    long totalBytes = archive.TotalUncompressedSize;
                    ValidateTotalBytes(totalBytes, options);
                    using (IReader reader = archive.ExtractAllEntries())
                    {
                        ExtractReader(
                            reader,
                            root,
                            comparison,
                            options,
                            progress,
                            totalBytes,
                            totalEntries,
                            ct);
                    }
                }

                return;
            }

            if (options.Format == ArchiveFormat.Auto && IsBZip2(archivePath))
            {
                ExtractBZip2(
                    archivePath, destinationDirectory, options, progress, ct);
                return;
            }

            throw new RFrameworkException(
                "SharpCompressArchiveHelper: unsupported or invalid archive format.");
        }

        private static void ExtractReader(
            IReader reader,
            string root,
            StringComparison comparison,
            ArchiveExtractionOptions options,
            IProgress<ArchiveProgress> progress,
            long totalBytes,
            int totalEntries,
            CancellationToken ct)
        {
            long processedBytes = 0L;
            int processedEntries = 0;
            byte[] buffer = new byte[BufferSize];

            while (reader.MoveToNextEntry())
            {
                ct.ThrowIfCancellationRequested();
                IEntry entry = reader.Entry;
                processedEntries++;
                if (options.MaxEntries > 0 && processedEntries > options.MaxEntries)
                {
                    throw new RFrameworkException(
                        $"SharpCompressArchiveHelper: archive contains too many entries ({processedEntries}).");
                }

                if (!string.IsNullOrEmpty(entry.LinkTarget))
                {
                    throw new RFrameworkException(
                        $"SharpCompressArchiveHelper: link entry '{entry.Key}' is not allowed.");
                }

                string entryPath = GetSafeEntryPath(root, entry.Key, comparison);
                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(entryPath);
                    Report(progress, processedBytes, totalBytes,
                        processedEntries, totalEntries, entry.Key);
                    continue;
                }

                string parent = Path.GetDirectoryName(entryPath);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                using (Stream input = reader.OpenEntryStream())
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
                                "SharpCompressArchiveHelper: extracted size exceeds the configured limit.");
                        }

                        Report(progress, processedBytes, totalBytes,
                            processedEntries - 1, totalEntries, entry.Key);
                    }
                }

                Report(progress, processedBytes, totalBytes,
                    processedEntries, totalEntries, entry.Key);
            }
        }

        private static void ExtractBZip2(
            string archivePath,
            string destinationDirectory,
            ArchiveExtractionOptions options,
            IProgress<ArchiveProgress> progress,
            CancellationToken ct)
        {
            string fileName = Path.GetFileNameWithoutExtension(archivePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "content";
            }

            string outputPath = Path.Combine(destinationDirectory, fileName);
            long processedBytes = 0L;
            byte[] buffer = new byte[BufferSize];
            using (FileStream source = File.OpenRead(archivePath))
            using (BZip2Stream input = BZip2Stream.Create(source, CompressionMode.Decompress, false))
            using (FileStream output = new FileStream(
                outputPath, FileMode.Create, FileAccess.Write, FileShare.None,
                BufferSize, FileOptions.SequentialScan))
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
                            "SharpCompressArchiveHelper: extracted size exceeds the configured limit.");
                    }

                    Report(progress, processedBytes, -1L, 0, 1, fileName);
                }
            }

            Report(progress, processedBytes, processedBytes, 1, 1, fileName);
        }

        private static bool IsBZip2(string archivePath)
        {
            using (FileStream stream = File.OpenRead(archivePath))
            {
                return BZip2Stream.IsBZip2(stream);
            }
        }

        private static int CountEntries(IArchive archive, ArchiveExtractionOptions options)
        {
            int count = 0;
            foreach (IArchiveEntry entry in archive.Entries)
            {
                count++;
                if (options.MaxEntries > 0 && count > options.MaxEntries)
                {
                    throw new RFrameworkException(
                        $"SharpCompressArchiveHelper: archive contains too many entries ({count}).");
                }
            }

            return count;
        }

        private static void ValidateTotalBytes(long totalBytes, ArchiveExtractionOptions options)
        {
            if (options.MaxExtractedBytes > 0 && totalBytes > options.MaxExtractedBytes)
            {
                throw new RFrameworkException(
                    "SharpCompressArchiveHelper: extracted size exceeds the configured limit.");
            }
        }

        private static void ValidateDetectedFormat(ArchiveType detected, ArchiveFormat requested)
        {
            if (requested == ArchiveFormat.Auto)
            {
                return;
            }

            bool matches = requested == ArchiveFormat.Zip && detected == ArchiveType.Zip
                || requested == ArchiveFormat.Rar && detected == ArchiveType.Rar
                || requested == ArchiveFormat.SevenZip && detected == ArchiveType.SevenZip
                || requested == ArchiveFormat.Tar && detected == ArchiveType.Tar
                || requested == ArchiveFormat.GZip && detected == ArchiveType.GZip;
            if (!matches)
            {
                throw new RFrameworkException(
                    $"SharpCompressArchiveHelper: requested format '{requested}' does not match detected format '{detected}'.");
            }
        }

        private static string GetExtensionHint(ArchiveFormat format)
        {
            switch (format)
            {
                case ArchiveFormat.Zip:
                    return "zip";
                case ArchiveFormat.Rar:
                    return "rar";
                case ArchiveFormat.SevenZip:
                    return "7z";
                case ArchiveFormat.Tar:
                    return "tar";
                case ArchiveFormat.GZip:
                    return "gz";
                case ArchiveFormat.BZip2:
                    return "bz2";
                default:
                    return null;
            }
        }

        private static string GetSafeEntryPath(
            string root,
            string entryName,
            StringComparison comparison)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                throw new RFrameworkException(
                    "SharpCompressArchiveHelper: archive entry name is empty.");
            }

            string normalized = entryName
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            if (normalized.IndexOf(':') >= 0)
            {
                throw new RFrameworkException(
                    $"SharpCompressArchiveHelper: unsafe archive entry path '{entryName}'.");
            }

            string entryPath = Path.GetFullPath(Path.Combine(root, normalized));
            if (!entryPath.StartsWith(root, comparison))
            {
                throw new RFrameworkException(
                    $"SharpCompressArchiveHelper: unsafe archive entry path '{entryName}'.");
            }

            return entryPath;
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static void Report(
            IProgress<ArchiveProgress> progress,
            long processedBytes,
            long totalBytes,
            int processedEntries,
            int totalEntries,
            string entryName)
        {
            progress?.Report(new ArchiveProgress(
                processedBytes,
                totalBytes,
                processedEntries,
                totalEntries,
                entryName));
        }

        private static void ValidateArguments(
            string archivePath,
            string destinationDirectory,
            ArchiveExtractionOptions options)
        {
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            {
                throw new RFrameworkException(
                    "SharpCompressArchiveHelper: archive file does not exist.");
            }

            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                throw new RFrameworkException(
                    "SharpCompressArchiveHelper: destination directory is invalid.");
            }

            if (options == null)
            {
                throw new RFrameworkException(
                    "SharpCompressArchiveHelper: options cannot be null.");
            }

            if (options.MaxEntries < 0 || options.MaxExtractedBytes < 0L)
            {
                throw new RFrameworkException(
                    "SharpCompressArchiveHelper: extraction limits cannot be negative.");
            }
        }
    }
}
