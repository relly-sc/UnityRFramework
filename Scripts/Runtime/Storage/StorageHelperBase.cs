using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>平台存档文件辅助器基类。</summary>
    public abstract class StorageHelperBase : MonoBehaviour, IStorageHelper
    {
        /// <summary>初始化存档根目录。</summary>
        /// <param name="rootDirectory">存档根目录绝对路径。</param>
        public abstract void Initialize(string rootDirectory);

        /// <inheritdoc />
        public abstract System.Threading.Tasks.Task WriteAtomicAsync(
            string slotName, byte[] data, bool createBackup,
            System.Threading.CancellationToken ct);

        /// <inheritdoc />
        public abstract System.Threading.Tasks.Task<byte[]> ReadAsync(
            string slotName, bool backup, System.Threading.CancellationToken ct);

        /// <inheritdoc />
        public abstract System.Threading.Tasks.Task DeleteAsync(
            string slotName, System.Threading.CancellationToken ct);

        /// <inheritdoc />
        public abstract System.Threading.Tasks.Task<bool> ExistsAsync(
            string slotName, System.Threading.CancellationToken ct);

        /// <inheritdoc />
        public abstract System.Threading.Tasks.Task<
            System.Collections.Generic.IReadOnlyList<StorageSlotInfo>> GetSlotsAsync(
            System.Threading.CancellationToken ct);
    }
}
