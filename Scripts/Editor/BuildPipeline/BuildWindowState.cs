using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建工具窗口的持久化状态，保存到 EditorPrefs。
    /// 包含上次选中的 Profile、滚动位置、折叠分区与最近一次构建摘要。
    /// </summary>
    public sealed class BuildWindowState
    {
        /// <summary>EditorPrefs 键前缀，避免与其他工具冲突。</summary>
        private const string KeyPrefix = "UnityRFramework.BuildWindow.";

        /// <summary>上次选中 Profile GUID 的 EditorPrefs 键。</summary>
        private const string ProfileGuidKey = KeyPrefix + "ProfileGuid";

        /// <summary>滚动位置的 EditorPrefs 键。</summary>
        private const string ScrollKey = KeyPrefix + "Scroll";

        /// <summary>折叠分区列表的 EditorPrefs 键。</summary>
        private const string FoldsKey = KeyPrefix + "Folds";

        /// <summary>最近一次构建摘要的 EditorPrefs 键。</summary>
        private const string LastBuildKey = KeyPrefix + "LastBuild";

        /// <summary>上次选中的 Profile GUID；为空表示尚未选择。</summary>
        public string SelectedProfileGuid = string.Empty;

        /// <summary>窗口滚动位置。</summary>
        public Vector2 ScrollPosition;

        /// <summary>当前处于折叠状态的分区键列表；不在列表中的分区默认展开。</summary>
        public List<string> FoldedSections = new List<string>();

        /// <summary>最近一次构建摘要；无记录时 HasRecord 为 false。</summary>
        public BuildWindowLastBuild LastBuild = new BuildWindowLastBuild();

        /// <summary>
        /// 从 EditorPrefs 读取窗口状态；键缺失或数据损坏时保持默认值。
        /// </summary>
        public void Load()
        {
            SelectedProfileGuid = EditorPrefs.GetString(ProfileGuidKey, string.Empty);

            ScrollPosition = new Vector2(
                EditorPrefs.GetFloat(ScrollKey + ".x", 0f),
                EditorPrefs.GetFloat(ScrollKey + ".y", 0f));

            string foldsJson = EditorPrefs.GetString(FoldsKey, string.Empty);
            if (!string.IsNullOrEmpty(foldsJson))
            {
                try
                {
                    FoldedSections = JsonUtility.FromJson<StringListWrapper>(foldsJson)?.Items
                        ?? new List<string>();
                }
                catch
                {
                    FoldedSections = new List<string>();
                }
            }

            string lastBuildJson = EditorPrefs.GetString(LastBuildKey, string.Empty);
            if (!string.IsNullOrEmpty(lastBuildJson))
            {
                try
                {
                    LastBuild = JsonUtility.FromJson<BuildWindowLastBuild>(lastBuildJson)
                        ?? new BuildWindowLastBuild();
                }
                catch
                {
                    LastBuild = new BuildWindowLastBuild();
                }
            }
        }

        /// <summary>
        /// 将窗口状态写入 EditorPrefs，供下次打开窗口恢复。
        /// </summary>
        public void Save()
        {
            EditorPrefs.SetString(ProfileGuidKey, SelectedProfileGuid);
            EditorPrefs.SetFloat(ScrollKey + ".x", ScrollPosition.x);
            EditorPrefs.SetFloat(ScrollKey + ".y", ScrollPosition.y);

            StringListWrapper wrapper = new StringListWrapper();
            wrapper.Items = FoldedSections;
            EditorPrefs.SetString(FoldsKey, JsonUtility.ToJson(wrapper));

            EditorPrefs.SetString(LastBuildKey, JsonUtility.ToJson(LastBuild));
        }

        /// <summary>
        /// 判断指定分区是否处于折叠状态。
        /// </summary>
        /// <param name="key">分区键。</param>
        /// <returns>折叠时返回 true。</returns>
        public bool IsSectionFolded(string key)
        {
            return FoldedSections.Contains(key);
        }

        /// <summary>
        /// 设置分区的折叠状态。
        /// </summary>
        /// <param name="key">分区键。</param>
        /// <param name="folded">是否折叠。</param>
        public void SetSectionFolded(string key, bool folded)
        {
            if (folded)
            {
                if (!FoldedSections.Contains(key))
                {
                    FoldedSections.Add(key);
                }
            }
            else
            {
                FoldedSections.Remove(key);
            }
        }
    }

    /// <summary>
    /// 最近一次构建摘要，随窗口状态持久化。
    /// </summary>
    [Serializable]
    public sealed class BuildWindowLastBuild
    {
        /// <summary>是否有有效构建记录；无记录时窗口显示占位文本。</summary>
        public bool HasRecord;

        /// <summary>构建使用的 Profile 名称。</summary>
        public string ProfileName = string.Empty;

        /// <summary>构建平台显示名称。</summary>
        public string Platform = string.Empty;

        /// <summary>构建版本号（含构建号）。</summary>
        public string Version = string.Empty;

        /// <summary>构建结果状态：成功、失败或取消。</summary>
        public string Status = string.Empty;

        /// <summary>构建耗时（秒）。</summary>
        public float DurationSeconds;

        /// <summary>产物输出路径。</summary>
        public string OutputPath = string.Empty;

        /// <summary>构建完成时刻文本。</summary>
        public string TimeText = string.Empty;
    }

    /// <summary>
    /// JsonUtility 不支持 Dictionary，折叠分区列表使用字符串数组包装序列化。
    /// </summary>
    [Serializable]
    internal sealed class StringListWrapper
    {
        /// <summary>字符串列表。</summary>
        public List<string> Items = new List<string>();
    }
}
