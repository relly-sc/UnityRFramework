using System;
using System.Threading.Tasks;
using RFramework.Resource;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认资源辅助器（占位实现）。
    /// 所有方法均抛出 <see cref="NotSupportedException"/>，
    /// 提示在 Expansion 层通过 YooAsset 或 Addressables 提供真实实现。
    /// </summary>
    public sealed class DefaultResourceHelper : ResourceHelperBase
    {
        /// <inheritdoc/>
        public override Task InitializeAsync(string packageName, ResourcePlayMode playMode,
            string defaultHostServer, string fallbackHostServer)
        {
            throw new NotSupportedException(
                "DefaultResourceHelper: Resource helper not set. "
                + "Please provide a real IResourceHelper implementation (e.g. YooAssetResourceHelper) "
                + "in the Expansion layer.");
        }

        /// <inheritdoc/>
        public override void Destroy()
        {
            throw new NotSupportedException(
                "DefaultResourceHelper: Resource helper not set.");
        }

        /// <inheritdoc/>
        public override Task<object> LoadAssetAsync(string location, Type assetType, uint priority)
        {
            throw new NotSupportedException(
                "DefaultResourceHelper: Resource helper not set.");
        }

        /// <inheritdoc/>
        public override object LoadAssetSync(string location, Type assetType)
        {
            throw new NotSupportedException(
                "DefaultResourceHelper: Resource helper not set.");
        }

        /// <inheritdoc/>
        public override void ReleaseAsset(string location)
        {
            throw new NotSupportedException(
                "DefaultResourceHelper: Resource helper not set.");
        }

        /// <inheritdoc/>
        public override Task LoadSceneAsync(string location, SceneLoadMode sceneMode,
            bool activateOnLoad, uint priority)
        {
            throw new NotSupportedException(
                "DefaultResourceHelper: Resource helper not set.");
        }

        /// <inheritdoc/>
        public override Task UnloadSceneAsync(string location)
        {
            throw new NotSupportedException(
                "DefaultResourceHelper: Resource helper not set.");
        }

        /// <inheritdoc/>
        public override bool IsLocationValid(string location)
        {
            throw new NotSupportedException(
                "DefaultResourceHelper: Resource helper not set.");
        }

        /// <inheritdoc/>
        public override long GetDownloadSize(string location)
        {
            throw new NotSupportedException(
                "DefaultResourceHelper: Resource helper not set.");
        }
    }
}
