using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>生成 Config 简单偏移混淆密钥文件。</summary>
    public static class ConfigKeyFileGenerator
    {
        public const string DefaultPath = "Assets/ConfigSource/configKey.bytes";

        [MenuItem("UnityRFramework/配置表工具/生成 Config 密钥文件")]
        public static void GenerateDefault()
        {
            Generate(DefaultPath);
        }

        /// <summary>生成新的随机密钥文件；已有文件必须由用户确认覆盖。</summary>
        public static bool Generate(string assetPath)
        {
            assetPath = assetPath?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(assetPath)
                || !assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                || !assetPath.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Config 密钥文件必须是 Assets 下的 .bytes 文件。", nameof(assetPath));
            }

            if (File.Exists(assetPath)
                && !EditorUtility.DisplayDialog(
                    "覆盖 Config 密钥文件",
                    "重新生成后，旧的加密 Config 将无法读取。确认覆盖？",
                    "覆盖",
                    "取消"))
            {
                return false;
            }

            byte[] key = new byte[32];
            byte[] offsetBytes = new byte[1];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(key);
                random.GetBytes(offsetBytes);
            }

            string directory = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(assetPath, Runtime.ConfigKeyFile.Encode(key, offsetBytes[0]));
            Array.Clear(key, 0, key.Length);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            Debug.Log($"Config 密钥文件已生成：{assetPath}");
            return true;
        }
    }
}
