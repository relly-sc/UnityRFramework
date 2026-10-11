using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace UnityRFramework.Expansion.Platform.Windows
{
    /// <summary>
    /// Windows 外部程序启动与命令行参数读取工具。
    /// </summary>
    public static class WindowsProcessLauncher
    {
        public static bool TryLaunch(string filePath, string arguments, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                error = "外部程序路径为空。";
                return false;
            }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                UnityEngine.Debug.LogError($"启动外部程序失败：{filePath}，{exception.Message}");
                return false;
            }
#else
            error = "当前平台不是 Windows。";
            return false;
#endif
        }

        public static IReadOnlyList<string> GetArguments()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            var result = new List<string>(Math.Max(0, arguments.Length - 1));
            for (int i = 1; i < arguments.Length; i++)
            {
                result.Add(arguments[i]);
            }

            return result;
        }
    }
}
