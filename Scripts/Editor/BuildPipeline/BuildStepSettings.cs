using System;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建步骤配置条目，只保存步骤 Id、启用状态和私有配置资产引用。
    /// </summary>
    [Serializable]
    public sealed class BuildStepSettings
    {
        /// <summary>步骤唯一 Id，与步骤实现的唯一 Id 对应。</summary>
        [Tooltip("步骤唯一 Id。")]
        public string StepId = string.Empty;

        /// <summary>是否在构建流程中启用该步骤。</summary>
        [Tooltip("是否在构建流程中启用该步骤。")]
        public bool Enabled = true;

        /// <summary>步骤私有配置资产；无私有配置的步骤保持为空。</summary>
        [Tooltip("步骤私有配置资产。")]
        public ScriptableObject Configuration;

        /// <summary>
        /// 校验步骤条目是否具备参与构建的基本条件。
        /// </summary>
        /// <returns>步骤 Id 非空且已启用时返回 true。</returns>
        public bool IsUsable()
        {
            return Enabled
                && !string.IsNullOrWhiteSpace(StepId);
        }
    }
}
