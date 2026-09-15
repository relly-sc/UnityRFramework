using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine.Scripting;

namespace UnityRFramework.Runtime
{
    /// <summary>使用本地文件系统实现原子提交和备份的默认存档辅助器。</summary>
    [Preserve]
    public sealed class DefaultStorageHelper : StorageHelperBase
    {
        private const string SaveExtension = ".save";
        private const string BackupExtension = ".bak";
        private const string TemporaryExtension = ".tmp";

        private string rootDirectory;

        /// <inheritdoc />
        public override void Initialize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new RFrameworkException("Storage root directory is invalid.");
            }

            rootDirectory = Path.GetFullPath(value);
            Directory.CreateDirectory(rootDirectory);
        }

        /// <inheritdoc />
        public override async Task WriteAtomicAsync(
            string slotName, byte[] data, bool createBackup, CancellationToken ct)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            string target = GetPath(slotName, false);
            string backup = target + BackupExtension;
            string temporary = target + TemporaryExtension;
            Directory.CreateDirectory(rootDirectory);

            try
            {
                using (FileStream stream = new FileStream(
                    temporary, FileMode.Create, FileAccess.Write, FileShare.None,
                    81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(data, 0, data.Length, ct).ConfigureAwait(false);
                    await stream.FlushAsync(ct).ConfigureAwait(false);
                    stream.Flush(true);
                }

                ct.ThrowIfCancellationRequested();
                CommitTemporaryFile(temporary, target, backup, createBackup);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        /// <inheritdoc />
        public override async Task<byte[]> ReadAsync(
            string slotName, bool backup, CancellationToken ct)
        {
            string path = GetPath(slotName, backup);
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                if (stream.Length > int.MaxValue)
                {
                    throw new IOException("Storage file is too large.");
                }

                byte[] data = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < data.Length)
                {
                    int read = await stream.ReadAsync(
                        data, offset, data.Length - offset, ct).ConfigureAwait(false);
                    if (read == 0) throw new EndOfStreamException("Storage file is truncated.");
                    offset += read;
                }
                return data;
            }
        }

        /// <inheritdoc />
        public override Task DeleteAsync(string slotName, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            string target = GetPath(slotName, false);
            DeleteIfExists(target);
            DeleteIfExists(target + BackupExtension);
            DeleteIfExists(target + TemporaryExtension);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override Task<bool> ExistsAsync(string slotName, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(File.Exists(GetPath(slotName, false)));
        }

        /// <inheritdoc />
        public override Task<IReadOnlyList<StorageSlotInfo>> GetSlotsAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var result = new List<StorageSlotInfo>();
            if (!Directory.Exists(rootDirectory))
            {
                return Task.FromResult<IReadOnlyList<StorageSlotInfo>>(result);
            }

            string[] files = Directory.GetFiles(rootDirectory, "*" + SaveExtension);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string path in files)
            {
                ct.ThrowIfCancellationRequested();
                FileInfo info = new FileInfo(path);
                result.Add(new StorageSlotInfo(
                    Path.GetFileNameWithoutExtension(path),
                    info.Length,
                    info.LastWriteTimeUtc,
                    File.Exists(path + BackupExtension)));
            }
            return Task.FromResult<IReadOnlyList<StorageSlotInfo>>(result);
        }

        private static void CommitTemporaryFile(
            string temporary, string target, string backup, bool createBackup)
        {
            if (!File.Exists(target))
            {
                File.Move(temporary, target);
                return;
            }

            if (createBackup)
            {
                DeleteIfExists(backup);
                try
                {
                    File.Replace(temporary, target, backup, true);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                }
                catch (IOException)
                {
                    // 部分移动平台文件系统不支持 Replace，使用可恢复的移动流程。
                }

                File.Move(target, backup);
                try
                {
                    File.Move(temporary, target);
                }
                catch
                {
                    if (!File.Exists(target) && File.Exists(backup)) File.Move(backup, target);
                    throw;
                }
                return;
            }

            string rollback = target + ".rollback";
            DeleteIfExists(rollback);
            File.Move(target, rollback);
            try
            {
                File.Move(temporary, target);
                DeleteIfExists(rollback);
            }
            catch
            {
                if (!File.Exists(target) && File.Exists(rollback)) File.Move(rollback, target);
                throw;
            }
        }

        private string GetPath(string slotName, bool backup)
        {
            if (string.IsNullOrEmpty(rootDirectory))
            {
                throw new RFrameworkException("Storage helper is not initialized.");
            }

            string fileName = slotName + SaveExtension + (backup ? BackupExtension : string.Empty);
            string path = Path.GetFullPath(Path.Combine(rootDirectory, fileName));
            string root = rootDirectory.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new RFrameworkException("Storage slot resolves outside the storage root.");
            }
            return path;
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
