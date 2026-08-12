using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 本地可替换文件与 Resources 兜底资源辅助器。
    /// byte[]、string、TextAsset、Texture2D、Sprite 和 AudioClip 按
    /// persistentDataPath、StreamingAssets、Resources 的顺序加载；
    /// 其他 Unity 资产与场景交给 DefaultResourceHelper。
    /// </summary>
    public sealed class LocalFileResourceHelper : ResourceHelperBase, IResourceUrlProvider
    {
        private readonly Dictionary<AssetHandleKey, LoadedAssetHandle> loadedAssets =
            new Dictionary<AssetHandleKey, LoadedAssetHandle>();

        private DefaultResourceHelper resourcesFallback;

        /// <inheritdoc/>
        public override async Task InitializeAsync(string packageName, ResourcePlayMode playMode,
            string defaultHostServer, string fallbackHostServer)
        {
            ReleaseAllLoadedAssets();
            if (resourcesFallback == null)
            {
                resourcesFallback = gameObject.GetComponent<DefaultResourceHelper>();
                if (resourcesFallback == null)
                {
                    resourcesFallback = gameObject.AddComponent<DefaultResourceHelper>();
                }
            }

            await resourcesFallback.InitializeAsync(
                packageName, playMode, defaultHostServer, fallbackHostServer);
        }

        /// <inheritdoc/>
        public override void Destroy()
        {
            ReleaseAllLoadedAssets();
            resourcesFallback?.Destroy();
        }

        /// <inheritdoc/>
        public override async Task<object> LoadAssetAsync(string location, Type assetType,
            uint priority, CancellationToken ct = default)
        {
            ValidateLocation(location);
            Type requestedType = assetType ?? typeof(Object);
            Type localFileType = ResolveLocalFileType(location, requestedType);
            AssetHandleKey key = new AssetHandleKey(location, requestedType);
            if (loadedAssets.TryGetValue(key, out LoadedAssetHandle cached))
            {
                return cached.Asset;
            }

            if (!CanLoadFromLocalFile(localFileType))
            {
                return await LoadFallbackAsync(key, location, requestedType, priority, ct);
            }

            string persistentPath = GetPersistentPath(location);
            if (File.Exists(persistentPath))
            {
                LoadedAssetHandle persistentHandle = await LoadFromUrlAsync(
                    ToRequestUrl(persistentPath), localFileType, ct);
                loadedAssets.Add(key, persistentHandle);
                return persistentHandle.Asset;
            }

            string streamingPath = GetStreamingPath(location);
            LoadedAssetHandle streamingHandle = null;
            Exception streamingError = null;
            try
            {
                streamingHandle = await LoadFromUrlAsync(
                    ToRequestUrl(streamingPath), localFileType, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                streamingError = ex;
            }

            if (streamingHandle != null)
            {
                loadedAssets.Add(key, streamingHandle);
                return streamingHandle.Asset;
            }

            object fallbackAsset = await resourcesFallback.LoadAssetAsync(
                location, requestedType, priority, ct);
            if (fallbackAsset == null)
            {
                throw new RFrameworkException(
                    $"LocalFileResourceHelper: failed to load '{location}' from persistentDataPath, "
                    + "StreamingAssets and Resources.", streamingError);
            }

            loadedAssets.Add(key, LoadedAssetHandle.CreateFallback(fallbackAsset));
            return fallbackAsset;
        }

        /// <inheritdoc/>
        public override object LoadAssetSync(string location, Type assetType)
        {
            ValidateLocation(location);
            Type requestedType = assetType ?? typeof(Object);
            Type localFileType = ResolveLocalFileType(location, requestedType);
            AssetHandleKey key = new AssetHandleKey(location, requestedType);
            if (loadedAssets.TryGetValue(key, out LoadedAssetHandle cached))
            {
                return cached.Asset;
            }

            if (!CanLoadFromLocalFile(localFileType))
            {
                return LoadFallbackSync(key, location, requestedType);
            }

            string persistentPath = GetPersistentPath(location);
            if (File.Exists(persistentPath))
            {
                LoadedAssetHandle persistentHandle = LoadFromFileSync(
                    persistentPath, localFileType);
                loadedAssets.Add(key, persistentHandle);
                return persistentHandle.Asset;
            }

            string streamingPath = GetStreamingPath(location);
            if (IsDirectFilePath(streamingPath))
            {
                if (File.Exists(streamingPath))
                {
                    LoadedAssetHandle streamingHandle = LoadFromFileSync(
                        streamingPath, localFileType);
                    loadedAssets.Add(key, streamingHandle);
                    return streamingHandle.Asset;
                }

                return LoadFallbackSync(key, location, requestedType);
            }

            throw new NotSupportedException(
                "LocalFileResourceHelper: synchronous StreamingAssets loading is not supported "
                + "on Android or WebGL. Use LoadAssetAsync instead.");
        }

        /// <inheritdoc/>
        public override void ReleaseAsset(string location, Type assetType)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return;
            }

            Type requestedType = assetType ?? typeof(Object);
            AssetHandleKey key = new AssetHandleKey(location, requestedType);
            if (!loadedAssets.TryGetValue(key, out LoadedAssetHandle handle))
            {
                return;
            }

            loadedAssets.Remove(key);
            if (handle.IsFallback)
            {
                resourcesFallback?.ReleaseAsset(location, requestedType);
                return;
            }

            DestroyOwnedObjects(handle);
        }

        /// <inheritdoc/>
        public override Task LoadSceneAsync(string location, int sceneMode,
            bool activateOnLoad, uint priority, IProgress<float> onProgress = null)
        {
            return resourcesFallback.LoadSceneAsync(
                location, sceneMode, activateOnLoad, priority, onProgress);
        }

        /// <inheritdoc/>
        public override Task UnloadSceneAsync(string location)
        {
            return resourcesFallback.UnloadSceneAsync(location);
        }

        /// <inheritdoc/>
        public override bool IsLocationValid(string location)
        {
            if (string.IsNullOrWhiteSpace(location) || !IsSafeRelativeLocation(location))
            {
                return false;
            }

            if (File.Exists(GetPersistentPath(location)))
            {
                return true;
            }

            string streamingPath = GetStreamingPath(location);
            if (IsDirectFilePath(streamingPath) && File.Exists(streamingPath))
            {
                return true;
            }

            return resourcesFallback != null && resourcesFallback.IsLocationValid(location);
        }

        /// <inheritdoc/>
        public override long GetDownloadSize(string location)
        {
            if (string.IsNullOrWhiteSpace(location) || !IsSafeRelativeLocation(location))
            {
                return 0;
            }

            string persistentPath = GetPersistentPath(location);
            if (File.Exists(persistentPath))
            {
                return new FileInfo(persistentPath).Length;
            }

            string streamingPath = GetStreamingPath(location);
            if (IsDirectFilePath(streamingPath) && File.Exists(streamingPath))
            {
                return new FileInfo(streamingPath).Length;
            }

            return 0;
        }

        /// <inheritdoc/>
        public string GetAssetUrl(string location)
        {
            ValidateLocation(location);
            string persistentPath = GetPersistentPath(location);
            return File.Exists(persistentPath) ? persistentPath : GetStreamingPath(location);
        }

        private async Task<object> LoadFallbackAsync(AssetHandleKey key, string location,
            Type requestedType, uint priority, CancellationToken ct)
        {
            object asset = await resourcesFallback.LoadAssetAsync(
                location, requestedType, priority, ct);
            if (asset != null)
            {
                loadedAssets.Add(key, LoadedAssetHandle.CreateFallback(asset));
            }

            return asset;
        }

        private object LoadFallbackSync(AssetHandleKey key, string location, Type requestedType)
        {
            object asset = resourcesFallback.LoadAssetSync(location, requestedType);
            if (asset != null)
            {
                loadedAssets.Add(key, LoadedAssetHandle.CreateFallback(asset));
            }

            return asset;
        }

        private static async Task<LoadedAssetHandle> LoadFromUrlAsync(
            string url, Type requestedType, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            UnityWebRequest request = CreateRequest(url, requestedType);
            try
            {
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (ct.IsCancellationRequested)
                    {
                        request.Abort();
                        ct.ThrowIfCancellationRequested();
                    }

                    await Task.Yield();
                }

                ct.ThrowIfCancellationRequested();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new RFrameworkException(
                        $"LocalFileResourceHelper: request failed '{url}'. {request.error}");
                }

                return CreateHandleFromRequest(request, requestedType);
            }
            finally
            {
                request.Dispose();
            }
        }

        private static UnityWebRequest CreateRequest(string url, Type requestedType)
        {
            if (requestedType == typeof(Texture2D) || requestedType == typeof(Sprite))
            {
                return UnityWebRequestTexture.GetTexture(url, false);
            }

            if (requestedType == typeof(AudioClip))
            {
                return UnityWebRequestMultimedia.GetAudioClip(url, GetAudioType(url));
            }

            return UnityWebRequest.Get(url);
        }

        private static LoadedAssetHandle CreateHandleFromRequest(
            UnityWebRequest request, Type requestedType)
        {
            if (requestedType == typeof(byte[]))
            {
                return LoadedAssetHandle.CreateOwned(request.downloadHandler.data);
            }

            if (requestedType == typeof(string))
            {
                return LoadedAssetHandle.CreateOwned(request.downloadHandler.text);
            }

            if (requestedType == typeof(TextAsset))
            {
                TextAsset textAsset = new TextAsset(
                    Encoding.UTF8.GetString(request.downloadHandler.data));
                return LoadedAssetHandle.CreateOwned(textAsset, textAsset);
            }

            if (requestedType == typeof(Texture2D))
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                return LoadedAssetHandle.CreateOwned(texture, texture);
            }

            if (requestedType == typeof(Sprite))
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite sprite = Sprite.Create(texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                return LoadedAssetHandle.CreateOwned(sprite, sprite, texture);
            }

            AudioClip audioClip = DownloadHandlerAudioClip.GetContent(request);
            return LoadedAssetHandle.CreateOwned(audioClip, audioClip);
        }

        private static LoadedAssetHandle LoadFromFileSync(string path, Type requestedType)
        {
            if (requestedType == typeof(AudioClip))
            {
                throw new NotSupportedException(
                    "LocalFileResourceHelper: AudioClip only supports asynchronous local-file loading.");
            }

            byte[] bytes = File.ReadAllBytes(path);
            if (requestedType == typeof(byte[]))
            {
                return LoadedAssetHandle.CreateOwned(bytes);
            }

            string text = Encoding.UTF8.GetString(bytes);
            if (requestedType == typeof(string))
            {
                return LoadedAssetHandle.CreateOwned(text);
            }

            if (requestedType == typeof(TextAsset))
            {
                TextAsset textAsset = new TextAsset(text);
                return LoadedAssetHandle.CreateOwned(textAsset, textAsset);
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes, false))
            {
                DestroyUnityObject(texture);
                throw new RFrameworkException(
                    $"LocalFileResourceHelper: image decode failed '{path}'.");
            }

            if (requestedType == typeof(Texture2D))
            {
                return LoadedAssetHandle.CreateOwned(texture, texture);
            }

            Sprite sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            return LoadedAssetHandle.CreateOwned(sprite, sprite, texture);
        }

        private void ReleaseAllLoadedAssets()
        {
            foreach (KeyValuePair<AssetHandleKey, LoadedAssetHandle> pair in loadedAssets)
            {
                if (pair.Value.IsFallback)
                {
                    resourcesFallback?.ReleaseAsset(pair.Key.Location, pair.Key.AssetType);
                }
                else
                {
                    DestroyOwnedObjects(pair.Value);
                }
            }

            loadedAssets.Clear();
        }

        private static void DestroyOwnedObjects(LoadedAssetHandle handle)
        {
            for (int i = 0; i < handle.OwnedObjects.Count; i++)
            {
                Object owned = handle.OwnedObjects[i];
                if (owned != null)
                {
                    DestroyUnityObject(owned);
                }
            }
        }

        private static void DestroyUnityObject(Object target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        private static bool CanLoadFromLocalFile(Type requestedType)
        {
            return requestedType == typeof(byte[])
                || requestedType == typeof(string)
                || requestedType == typeof(TextAsset)
                || requestedType == typeof(Texture2D)
                || requestedType == typeof(Sprite)
                || requestedType == typeof(AudioClip);
        }

        private static Type ResolveLocalFileType(string location, Type requestedType)
        {
            if (requestedType != typeof(object) && requestedType != typeof(Object))
            {
                return requestedType;
            }

            string extension = Path.GetExtension(location).ToLowerInvariant();
            switch (extension)
            {
                case ".wav":
                case ".ogg":
                case ".mp3":
                    return typeof(AudioClip);
                default:
                    return requestedType;
            }
        }

        private static AudioType GetAudioType(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".wav":
                    return AudioType.WAV;
                case ".ogg":
                    return AudioType.OGGVORBIS;
                case ".mp3":
                    return AudioType.MPEG;
                default:
                    throw new NotSupportedException(
                        $"LocalFileResourceHelper: unsupported audio extension '{extension}'.");
            }
        }

        private static string GetPersistentPath(string location)
        {
            string normalized = NormalizeLocation(location);
            return Path.Combine(Application.persistentDataPath,
                normalized.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string GetStreamingPath(string location)
        {
            string normalized = NormalizeLocation(location);
            return Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + normalized;
        }

        private static string ToRequestUrl(string path)
        {
            if (path.IndexOf("://", StringComparison.Ordinal) >= 0
                || path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return new Uri(Path.GetFullPath(path)).AbsoluteUri;
        }

        private static bool IsDirectFilePath(string path)
        {
            return path.IndexOf("://", StringComparison.Ordinal) < 0
                && !path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location) || !IsSafeRelativeLocation(location))
            {
                throw new RFrameworkException(
                    "LocalFileResourceHelper: location must be a safe relative path.");
            }
        }

        private static bool IsSafeRelativeLocation(string location)
        {
            if (Path.IsPathRooted(location))
            {
                return false;
            }

            string[] segments = location.Replace('\\', '/').Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == "..")
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeLocation(string location)
        {
            return location.Trim().Replace('\\', '/').TrimStart('/');
        }

        private readonly struct AssetHandleKey : IEquatable<AssetHandleKey>
        {
            public AssetHandleKey(string location, Type assetType)
            {
                Location = NormalizeLocation(location);
                AssetType = assetType;
            }

            public string Location { get; }

            public Type AssetType { get; }

            public bool Equals(AssetHandleKey other)
            {
                return string.Equals(Location, other.Location, StringComparison.Ordinal)
                    && AssetType == other.AssetType;
            }

            public override bool Equals(object obj)
            {
                return obj is AssetHandleKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Location != null ? Location.GetHashCode() : 0) * 397)
                        ^ (AssetType != null ? AssetType.GetHashCode() : 0);
                }
            }
        }

        private sealed class LoadedAssetHandle
        {
            private LoadedAssetHandle(object asset, bool isFallback, List<Object> ownedObjects)
            {
                Asset = asset;
                IsFallback = isFallback;
                OwnedObjects = ownedObjects;
            }

            public object Asset { get; }

            public bool IsFallback { get; }

            public List<Object> OwnedObjects { get; }

            public static LoadedAssetHandle CreateFallback(object asset)
            {
                return new LoadedAssetHandle(asset, true, new List<Object>());
            }

            public static LoadedAssetHandle CreateOwned(object asset, params Object[] ownedObjects)
            {
                return new LoadedAssetHandle(
                    asset, false, new List<Object>(ownedObjects ?? Array.Empty<Object>()));
            }
        }
    }
}
