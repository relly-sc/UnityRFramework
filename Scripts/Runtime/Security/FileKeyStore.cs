using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using RFramework;
using UnityEngine.Scripting;

namespace UnityRFramework.Runtime
{
    /// <summary>使用散列文件名持久化不透明密钥数据；本身不提供平台级加密。</summary>
    [Preserve]
    public sealed class FileKeyStore : IKeyStore
    {
        private const string KeyExtension = ".key";
        private readonly string rootDirectory;

        public FileKeyStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("Key store root directory is invalid.", nameof(rootDirectory));
            }

            this.rootDirectory = Path.GetFullPath(rootDirectory);
        }

        public bool TryRead(string keyId, out byte[] key)
        {
            string path = GetPath(keyId);
            if (!File.Exists(path))
            {
                key = null;
                return false;
            }

            key = File.ReadAllBytes(path);
            return true;
        }

        public void Write(string keyId, byte[] key)
        {
            if (key == null || key.Length == 0)
            {
                throw new ArgumentException("Key data is invalid.", nameof(key));
            }

            Directory.CreateDirectory(rootDirectory);
            string target = GetPath(keyId);
            if (File.Exists(target))
            {
                throw new RFrameworkException($"Key '{keyId}' already exists.");
            }

            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                {
                    stream.Write(key, 0, key.Length);
                    stream.Flush(true);
                }

                File.Move(temporary, target);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public bool Delete(string keyId)
        {
            string path = GetPath(keyId);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        private string GetPath(string keyId)
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new RFrameworkException("Key ID is invalid.");
            }

            byte[] idBytes = Encoding.UTF8.GetBytes(keyId);
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = algorithm.ComputeHash(idBytes);
            }
            Array.Clear(idBytes, 0, idBytes.Length);

            var fileName = new StringBuilder(hash.Length * 2 + KeyExtension.Length);
            foreach (byte value in hash) fileName.Append(value.ToString("x2"));
            Array.Clear(hash, 0, hash.Length);
            fileName.Append(KeyExtension);
            return Path.Combine(rootDirectory, fileName.ToString());
        }
    }
}
