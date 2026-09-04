using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>旧版固定版本字段，仅用于读取已有 EditorPrefs。</summary>
        public string Version = string.Empty;

        /// <summary>本次任务的 Recipe 中文名称。</summary>
        public string Recipe = string.Empty;

        /// <summary>仅 Player/Release 使用的实际 Player 版本。</summary>
        public string PlayerVersion = string.Empty;

        /// <summary>按 Recipe 提取的关键步骤结果。</summary>
        public string Summary = string.Empty;

        /// <summary>构建结果状态：成功、失败或取消。</summary>
        public string Status = string.Empty;

        /// <summary>构建耗时（秒）。</summary>
        public float DurationSeconds;

        /// <summary>产物输出路径。</summary>
        public string OutputPath = string.Empty;

        /// <summary>输出路径字段的语义：Player 目录或构建报告。</summary>
        public string OutputLabel = string.Empty;

        /// <summary>构建完成时刻文本。</summary>
        public string TimeText = string.Empty;

        /// <summary>根据终态任务生成 Recipe 感知的最近构建摘要。</summary>
        public static BuildWindowLastBuild Create(BuildPipelineState state)
        {
            BuildWindowLastBuild result = new BuildWindowLastBuild
            {
                HasRecord = true,
                ProfileName = state.ProfileName,
                Platform = state.TargetName,
                Recipe = GetRecipeText(state.Recipe),
                Status = state.Phase == BuildPipelinePhase.Succeeded
                    ? "成功"
                    : state.Phase == BuildPipelinePhase.Cancelled
                        ? "已取消"
                        : state.Phase == BuildPipelinePhase.ManualIntervention
                            ? "人工处理"
                            : "失败",
                TimeText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            if (DateTime.TryParse(state.CreatedAt, out DateTime createdAt)
                && DateTime.TryParse(state.UpdatedAt, out DateTime updatedAt))
            {
                result.DurationSeconds =
                    (float)Math.Max(0.0, (updatedAt - createdAt).TotalSeconds);
            }

            bool producesPlayer = state.Recipe == BuildRecipe.Player
                || state.Recipe == BuildRecipe.Release;
            if (producesPlayer)
            {
                result.PlayerVersion =
                    $"{state.PublicVersion}（构建号 {state.BuildNumber}）";
                result.OutputLabel = "Player 目录";
                result.OutputPath = ResolvePlayerDirectory(state);
            }
            else
            {
                result.OutputLabel = "构建报告";
                result.OutputPath = Path.GetFullPath(Path.Combine(
                    "Bundles",
                    BuildReportWriter.GetAssetOnlyReportDirectory(state),
                    BuildReportWriter.ReportFileName));
            }

            result.Summary = BuildStepSummary(state);
            return result;
        }

        private static string GetRecipeText(BuildRecipe recipe)
        {
            switch (recipe)
            {
                case BuildRecipe.Player: return "Player";
                case BuildRecipe.Assets: return "资源";
                case BuildRecipe.HotUpdate: return "热更新";
                case BuildRecipe.Release: return "完整发布";
                default: return recipe.ToString();
            }
        }

        private static string ResolvePlayerDirectory(BuildPipelineState state)
        {
            if (string.IsNullOrEmpty(state.OutputRootAbsolute))
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(state.OutputDirectory)
                ? state.OutputRootAbsolute
                : Path.Combine(state.OutputRootAbsolute, state.OutputDirectory);
        }

        private static string BuildStepSummary(BuildPipelineState state)
        {
            string[] relevantSteps;
            switch (state.Recipe)
            {
                case BuildRecipe.Assets:
                    relevantSteps = new[] { "config", "yooasset" };
                    break;
                case BuildRecipe.HotUpdate:
                    relevantSteps = new[] { "hybridclr", "obfuz", "yooasset" };
                    break;
                case BuildRecipe.Release:
                    relevantSteps = new[] { "config", "hybridclr", "obfuz", "yooasset" };
                    break;
                default:
                    relevantSteps = Array.Empty<string>();
                    break;
            }

            List<string> messages = new List<string>();
            for (int i = 0; i < relevantSteps.Length; i++)
            {
                BuildStepRecord record = state.CompletedSteps?.Find(item =>
                    string.Equals(item.StepId, relevantSteps[i], StringComparison.OrdinalIgnoreCase));
                if (record != null && !string.IsNullOrWhiteSpace(record.Message))
                {
                    messages.Add(record.Message.Trim());
                }
            }

            if (state.FailedStep != null
                && !string.IsNullOrWhiteSpace(state.FailedStep.Message))
            {
                messages.Add(state.FailedStep.Message.Trim());
            }

            return string.Join("\n", messages);
        }
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
