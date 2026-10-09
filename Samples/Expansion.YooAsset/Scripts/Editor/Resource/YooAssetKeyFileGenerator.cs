using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion.Editor
{
    /// <summary>生成供 YooAsset Bundle 构建和 Player 解密共用的密钥文件。</summary>
    public static class YooAssetKeyFileGenerator
    {
        public const string DefaultPath =
            "Assets/Resources/UnityRFramework/YooAssetKey.bytes";

        [MenuItem("UnityRFramework/Expansion/YooAsset/首次生成 Bundle 密钥文件")]
        public static void GenerateDefault()
        {
            Generate(DefaultPath);
        }

        [MenuItem("UnityRFramework/Expansion/YooAsset/更换 Bundle 密钥")]
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
                throw new ArgumentException(
                    $"YooAsset 密钥文件必须位于 {DefaultPath}。",
                    nameof(assetPath));
            }

            bool exists = File.Exists(assetPath);
            if (exists && !replace)
            {
                Debug.Log($"YooAsset Bundle 密钥文件已存在，保留原密钥：{assetPath}");
                return false;
            }

            if (replace && (!exists || !EditorUtility.DisplayDialog(
                    "更换 YooAsset Bundle 密钥",
                    "旧 Bundle 和缓存将无法解密。需要重新构建全部 Bundle 并发布使用新密钥的 Player，用户需重新下载。确认更换？",
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

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            File.WriteAllBytes(
                assetPath,
                ConfigKeyFile.Encode(key, offsetBytes[0]));
            Array.Clear(key, 0, key.Length);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            Debug.Log($"YooAsset Bundle 密钥文件已生成：{assetPath}");
            return true;
        }
    }
}
