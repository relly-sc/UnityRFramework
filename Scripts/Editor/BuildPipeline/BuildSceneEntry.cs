using System;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建配置中的单个场景条目，保存场景资产引用与参与构建的启用状态。
    /// 构建时通过场景资产路径解析并校验存在性，避免字符串路径在资产重命名后失效。
    /// </summary>
    [Serializable]
    public sealed class BuildSceneEntry
    {
        /// <summary>参与构建的场景资产引用。</summary>
        [Tooltip("参与构建的场景资产引用。")]
        public SceneAsset Scene;

        /// <summary>是否将该场景纳入构建；关闭后保留条目但跳过构建。</summary>
        [Tooltip("是否将该场景纳入构建；关闭后保留条目但跳过构建。")]
        public bool Enabled = true;

        /// <summary>
        /// 解析场景资产路径；场景引用缺失时返回空字符串。
        /// </summary>
        /// <returns>场景的 Assets 相对路径，缺失时返回空字符串。</returns>
        public string ResolvePath()
        {
            if (Scene == null)
            {
                return string.Empty;
            }

            return AssetDatabase.GetAssetPath(Scene);
        }

        /// <summary>
        /// 校验场景条目是否为有效且启用的场景。
        /// </summary>
        /// <returns>场景引用存在、路径有效且启用时返回 true。</returns>
        public bool IsValidEnabled()
        {
            if (!Enabled || Scene == null)
            {
                return false;
            }

            string path = ResolvePath();
            return !string.IsNullOrEmpty(path)
                && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }
    }
}
