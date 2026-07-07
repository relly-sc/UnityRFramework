#if UNITY_EDITOR

using System.Linq;
using UnityEditor;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 日志脚本定义符号菜单。
    /// 提供 UnityRFramework → Log Scripting Define Symbols 菜单项，
    /// 用于为当前激活的 BuildTargetGroup 添加或移除 ENABLE_LOG 预编译宏。
    /// </summary>
    public static class LogScriptingDefineSymbols
    {
        private const string LogMacro = "ENABLE_LOG";
        private const string MenuRoot = "UnityRFramework/Log Scripting Define Symbols/";

        /// <summary>
        /// 启用所有日志（定义 ENABLE_LOG 宏）。
        /// </summary>
        [MenuItem(MenuRoot + "Enable All Logs")]
        private static void EnableLog()
        {
            SetLogMacro(true);
        }

        /// <summary>
        /// 验证"启用所有日志"菜单项是否应显示为勾选状态。
        /// </summary>
        [MenuItem(MenuRoot + "Enable All Logs", true)]
        private static bool ValidateEnableLog()
        {
            return !IsLogMacroDefined();
        }

        /// <summary>
        /// 禁用所有日志（移除 ENABLE_LOG 宏）。
        /// </summary>
        [MenuItem(MenuRoot + "Disable All Logs")]
        private static void DisableLog()
        {
            SetLogMacro(false);
        }

        /// <summary>
        /// 验证"禁用所有日志"菜单项是否应显示为勾选状态。
        /// </summary>
        [MenuItem(MenuRoot + "Disable All Logs", true)]
        private static bool ValidateDisableLog()
        {
            return IsLogMacroDefined();
        }

        /// <summary>
        /// 根据当前状态添加或移除 ENABLE_LOG 宏。
        /// </summary>
        /// <param name="enable">true 添加宏，false 移除宏。</param>
        private static void SetLogMacro(bool enable)
        {
            BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);

            string[] symbols = defines.Split(';')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();

            if (enable)
            {
                if (!symbols.Contains(LogMacro))
                {
                    symbols = symbols.Append(LogMacro).ToArray();
                }
            }
            else
            {
                symbols = symbols.Where(s => s != LogMacro).ToArray();
            }

            string result = string.Join(";", symbols);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, result);
        }

        /// <summary>
        /// 检查当前 BuildTargetGroup 是否已定义 ENABLE_LOG 宏。
        /// </summary>
        private static bool IsLogMacroDefined()
        {
            BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);

            return defines.Split(';')
                .Select(s => s.Trim())
                .Any(s => s == LogMacro);
        }
    }
}

#endif
