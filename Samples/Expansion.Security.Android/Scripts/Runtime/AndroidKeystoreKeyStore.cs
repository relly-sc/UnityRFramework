using System;
using System.IO;
using System.Text;
using RFramework;
using UnityEngine;
using UnityEngine.Scripting;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion
{
    /// <summary>使用 Android Keystore 中不可导出的 AES 密钥保护落盘 SaveKey。</summary>
    [Preserve]
    public sealed class AndroidKeystoreKeyStore : IKeyStore
    {
        private const byte BlobVersion = 1;
        private const int MinimumAndroidApiLevel = 23;
        private const string WrappingKeyAlias = "UnityRFramework.Storage.SaveKey";
        private const string AndroidKeyStore = "AndroidKeyStore";
        private const string CipherTransformation = "AES/GCM/NoPadding";

        private static readonly object SyncRoot = new object();
        private readonly FileKeyStore fileStore;

        [Preserve]
        public AndroidKeystoreKeyStore(string rootDirectory)
        {
            fileStore = new FileKeyStore(rootDirectory);
        }

        public bool TryRead(string keyId, out byte[] key)
        {
            if (!fileStore.TryRead(keyId, out byte[] protectedKey))
            {
                key = null;
                return false;
            }

            try
            {
                lock (SyncRoot)
                {
                    key = Unprotect(keyId, protectedKey);
                    return true;
                }
            }
            finally
            {
                Array.Clear(protectedKey, 0, protectedKey.Length);
            }
        }

        public void Write(string keyId, byte[] key)
        {
            if (key == null || key.Length == 0)
            {
                throw new ArgumentException("Key data is invalid.", nameof(key));
            }

            byte[] protectedKey;
            lock (SyncRoot)
            {
                protectedKey = Protect(keyId, key);
            }

            try
            {
                fileStore.Write(keyId, protectedKey);
            }
            finally
            {
                Array.Clear(protectedKey, 0, protectedKey.Length);
            }
        }

        public bool Delete(string keyId)
        {
            return fileStore.Delete(keyId);
        }

        private static byte[] Protect(string keyId, byte[] plaintext)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureSupportedDevice();
            using (AndroidJavaObject wrappingKey = GetOrCreateWrappingKey())
            using (AndroidJavaClass cipherClass = new AndroidJavaClass("javax.crypto.Cipher"))
            using (AndroidJavaObject cipher = cipherClass.CallStatic<AndroidJavaObject>(
                       "getInstance", CipherTransformation))
            {
                cipher.Call("init", 1, wrappingKey);
                ApplyAssociatedData(cipher, keyId);
                byte[] ciphertext = cipher.Call<byte[]>("doFinal", plaintext);
                byte[] iv = cipher.Call<byte[]>("getIV");
                try
                {
                    if (iv == null || iv.Length == 0 || iv.Length > byte.MaxValue)
                    {
                        throw new RFrameworkException("Android Keystore returned an invalid IV.");
                    }

                    byte[] result = new byte[2 + iv.Length + ciphertext.Length];
                    result[0] = BlobVersion;
                    result[1] = (byte)iv.Length;
                    Buffer.BlockCopy(iv, 0, result, 2, iv.Length);
                    Buffer.BlockCopy(ciphertext, 0, result, 2 + iv.Length, ciphertext.Length);
                    return result;
                }
                finally
                {
                    if (iv != null) Array.Clear(iv, 0, iv.Length);
                    if (ciphertext != null) Array.Clear(ciphertext, 0, ciphertext.Length);
                }
            }
#else
            throw new PlatformNotSupportedException(
                "Android Keystore key storage is only available in an Android Player.");
#endif
        }

        private static byte[] Unprotect(string keyId, byte[] protectedData)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureSupportedDevice();
            if (protectedData == null || protectedData.Length < 3
                || protectedData[0] != BlobVersion)
            {
                throw new RFrameworkException("Android Keystore key blob is invalid.");
            }

            int ivLength = protectedData[1];
            int ciphertextLength = protectedData.Length - 2 - ivLength;
            if (ivLength <= 0 || ciphertextLength <= 0)
            {
                throw new RFrameworkException("Android Keystore key blob is truncated.");
            }

            byte[] iv = new byte[ivLength];
            byte[] ciphertext = new byte[ciphertextLength];
            Buffer.BlockCopy(protectedData, 2, iv, 0, iv.Length);
            Buffer.BlockCopy(protectedData, 2 + iv.Length, ciphertext, 0, ciphertext.Length);
            try
            {
                using (AndroidJavaObject wrappingKey = GetExistingWrappingKey())
                using (AndroidJavaClass cipherClass = new AndroidJavaClass("javax.crypto.Cipher"))
                using (AndroidJavaObject cipher = cipherClass.CallStatic<AndroidJavaObject>(
                           "getInstance", CipherTransformation))
                using (AndroidJavaObject parameters = new AndroidJavaObject(
                           "javax.crypto.spec.GCMParameterSpec", 128, iv))
                {
                    if (wrappingKey == null)
                    {
                        throw new RFrameworkException(
                            "Android Keystore wrapping key is unavailable.");
                    }

                    cipher.Call("init", 2, wrappingKey, parameters);
                    ApplyAssociatedData(cipher, keyId);
                    return cipher.Call<byte[]>("doFinal", ciphertext);
                }
            }
            finally
            {
                Array.Clear(iv, 0, iv.Length);
                Array.Clear(ciphertext, 0, ciphertext.Length);
            }
#else
            throw new PlatformNotSupportedException(
                "Android Keystore key storage is only available in an Android Player.");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject GetOrCreateWrappingKey()
        {
            AndroidJavaObject existing = GetExistingWrappingKey();
            if (existing != null) return existing;

            using (AndroidJavaClass keyProperties = new AndroidJavaClass(
                       "android.security.keystore.KeyProperties"))
            using (AndroidJavaClass generatorClass = new AndroidJavaClass(
                       "javax.crypto.KeyGenerator"))
            using (AndroidJavaObject generator = generatorClass.CallStatic<AndroidJavaObject>(
                       "getInstance", "AES", AndroidKeyStore))
            using (AndroidJavaObject builder = new AndroidJavaObject(
                       "android.security.keystore.KeyGenParameterSpec$Builder",
                       WrappingKeyAlias,
                       keyProperties.GetStatic<int>("PURPOSE_ENCRYPT")
                       | keyProperties.GetStatic<int>("PURPOSE_DECRYPT")))
            {
                builder.Call<AndroidJavaObject>(
                    "setBlockModes", (object)new[] { "GCM" });
                builder.Call<AndroidJavaObject>(
                    "setEncryptionPaddings", (object)new[] { "NoPadding" });
                builder.Call<AndroidJavaObject>("setRandomizedEncryptionRequired", true);
                builder.Call<AndroidJavaObject>("setUserAuthenticationRequired", false);
                using (AndroidJavaObject specification =
                       builder.Call<AndroidJavaObject>("build"))
                {
                    generator.Call("init", specification);
                    return generator.Call<AndroidJavaObject>("generateKey");
                }
            }
        }

        private static AndroidJavaObject GetExistingWrappingKey()
        {
            using (AndroidJavaClass keyStoreClass = new AndroidJavaClass(
                       "java.security.KeyStore"))
            using (AndroidJavaObject keyStore = keyStoreClass.CallStatic<AndroidJavaObject>(
                       "getInstance", AndroidKeyStore))
            {
                keyStore.Call("load", new object[] { null });
                if (!keyStore.Call<bool>("containsAlias", WrappingKeyAlias)) return null;
                return keyStore.Call<AndroidJavaObject>(
                    "getKey", new object[] { WrappingKeyAlias, null });
            }
        }

        private static void ApplyAssociatedData(AndroidJavaObject cipher, string keyId)
        {
            byte[] associatedData = Encoding.UTF8.GetBytes(keyId);
            try
            {
                cipher.Call("updateAAD", associatedData);
            }
            finally
            {
                Array.Clear(associatedData, 0, associatedData.Length);
            }
        }

        private static void EnsureSupportedDevice()
        {
            using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                if (version.GetStatic<int>("SDK_INT") < MinimumAndroidApiLevel)
                {
                    throw new PlatformNotSupportedException(
                        "Android Keystore AES-GCM requires Android API level 23 or newer.");
                }
            }
        }
#endif
    }

    internal static class AndroidKeystoreKeyStoreRegistration
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            InstallSaveKeyStoreRegistry.RegisterPlatformFactory(
                rootDirectory => new AndroidKeystoreKeyStore(rootDirectory));
        }
#endif
    }
}
