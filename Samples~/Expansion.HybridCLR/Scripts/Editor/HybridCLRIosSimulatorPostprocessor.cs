#if UNITY_IOS
using System.IO;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 修正 Unity 2022 的 HybridCLR iOS 模拟器工程重复链接 libil2cpp.a 的问题。
    /// </summary>
    public static class HybridCLRIosSimulatorPostprocessor
    {
        /// <summary>
        /// HybridCLR 后处理完成后，仅从模拟器工程的 UnityFramework 取消链接 libil2cpp.a。
        /// </summary>
        [PostProcessBuild(1000)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS
                || PlayerSettings.iOS.sdkVersion != iOSSdkVersion.SimulatorSDK
                || !HybridCLRSettings.Instance.enable)
            {
                return;
            }

            string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            if (!File.Exists(projectPath))
            {
                return;
            }

            PBXProject project = new PBXProject();
            project.ReadFromFile(projectPath);

            string libraryGuid = project.FindFileGuidByProjectPath("Libraries/libil2cpp.a");
            if (string.IsNullOrEmpty(libraryGuid))
            {
                return;
            }

            project.RemoveFileFromBuild(project.GetUnityFrameworkTargetGuid(), libraryGuid);
            project.WriteToFile(projectPath);
            Debug.Log(
                "[UnityRFramework] 已从 iOS Simulator 的 UnityFramework 取消链接 libil2cpp.a，"
                + "避免与 libGameAssembly.a 产生重复符号。");
        }
    }
}
#endif
