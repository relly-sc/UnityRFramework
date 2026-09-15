using System;
using System.IO;
using System.Text;
using RFramework;

namespace UnityRFramework.Editor
{
    /// <summary>为 Config 正式二进制产物提供可选的加密认证和稳定写入。</summary>
    internal sealed class ConfigProtectionExporter
    {
        internal const string ManifestName = "UnityRFramework.ConfigProtection.manifest";

        private readonly ConfigProtectionMode mode;
        private readonly string keyId;
        private readonly string sourceRoot;
        private readonly ConfigReleaseDataFormat releaseFormat;
        private readonly IDataProtector protector;

        private ConfigProtectionExporter(
            ConfigProtectionMode mode,
            string keyId,
            string sourceRoot,
            ConfigReleaseDataFormat releaseFormat,
            IDataProtector protector)
        {
            this.mode = mode;
            this.keyId = keyId;
            this.sourceRoot = sourceRoot;
            this.releaseFormat = releaseFormat;
            this.protector = protector;
        }

        internal static ConfigProtectionExporter Create(
            ConfigPipelineOptions options,
            IKeyProvider keyProvider)
        {
            if (options.ConfigReleaseFormat != ConfigReleaseDataFormat.FrameworkBinary
                && options.ConfigReleaseFormat != ConfigReleaseDataFormat.JsonContent)
            {
                throw new RFrameworkException(
                    $"Unsupported Config release format '{options.ConfigReleaseFormat}'.");
            }

            if (options.ConfigBinaryProtection == ConfigProtectionMode.None)
            {
                return new ConfigProtectionExporter(
                    ConfigProtectionMode.None,
                    string.Empty,
                    string.Empty,
                    options.ConfigReleaseFormat,
                    null);
            }

            if (options.ConfigBinaryProtection
                != ConfigProtectionMode.EncryptedAndAuthenticated)
            {
                throw new RFrameworkException(
                    $"Unsupported Config protection mode '{options.ConfigBinaryProtection}'.");
            }

            string keyId = options.ConfigProtectionKeyId?.Trim();
            if (string.IsNullOrEmpty(keyId)
                || keyId.IndexOfAny(new[] { '\r', '\n' }) >= 0
                || Encoding.UTF8.GetByteCount(keyId) > 255)
            {
                throw new RFrameworkException("Config protection KeyId is invalid.");
            }

            string sourceRoot = NormalizeSourceRoot(options.ConfigProtectionSourceRoot);
            IKeyProvider provider = keyProvider ?? new EnvironmentVariableKeyProvider(
                keyId,
                options.ConfigProtectionKeyEnvironmentVariable);
            ValidateKey(provider, keyId);
            return new ConfigProtectionExporter(
                options.ConfigBinaryProtection,
                keyId,
                sourceRoot,
                options.ConfigReleaseFormat,
                new DefaultDataProtector(provider));
        }

        internal bool WriteBytesIfChanged(
            string path,
            string fileName,
            ConfigPayloadType payloadType,
            ConfigPayloadFormat payloadFormat,
            byte[] plaintext)
        {
            if (mode == ConfigProtectionMode.None)
            {
                return ConfigBinaryExporter.WriteBytesIfChanged(path, plaintext);
            }

            ConfigProtectionContext context = new ConfigProtectionContext(
                mode,
                sourceRoot + "/" + fileName,
                payloadType,
                payloadFormat);
            if (File.Exists(path) && HasSamePlaintext(path, plaintext, context))
            {
                return false;
            }

            byte[] protectedBytes = context.Protect(protector, plaintext, keyId);
            try
            {
                return ConfigBinaryExporter.WriteBytesIfChanged(path, protectedBytes);
            }
            finally
            {
                Array.Clear(protectedBytes, 0, protectedBytes.Length);
            }
        }

        internal bool WriteManifest(string binaryRoot)
        {
            string content =
                "formatVersion=1\n"
                + $"protectionMode={mode}\n"
                + $"envelopeVersion={(mode == ConfigProtectionMode.None ? 0 : DefaultDataProtector.FormatVersion)}\n"
                + $"keyId={keyId}\n"
                + $"releaseFormat={releaseFormat}\n"
                + $"sourceRoot={sourceRoot}\n";
            return JsonExportUtility.WriteTextIfChanged(
                Path.Combine(binaryRoot, ManifestName), content);
        }

        private bool HasSamePlaintext(
            string path,
            byte[] plaintext,
            ConfigProtectionContext context)
        {
            byte[] oldPlaintext = null;
            try
            {
                oldPlaintext = context.Unprotect(protector, File.ReadAllBytes(path));
                return AreEqual(oldPlaintext, plaintext);
            }
            catch (RFrameworkException)
            {
                return false;
            }
            finally
            {
                if (oldPlaintext != null)
                {
                    Array.Clear(oldPlaintext, 0, oldPlaintext.Length);
                }
            }
        }

        private static void ValidateKey(IKeyProvider provider, string keyId)
        {
            if (!provider.TryGetKey(keyId, out byte[] key) || key == null)
            {
                throw new RFrameworkException(
                    $"Config protection key '{keyId}' is unavailable.");
            }

            try
            {
                if (key.Length != 32)
                {
                    throw new RFrameworkException(
                        "Config protection key must contain exactly 32 bytes.");
                }
            }
            finally
            {
                Array.Clear(key, 0, key.Length);
            }
        }

        private static string NormalizeSourceRoot(string value)
        {
            string root = value?.Trim().Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(root)
                || root.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                throw new RFrameworkException(
                    "Config protection runtime path prefix is invalid.");
            }


            string[] segments = root.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0 || segments[i] == "..")
                {
                    throw new RFrameworkException(
                        "Config protection runtime path prefix is invalid.");
                }
            }

            return root;
        }

        private static bool AreEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class EnvironmentVariableKeyProvider : IKeyProvider
        {
            private readonly string keyId;
            private readonly string variableName;

            internal EnvironmentVariableKeyProvider(string keyId, string variableName)
            {
                this.keyId = keyId;
                this.variableName = variableName?.Trim();
                if (string.IsNullOrEmpty(this.variableName))
                {
                    throw new RFrameworkException(
                        "Config protection key environment variable name is invalid.");
                }
            }

            public bool TryGetKey(string requestedKeyId, out byte[] key)
            {
                key = null;
                if (!string.Equals(keyId, requestedKeyId, StringComparison.Ordinal))
                {
                    return false;
                }

                string value = Environment.GetEnvironmentVariable(variableName);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                try
                {
                    key = Convert.FromBase64String(value.Trim());
                    return true;
                }
                catch (FormatException)
                {
                    return false;
                }
            }
        }
    }
}
