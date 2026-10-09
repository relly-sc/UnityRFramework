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
        public const string DefaultPath = "Assets/Resources/UnityRFramework/ConfigKey.bytes";

        [MenuItem("UnityRFramework/配置表工具/首次生成 Config 密钥文件")]
        public static void GenerateDefault()
        {
            Generate(DefaultPath);
        }

        [MenuItem("UnityRFramework/配置表工具/更换 Config 密钥")]
        public static void ReplaceDefault()
        {
            Replace(DefaultPath);
        }

        /// <summary>首次生成密钥，已有文件保持原样。</summary>
        public static bool Generate(string assetPath)
        {
            return WriteKey(assetPath, false);
        }

        /// <summary>经确认后更换已有密钥。</summary>
        public static bool Replace(string assetPath)
        {
            return WriteKey(assetPath, true);
        }

        private static bool WriteKey(string assetPath, bool replace)
        {
            assetPath = assetPath?.Replace('\\', '/');
            if (!string.Equals(assetPath, DefaultPath, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Config 密钥文件必须位于 {DefaultPath}。", nameof(assetPath));
            }

            bool exists = File.Exists(assetPath);
            if (exists && !replace)
            {
                Debug.Log($"Config 密钥文件已存在，保留原密钥：{assetPath}");
                return false;
            }

            if (replace && (!exists || !EditorUtility.DisplayDialog(
                    "更换 Config 密钥",
                    "旧的加密 Config 将无法读取。需要重新导出配置并发布使用新密钥的 Player。确认更换？",
                    "更换",
                    "取消")))
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
