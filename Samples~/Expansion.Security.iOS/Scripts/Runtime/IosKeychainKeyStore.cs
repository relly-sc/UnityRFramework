using System;
using System.Runtime.InteropServices;
using RFramework;
using UnityEngine;
using UnityEngine.Scripting;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion
{
    /// <summary>使用 iOS Keychain 持久化本机安装级 SaveKey。</summary>
    [Preserve]
    public sealed class IosKeychainKeyStore : IKeyStore
    {
        private const int Success = 0;
        private const int ItemNotFound = -25300;
        private const int DuplicateItem = -25299;
        private const string ServiceSuffix = ".UnityRFramework.Storage";

        private readonly string service;

        [Preserve]
        public IosKeychainKeyStore(string rootDirectory)
        {
            service = Application.identifier + ServiceSuffix;
        }

        public bool TryRead(string keyId, out byte[] key)
        {
            ValidateKeyId(keyId);
#if UNITY_IOS && !UNITY_EDITOR
            int status = UnityRFrameworkKeychainRead(
                service, keyId, out IntPtr data, out int length);
            if (status == ItemNotFound)
            {
                key = null;
                return false;
            }

            if (status != Success || data == IntPtr.Zero || length <= 0)
            {
                if (data != IntPtr.Zero) UnityRFrameworkKeychainFree(data, length);
                throw CreateKeychainException("read", status);
            }

            try
            {
                key = new byte[length];
                Marshal.Copy(data, key, 0, length);
                return true;
            }
            finally
            {
                UnityRFrameworkKeychainFree(data, length);
            }
#else
            throw new PlatformNotSupportedException(
                "iOS Keychain storage is only available in an iOS Player.");
#endif
        }

        public void Write(string keyId, byte[] key)
        {
            ValidateKeyId(keyId);
            if (key == null || key.Length == 0)
            {
                throw new ArgumentException("Key data is invalid.", nameof(key));
            }

#if UNITY_IOS && !UNITY_EDITOR
            int status = UnityRFrameworkKeychainWrite(service, keyId, key, key.Length);
            if (status == DuplicateItem)
            {
                throw new RFrameworkException($"Key '{keyId}' already exists.");
            }

            if (status != Success) throw CreateKeychainException("write", status);
#else
            throw new PlatformNotSupportedException(
                "iOS Keychain storage is only available in an iOS Player.");
#endif
        }

        public bool Delete(string keyId)
        {
            ValidateKeyId(keyId);
#if UNITY_IOS && !UNITY_EDITOR
            int status = UnityRFrameworkKeychainDelete(service, keyId);
            if (status == ItemNotFound) return false;
            if (status != Success) throw CreateKeychainException("delete", status);
            return true;
#else
            throw new PlatformNotSupportedException(
                "iOS Keychain storage is only available in an iOS Player.");
#endif
        }

        private static void ValidateKeyId(string keyId)
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new RFrameworkException("Key ID is invalid.");
            }
        }

        private static RFrameworkException CreateKeychainException(
            string operation, int status)
        {
            return new RFrameworkException(
                $"iOS Keychain {operation} failed with status {status}.");
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int UnityRFrameworkKeychainRead(
            string service,
            string account,
            out IntPtr data,
            out int length);

        [DllImport("__Internal")]
        private static extern int UnityRFrameworkKeychainWrite(
            string service,
            string account,
            byte[] data,
            int length);

        [DllImport("__Internal")]
        private static extern int UnityRFrameworkKeychainDelete(
            string service,
            string account);

        [DllImport("__Internal")]
        private static extern void UnityRFrameworkKeychainFree(
            IntPtr data,
            int length);
#endif
    }

    internal static class IosKeychainKeyStoreRegistration
    {
#if UNITY_IOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            InstallSaveKeyStoreRegistry.RegisterPlatformFactory(
                rootDirectory => new IosKeychainKeyStore(rootDirectory));
        }
#endif
    }
}
