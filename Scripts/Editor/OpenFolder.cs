#if UNITY_EDITOR

using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 快速打开常用文件夹的编辑器菜单工具。
    /// 在 Unity 菜单栏 UnityRFramework → Open Folder 下提供若干菜单项，
    /// 一键打开 Data Path、Persistent Data Path、StreamingAssets、Temporary Cache
    /// 以及 Unity 编辑器控制台日志文件所在位置，便于排查资源与运行时产物。
    /// </summary>
    public static class OpenFolder
    {
        private const string MenuRoot = "UnityRFramework/Open Folder/";

        /// <summary>
        /// 打开项目资源目录（Assets 文件夹所在的绝对路径）。
        /// </summary>
        [MenuItem(MenuRoot + "Data Path")]
        private static void OpenDataPath()
        {
            OpenDirectory(Application.dataPath);
        }

        /// <summary>
        /// 打开持久化数据目录（Application.persistentDataPath）。
        /// </summary>
        [MenuItem(MenuRoot + "Persistent Data Path")]
        private static void OpenPersistentDataPath()
        {
            OpenDirectory(Application.persistentDataPath);
        }

        /// <summary>
        /// 打开 StreamingAssets 目录（Application.streamingAssetsPath）。
        /// </summary>
        [MenuItem(MenuRoot + "Streaming Assets Path")]
        private static void OpenStreamingAssetsPath()
        {
            OpenDirectory(Application.streamingAssetsPath);
        }

        /// <summary>
        /// 打开临时缓存目录（Application.temporaryCachePath）。
        /// </summary>
        [MenuItem(MenuRoot + "Temporary Cache Path")]
        private static void OpenTemporaryCachePath()
        {
            OpenDirectory(Application.temporaryCachePath);
        }

        /// <summary>
        /// 打开 Unity 编辑器控制台日志文件（不存在则打开其所在目录）。
        /// </summary>
        [MenuItem(MenuRoot + "Console Log Path")]
        private static void OpenConsoleLogPath()
        {
            OpenFile(GetConsoleLogPath());
        }

        /// <summary>
        /// 打开指定目录，若目录不存在则先创建。
        /// </summary>
        /// <param name="directory">目标目录的绝对路径。</param>
        private static void OpenDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            StartProcess(directory);
        }

        /// <summary>
        /// 打开指定文件，若文件尚不存在则打开其所在目录，方便定位。
        /// </summary>
        /// <param name="filePath">目标文件的绝对路径。</param>
        private static void OpenFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            StartProcess(filePath);
        }

        /// <summary>
        /// 跨平台启动系统文件管理器打开目标路径。
        /// </summary>
        /// <param name="path">待打开的文件或目录路径。</param>
        private static void StartProcess(string path)
        {
#if UNITY_EDITOR_WIN
            Process.Start("explorer.exe", path.Replace('/', '\\'));
#elif UNITY_EDITOR_OSX
            Process.Start("open", path);
#elif UNITY_EDITOR_LINUX
            Process.Start("xdg-open", path);
#endif
        }

        /// <summary>
        /// 获取当前平台 Unity 编辑器控制台日志文件的绝对路径。
        /// </summary>
        /// <returns>日志文件路径，未知平台返回空字符串。</returns>
        private static string GetConsoleLogPath()
        {
#if UNITY_EDITOR_WIN
            return "C:/Users/" + Environment.UserName + "/AppData/Local/Unity/Editor/Editor.log";
#elif UNITY_EDITOR_OSX
            return "~/Library/Logs/Unity/Editor.log";
#elif UNITY_EDITOR_LINUX
            return "~/.config/unity3d/Editor.log";
#else
            return string.Empty;
#endif
        }
    }
}

#endif
