using System.Collections.Generic;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 调试器信息接口。模块可选实现此接口，由 DebuggerOverlay / DebuggerWindow 读取内部状态。
    /// 仅在 Editor 和 Development Build 下被调用，Release 包中无开销。
    /// </summary>
    public interface IDebuggerInfo
    {
        /// <summary>
        /// 模块名称，用于面板左侧列表显示。
        /// </summary>
        string GetModuleName();

        /// <summary>
        /// 一行简短状态摘要，如 "Pool: 12/50 active"。
        /// </summary>
        string GetStatus();

        /// <summary>
        /// 详细键值对，如 {"Active Entities": "3", "Fsm States": "2"}。
        /// 返回 null 表示无详细信息。
        /// </summary>
        Dictionary<string, string> GetDetails();
    }
}
