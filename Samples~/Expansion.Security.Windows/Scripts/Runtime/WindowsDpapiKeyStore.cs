using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using RFramework;
using UnityEngine.Scripting;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion
{
    /// <summary>使用 Windows 当前用户 DPAPI 保护落盘密钥。</summary>
    [Preserve]
    public sealed class WindowsDpapiKeyStore : IKeyStore
    {
        private const uint CryptProtectUiForbidden = 0x1;
        private readonly FileKeyStore fileStore;

        [Preserve]
        public WindowsDpapiKeyStore(string rootDirectory)
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
                key = Unprotect(protectedKey);
                return true;
            }
            finally
            {
                Array.Clear(protectedKey, 0, protectedKey.Length);
            }
        }

        public void Write(string keyId, byte[] key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            byte[] protectedKey = Protect(key);
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

        private static byte[] Protect(byte[] data)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return Transform(data, true);
#else
            throw new PlatformNotSupportedException(
                "Windows DPAPI key storage is only available on Windows.");
#endif
        }

        private static byte[] Unprotect(byte[] data)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return Transform(data, false);
#else
            throw new PlatformNotSupportedException(
                "Windows DPAPI key storage is only available on Windows.");
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static byte[] Transform(byte[] data, bool protect)
        {
            if (data == null || data.Length == 0)
            {
                throw new RFrameworkException("DPAPI input is invalid.");
            }

            DataBlob input = default(DataBlob);
            DataBlob output = default(DataBlob);
            IntPtr description = IntPtr.Zero;
            input.DataLength = data.Length;
            input.Data = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, input.Data, data.Length);
                bool succeeded = protect
                    ? CryptProtectData(
                        ref input,
                        null,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CryptProtectUiForbidden,
                        out output)
                    : CryptUnprotectData(
                        ref input,
                        out description,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CryptProtectUiForbidden,
                        out output);
                if (!succeeded)
                {
                    throw new RFrameworkException(
                        $"Windows DPAPI operation failed with code {Marshal.GetLastWin32Error()}.",
                        new Win32Exception(Marshal.GetLastWin32Error()));
                }

                byte[] result = new byte[output.DataLength];
                Marshal.Copy(output.Data, result, 0, result.Length);
                return result;
            }
            finally
            {
                ZeroAndFree(input.Data, input.DataLength, false);
                ZeroAndFree(output.Data, output.DataLength, true);
                if (description != IntPtr.Zero) LocalFree(description);
            }
        }

        private static void ZeroAndFree(IntPtr pointer, int length, bool localFree)
        {
            if (pointer == IntPtr.Zero) return;
            if (length > 0)
            {
                byte[] zeros = new byte[length];
                Marshal.Copy(zeros, 0, pointer, zeros.Length);
            }

            if (localFree) LocalFree(pointer);
            else Marshal.FreeHGlobal(pointer);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int DataLength;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string description,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr prompt,
            uint flags,
            out DataBlob dataOut);

        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            out IntPtr description,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr prompt,
            uint flags,
            out DataBlob dataOut);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr memory);
#endif
    }

    internal static class WindowsDpapiKeyStoreRegistration
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            InstallSaveKeyStoreRegistry.RegisterPlatformFactory(
                rootDirectory => new WindowsDpapiKeyStore(rootDirectory));
        }
#endif
    }
}
